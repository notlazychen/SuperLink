using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DotKcp
{
    /// <summary>
    /// 可靠会话
    /// </summary>
    public class ReliableSession : IDisposable
    {
        private KcpChannel _kcpChannel;

        public string SessionId { get; private set; }
        public EndPoint EndPoint { get; private set; }
        public UdpServer Server { get; private set; }
        public uint Conv { get; private set; }

        public event CloseDelegate Closed;

        public ReliableSession(UdpServer server, string sessionId, EndPoint endPoint, uint conv)
        {
            Server = server;
            SessionId = sessionId;
            Conv = conv;
            EndPoint = endPoint;
        }

        public void Dispose()
        {
            _kcpChannel.Dispose();
        }

        protected virtual void OnDataReceived(Memory<byte> buf)
        {
            string message = Encoding.UTF8.GetString(buf.Span);
            //Console.WriteLine("收到消息: {0}", message);
            //回显
            var sendData = Encoding.UTF8.GetBytes($"[{EndPoint}]发送一条消息: {message}");
            this.Send(sendData);
        }

        /// <summary>
        /// 可靠发送
        /// </summary>
        public void Send(byte[] data)
        {
            _kcpChannel.Send(data);
        }


        internal void MockInput(byte[] buf, int offset, int count)
        {
            _kcpChannel.MockInput(new Span<byte>(buf, offset, count));
        }

        internal void MockConnect()
        {
            _kcpChannel = new KcpChannel(Conv);
            _kcpChannel.MessageReceived += (d) =>
            {
                OnDataReceived(d);
            };
            _kcpChannel.ProcessPost += (d) =>
            {
                Server.SendMessageByUnicast(d.ToArray(), EndPoint);
            };
            _kcpChannel.Closed += OnClose;
            _kcpChannel.Run();
        }

        private void OnClose()
        {
            Closed?.Invoke();
        }
    }
}
