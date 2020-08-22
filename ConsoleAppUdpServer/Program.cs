using DotKcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ConsoleAppUdpServer
{
    class Program
    {
        static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging => logging.ClearProviders().AddNLog().SetMinimumLevel(LogLevel.Debug))
                .ConfigureServices(services => 
                {
                    services.AddSingleton<UdpServer>();
                    services.AddHostedService<HostedUdpService>();
                });
    }
}
