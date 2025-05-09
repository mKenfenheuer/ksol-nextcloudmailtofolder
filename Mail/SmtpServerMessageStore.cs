using KSol.NextCloudMailToFolder.Api;
using KSol.NextCloudMailToFolder.Data;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;
using System.Buffers;
using System.Net;

namespace KSol.NextCloudMailToFolder.Mail
{
    public class SmtpServerMessageStore : MessageStore
    {
        private readonly ILogger<SmtpServerService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public SmtpServerMessageStore(ILogger<SmtpServerService> logger, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public override async Task<SmtpResponse> SaveAsync(ISessionContext context, IMessageTransaction transaction, ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
        {
            var sent = false;
            var failed = false;
            using (var scope = _serviceScopeFactory.CreateScope())
            using (var nextcloudApi = scope.ServiceProvider.GetRequiredService<NextCloudApi>())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
                foreach (var destination in transaction.To)
                {
                    var destinationAddress = $"{destination.User}@{destination.Host}";
                    var destinationEntity = await dbContext.Destinations
                        .Include(d => d.User)
                        .FirstOrDefaultAsync(d => d.Recipient == destinationAddress);

                    if (destinationEntity != null && destinationEntity.User != null)
                    {
                        var mailMessage = MimeMessage.Load(new MemoryStream(buffer.ToArray()));
                        // Save the message to the destination folder
                        // Implement your logic to save the message to the destination folder
                        // For example, you can use the destinationEntity.Path property to determine where to save the message
                        // You can also use the transaction.Message property to get the message content
                        var attachments = mailMessage.BodyParts.Cast<MimePart>().Where(b => b.FileName != null).ToArray();

                        foreach (MimePart attachment in attachments)
                        {
                            byte[] bytes = [];

                            if (attachment.ContentTransferEncoding == ContentEncoding.Base64)
                            {
                                var base64 = new StreamReader(attachment.Content.Stream).ReadToEnd();
                                bytes = Convert.FromBase64String(base64);
                            }
                            else
                            {
                                using (var stream = new MemoryStream())
                                {
                                    await attachment.Content.Stream.CopyToAsync(stream);
                                    bytes = stream.ToArray();
                                }
                            }

                            var ms = new MemoryStream(bytes);

                            var result = await nextcloudApi.UploadFileAsync(destinationEntity.User.Id, ms, attachment.ContentType.MimeType, destinationEntity.User.Id + "/" + destinationEntity.Path + "/" + attachment.FileName);
                            if (result)
                            {
                                _logger.LogInformation($"Attachment {destinationEntity.Path}/{attachment.FileName} for user {destinationEntity.UserId} uploaded to Nextcloud successfully.");
                                sent = true;
                            }
                            else
                            {
                                failed = true;
                                _logger.LogError($"Failed to upload attachment {attachment.FileName} to Nextcloud.");
                            }
                        }
                    }
                }

            if (sent && !failed)
                return new SmtpResponse(SmtpReplyCode.Ok, $"2.0.0 OK");

            _logger.LogInformation($"Message rejected from {transaction.From.User}@{transaction.From.Host} to {string.Join(", ", transaction.To.Select(t => t.User + "@" + t.Host))}");
            return new SmtpResponse(SmtpReplyCode.BadEmailAddress, "Destination not found");
        }
    }
}
