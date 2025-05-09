using KSol.NextCloudMailToFolder.Api;
using KSol.NextCloudMailToFolder.Data;
using SmtpServer;
using System.Net;
using System.Net.Mail;

namespace KSol.NextCloudMailToFolder.Mail
{
    public class SmtpServerService : IHostedService
    {
        readonly ILogger<SmtpServerService> _logger;
        private readonly SmtpServer.SmtpServer _server;
        private readonly SmtpServer.ComponentModel.ServiceProvider _serviceProvider;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SmtpServerConfiguration _configuration = new SmtpServerConfiguration();
        private Task _serverTask;

        public SmtpServerService(ILogger<SmtpServerService> logger, IServiceScopeFactory scopeFactory, IConfiguration configuration, SmtpServerMessageStore messageStore)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            configuration.Bind("SMTP", _configuration);

            var optionsBuilder = new SmtpServerOptionsBuilder()
                .ServerName(_configuration.Hostname ?? Dns.GetHostName())
                .MaxAuthenticationAttempts(5);

            if (_configuration.EndPoints == null || _configuration.EndPoints.Length == 0)
            {
                throw new Exception("No SMTP endpoints configured in appsettings.json");
            }

            using (var scope = _scopeFactory.CreateScope())
            using (var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
                foreach (var endpoint in _configuration.EndPoints)
                {
                    var smtpEndPoint = new EndpointDefinitionBuilder()
                        .Endpoint(new IPEndPoint(IPAddress.Parse(endpoint.Address), endpoint.Port))
                        .AuthenticationRequired(false)
                        .Build();

                    optionsBuilder = optionsBuilder.Endpoint(smtpEndPoint);
                }


            var options = optionsBuilder.Build();

            _serviceProvider = new SmtpServer.ComponentModel.ServiceProvider();
            _serviceProvider.Add(messageStore);

            _server = new SmtpServer.SmtpServer(options, _serviceProvider);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting SmtpServer");
            _ = _server.StartAsync(cancellationToken);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping SmtpServer");
            return Task.Run(_server.Shutdown);
        }
    }
}
