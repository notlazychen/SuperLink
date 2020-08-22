using System;
using System.Buffers;
using System.Net.Sockets.Kcp;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleAppServer
{
    class Program
    {
        static void Main(string[] args)
        {
            int recv = 0;
            Console.WriteLine("Hello World!");

            Parallel.For(0, 10, i =>
            {
                uint conv = (uint)(i+ 1);
                var handlerServer = new CallbackHandle();
                Session s = new Session(conv, handlerServer);
                s.MessageReceived += (session, d) =>
                {
                    var str = Encoding.UTF8.GetString(d.Span);
                    Console.WriteLine("服务端收到数据:{0}", str);
                };

                var handlerClient = new CallbackHandle();
                Session c = new Session(conv, handlerClient);
                handlerServer.DoPost = (d) =>
                {
                    c.MockInput(d.Span);
                };
                s.SetConnected();

                handlerClient.DoPost = (d) =>
                {
                    s.MockInput(d.Span);
                };
                c.MessageReceived += (session, d) =>
                {
                    var str = Encoding.UTF8.GetString(d.Span);

                    Interlocked.Increment(ref recv);
                    //Console.WriteLine("客户端收到数据:{0}, 回传Ack", str);
                    //var ack = "ack";
                    //var data = Encoding.UTF8.GetBytes(ack);
                    //session.Send(data);
                };
                c.SetConnected();

                for(int j = 0; j< 100; j++)
                {
                    s.Send(Encoding.UTF8.GetBytes(j.ToString()));
                }
            });

            while (true)
            {
                var str = Console.ReadLine();
                if (str == "q")
                {
                    break;
                }
                //s.Send(Encoding.UTF8.GetBytes(str));
                Console.WriteLine($"收到: {recv}");
            }


            Console.WriteLine("byebye");
        }
    }

    public delegate void ReceiveMessageEventHandler(Session session, Memory<byte> data);

    public delegate void ProcessOutputDelegate(Memory<byte> data);
    public class Session
    {
        private Kcp _kcp;
        public event ReceiveMessageEventHandler MessageReceived;

        public Session(uint conv, CallbackHandle callback)
        {
            _kcp = new Kcp(conv, callback);
            _kcp.NoDelay(1, 10, 2, 1);//fast
            _kcp.WndSize(64, 64);
            _kcp.SetMtu(512);
        }

        public void SetConnected()
        {
            Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        _kcp.Update(DateTime.UtcNow);
                        int len;
                        while ((len = _kcp.PeekSize()) > 0)
                        {
                            var buffer = new byte[len];
                            if (_kcp.Recv(buffer) >= 0)
                            {
                                OnReceive(buffer);
                                //handle.Receive(buffer);
                            }
                        }
                        await Task.Delay(5);
                    }
                }
                catch (Exception e)
                {
                    e.ToString();
                }
            });
        }

        private void OnReceive(Memory<byte> data)
        {
            MessageReceived?.Invoke(this, data);
        }

        public virtual void Send(byte[] data)
        {
            Console.WriteLine("准备发送:{0}字节", data.Length);
            _kcp.Send(data);
        }

        Random random = new Random();
        public void MockInput(Span<byte> span)
        {
            var next = random.Next(100);
            if (next >= 0)///随机丢包
            {
                _kcp.Input(span);
            }
        }
    }

    public class CallbackHandle : IKcpCallback
    {
        public void Output(IMemoryOwner<byte> buffer, int avalidLength)
        {
            using (buffer)
            {
                Console.WriteLine("执行发送:{0}字节", avalidLength);
                var data = buffer.Memory.Slice(0, avalidLength);
                //var str = Encoding.UTF8.GetString(data.Span);
                DoPost?.Invoke(data);
            }
        }

        //protected void DoPost(Memory<byte> data)
        //{
        //    var str = Encoding.UTF8.GetString(data.Span);
        //    Console.WriteLine("执行发送:{0}", str);
        //}
        public ProcessOutputDelegate DoPost;
    }
}
