using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotKcp
{
    public class UdpTransport : IDisposable
    {
        UdpClient _socket;
        KcpChannel _kcpChannel;
        uint Conv;
        //private IPEndPoint RemoteEndpoint;

        public event EventHandler<Memory<byte>> OnMessageReceived;
        public UdpTransport(uint conv)
        {
            Conv = conv;
        }

        public void Connect(string ip, int port)
        {
            _socket = new UdpClient();
            _kcpChannel = new KcpChannel(Conv);
            _kcpChannel.MessageReceived += (d) =>
            {
                OnMessageReceived?.Invoke(this, d);
            };
            _kcpChannel.ProcessPost += (d) =>
            {
                _socket.Send(d.ToArray(), d.Length);
            };
            _kcpChannel.Run();

            _socket.Connect(ip, port);
            //_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            //_socket.Bind(IPEndPoint.);
            //Thread t = new Thread(sendMsg);
            //t.Start();
            _= ReciveMsgAsync();
        }

        public void Dispose()
        {
            _socket.Dispose();
            _kcpChannel.Dispose();
        }

        /// <summary>
        /// 向特定ip的主机的端口发送数据报
        /// </summary>
        public void SendMsg(byte[] data)
        {
            _kcpChannel.Send(data);
        }

        /// <summary>
        /// 接收发送给本机ip对应端口号的数据报
        /// </summary>
        private async Task ReciveMsgAsync()
        {
            while (true)
            {
                //IPEndPoint point = new IPEndPoint(IPAddress.Any, 0);//用来保存发送方的ip和端口号
                //byte[] buffer = _socket.Receive(ref point);//接收数据报
                var result = await _socket.ReceiveAsync();
                _kcpChannel.MockInput(result.Buffer);
            }
        }
    }
}
