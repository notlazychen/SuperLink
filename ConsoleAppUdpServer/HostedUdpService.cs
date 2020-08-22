using DotKcp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleAppUdpServer
{
    public class HostedUdpService : IHostedService
    {
        private UdpServer _server;
        private ILogger _logger;
        public HostedUdpService(UdpServer server, ILogger<HostedUdpService> logger)
        {
            _server = server;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _server.NewSessionConnect += (session) =>
            {
                _logger.LogInformation($"[新连接进入] conv:{session.Conv}, 当前连接数:{_server.SessionCount}");
            };
            _server.SessionDisconnect += (session, reason) =>
            {
                _logger.LogInformation($"[旧连接断开] conv:{session.Conv}, 当前连接数:{_server.SessionCount}");
            };
            int port = 2020;
            _server.Start(port);
            _logger.LogInformation($"开始监听UDP端口: {port}");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
