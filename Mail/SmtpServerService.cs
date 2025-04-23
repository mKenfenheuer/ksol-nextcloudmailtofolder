using KSol.NextCloudMailToFolder.Data;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;
using System.Buffers;
using System.Net;
using System.Net.Mail;

namespace KSol.NextCloudMailToFolder.Mail
{
    public class SmtpServerService : IHostedService, IDisposable
    {
        readonly ILogger<SmtpServerService> _logger;
        private SmtpServer.SmtpServer _server;
        private SmtpServer.ComponentModel.ServiceProvider _serviceProvider;
        readonly IServiceScopeFactory _scopeFactory;
        private readonly SmtpServerConfiguration _configuration = new SmtpServerConfiguration();
        private Task _serverTask;

        public SmtpServerService(ILogger<SmtpServerService> logger, IServiceScopeFactory scopeFactory, IConfiguration configuration)
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
            _serviceProvider.Add(new SmtpServerMessageStore(_scopeFactory, options.ServerName));

            _server = new SmtpServer.SmtpServer(options, _serviceProvider);
        }

        public void Dispose()
        {
            _serviceProvider = null;
            _server = null;
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

        public class SmtpServerMessageStore : MessageStore
        {
            private IServiceScopeFactory _scopeFactory;
            private readonly string _host;

            public SmtpServerMessageStore(IServiceScopeFactory scopeFactory, string host)
            {
                _scopeFactory = scopeFactory;
                _host = host;
            }

            public override async Task<SmtpResponse> SaveAsync(ISessionContext context, IMessageTransaction transaction, ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
            {
                using (var scope = _scopeFactory.CreateScope())
                using (var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
                    foreach (var destination in transaction.To)
                    {
                        var destinationAddress = $"{destination.User}@{destination.Host}";
                        var destinationEntity = await dbContext.Destinations
                            .Include(d => d.User)
                            .FirstOrDefaultAsync(d => d.Recipient == destinationAddress);

                        if (destinationEntity != null)
                        {
                            var mailMessage = MimeMessage.Load(new MemoryStream(buffer.ToArray()));
                            // Save the message to the destination folder
                            // Implement your logic to save the message to the destination folder
                            // For example, you can use the destinationEntity.Path property to determine where to save the message
                            // You can also use the transaction.Message property to get the message content
                            var attachments = mailMessage.Attachments.ToArray();

                            HttpClient client = new HttpClient();
                            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", destinationEntity.User.Token);

                            foreach (MimePart attachment in attachments)
                            {
                                var base64 = new StreamReader(attachment.Content.Stream).ReadToEnd();
                                var ms = new MemoryStream(Convert.FromBase64String(base64));

                                var content = new StreamContent(ms);
                                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(attachment.ContentType.MimeType);
                                var result = await client.PutAsync("https://nc.ksol.it/remote.php/dav/files/" + destinationEntity.User.Id + "/" + destinationEntity.Path + "/" + attachment.FileName, content);
                            }

                            return new SmtpResponse(SmtpReplyCode.Ok, $"2.0.0 OK: queueued as {mailMessage.MessageId}"); 
                        }
                    }

                return new SmtpResponse(SmtpReplyCode.BadEmailAddress, "Destination not found");
            }
        }

        public class SmtpServerConfiguration
        {
            public SmtpServerEndPoint[]? EndPoints { get; set; }
            public string? Hostname { get; set; }
            public class SmtpServerEndPoint
            {
                public string? Address { get; set; }
                public int Port { get; set; }
            }
        }
    }
}
