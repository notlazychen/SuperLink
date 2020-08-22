using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotKcp
{
    public delegate void NewSessionConnectedDelegate(Session session);
    public delegate void SessionDisconnectedDelegate(Session session, string reason);

    public class UdpServer: IDisposable
    {
        Socket _socket; 
        const string BROADCAST_HOST = "255.255.255.255";
        public static int MAX_PACKAGE_SIZE { get; private set; }
        private SocketAsyncEventArgs _recvEventArg = null;

        public int LocalPort { get; private set; }
        public int SessionCount
        {
            get { return _sessions.Count; }
        }

        private ConcurrentDictionary<EndPoint, Session> _sessions = new ConcurrentDictionary<EndPoint, Session>();
        private EleasticStack<uint> _convPool = new EleasticStack<uint>(i => (uint)i);

        public event NewSessionConnectedDelegate NewSessionConnect;
        public event SessionDisconnectedDelegate SessionDisconnect;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="bufferSize">用于socket发送和接受的缓存区大小</param>
        public UdpServer(int maxPackageSize = 1024)
        {
            MAX_PACKAGE_SIZE = maxPackageSize;
            ////设置用于发送数据的SocketAsyncEventArgs
            //sendBuff = new byte[bufferSize];
            //sendEventArg = new SocketAsyncEventArgs();
            //sendEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
            //sendEventArg.SetBuffer(sendBuff, 0, bufferSize);
            //设置用于接受数据的SocketAsyncEventArgs
            var recvBuff = new byte[MAX_PACKAGE_SIZE];
            _recvEventArg = new SocketAsyncEventArgs();
            _recvEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
            _recvEventArg.SetBuffer(recvBuff, 0, recvBuff.Length);
        }

        public void Start(int port)
        {
            LocalPort = port;
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            ////设置广播
            //_socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            _socket.Bind(new IPEndPoint(IPAddress.Any, LocalPort));//绑定端口号和IP
            StartRecvFrom();
        }

        /// <summary>
        /// 开始接受udp客户端发送的数据
        /// </summary>
        private void StartRecvFrom()
        {
            _recvEventArg.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            bool willRaiseEvent = _socket.ReceiveFromAsync(_recvEventArg);
            if (!willRaiseEvent)
            {
                ProcessReceive(_recvEventArg);
            }
        }

        private void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.ReceiveFrom:
                    ProcessReceive(e);
                    break;
                case SocketAsyncOperation.SendTo:
                    ProcessSend(e);
                    break;
                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }
        }

        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                try
                {
                    bool isNew = false;
                    var endpoint = e.RemoteEndPoint;
                    var session = _sessions.GetOrAdd(endpoint, ep => 
                    {
                        var conv = BinaryPrimitives.ReadUInt32LittleEndian(e.Buffer);
                        var session = new Session(this, Guid.NewGuid().ToString(), endpoint, conv);                        
                        isNew = true;
                        return session;
                    });
                    if(isNew)
                    {
                        session.Closed += () =>
                        {
                            session.Dispose();
                            _sessions.TryRemove(endpoint, out var s2);
                            SessionDisconnect?.Invoke(session, "timeover");
                        };
                        session.MockConnect();
                        NewSessionConnect?.Invoke(session);
                    }
                    session.MockInput(e.Buffer, e.Offset, e.BytesTransferred);
                }
                catch(Exception ex)
                {
                    //todo: gloable exception filter
                }
                StartRecvFrom();
            }
            else
            {
                Restart();
            }
        }

        private void ProcessSend(SocketAsyncEventArgs e)
        {
            //AsyncUserToken token = (AsyncUserToken)e.UserToken;
            if (e.SocketError == SocketError.Success)
            {
                try
                {
                }
                catch (Exception ex)
                {
                    //todo: gloable exception filter
                }
            }
            else
            {
                Restart();
            }
        }

        /// <summary>
        /// 重新启动udp服务器
        /// </summary>
        public void Restart()
        {
            CloseSocket();
            Console.WriteLine("Udpserver stopped...");
            //CloseSocket();
            //Start(LocalPort);
        }

        /// <summary>
        /// 关闭udp服务器
        /// </summary>
        public void CloseSocket()
        {
            if (_socket == null)
                return;

            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch { }

            try
            {
                _socket.Close();
                _socket.Dispose();
            }
            catch { }
        }

        ///// <summary>
        ///// 发送广播
        ///// </summary>
        ///// <param name="message"></param>
        //public void SendMessageByBroadcast(byte[] data)
        //{
        //    if (_socket == null)
        //        throw new ArgumentNullException("socket cannot be null");
        //    if (data == null || data.Length == 0)
        //        throw new ArgumentNullException("message cannot be null");

        //    byte[] buff = data;
        //    if (buff.Length > _MAX_PACKAGE_SIZE)
        //        throw new ArgumentOutOfRangeException("message is out off range");


        //    var sendBuff = new byte[_MAX_PACKAGE_SIZE];
        //    var sendEventArg = new SocketAsyncEventArgs();
        //    sendEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
        //    sendEventArg.SetBuffer(sendBuff, 0, sendBuff.Length);

        //    sendEventArg.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(BROADCAST_HOST), LocalPort);
        //    buff.CopyTo(sendEventArg.Buffer, 0);
        //    sendEventArg.SetBuffer(0, buff.Length);
        //    bool willRaiseEvent = _socket.SendToAsync(sendEventArg);
        //    if (!willRaiseEvent)
        //    {
        //        ProcessSend(sendEventArg);
        //    }
        //}

        /// <summary>
        /// 发送单播
        /// </summary>
        public void SendMessageByUnicast(byte[] data, EndPoint remoteEndPoint)
        {
            if (_socket == null)
                throw new ArgumentNullException("socket cannot be null");
            if (data == null || data.Length == 0)
                throw new ArgumentNullException("message cannot be null");

            byte[] buff = data;
            if (buff.Length > MAX_PACKAGE_SIZE)
                throw new ArgumentOutOfRangeException("message is out off range");

            var sendBuff = new byte[MAX_PACKAGE_SIZE];
            var sendEventArg = new SocketAsyncEventArgs();
            sendEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
            sendEventArg.SetBuffer(sendBuff, 0, sendBuff.Length);

            sendEventArg.RemoteEndPoint = remoteEndPoint;
            buff.CopyTo(sendEventArg.Buffer, 0);
            sendEventArg.SetBuffer(0, buff.Length);
            bool willRaiseEvent = _socket.SendToAsync(sendEventArg);
            if (!willRaiseEvent)
            {
                ProcessSend(sendEventArg);
            }
        }

        public void Dispose()
        {
            CloseSocket();
        }
    }
}
