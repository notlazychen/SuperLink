using DotKcp;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace ConsoleAppUdpClient
{
    class Program
    {
        static int ReceivedCount = 0;
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Thread.Sleep(1000);
            var sw = Stopwatch.StartNew();
            //for(int i = 0; i< 10; i++)
            int SocketCount = 1;
            int PackageCount = 500;
            for (int i = 0; i < SocketCount; i++)
            {
                new Thread(() =>
                {
                    var conv = (uint)(i);
                    UdpTransport client = new UdpTransport(conv);
                    client.OnMessageReceived += (obj, buf) =>
                    {
                        var cnt = Interlocked.Increment(ref ReceivedCount);
                        if (cnt == SocketCount * PackageCount)
                        {
                            sw.Stop();
                            Console.WriteLine("over");
                        }
                        string message = Encoding.UTF8.GetString(buf.Span);
                        Console.WriteLine("[收到]" + message);
                    };
                    client.Connect("127.0.0.1", 2020);

                    //Thread.Sleep(1000);
                    for (int x = 0; x < PackageCount; x++)
                    {
                        var str = $"[{conv}] {x}";
                        client.SendMsg(Encoding.UTF8.GetBytes(str));
                        Thread.Sleep(1000);
                    }
                }).Start();
            }

            while (true)
            {
                var str = Console.ReadLine();
                if(str == "q")
                {
                    break;
                }

                Console.WriteLine("收到总:{0}, 用时{1}", ReceivedCount, sw.ElapsedMilliseconds);
            }

            Console.WriteLine("byebye");
        }
    }
}
