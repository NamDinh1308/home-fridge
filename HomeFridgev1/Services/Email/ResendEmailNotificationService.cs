using HomeFridgev1.Models.Settings;
using HomeFridgev1.Services.Interfaces;
using Microsoft.Extensions.Options;
using Resend;

namespace HomeFridgev1.Services.Email
{
    public class ResendEmailNotificationService : IEmailNotificationService
    {
        private readonly IResend _resend;
        private readonly EmailProviderSettings _settings;
        private readonly ILogger<ResendEmailNotificationService> _logger;

        public ResendEmailNotificationService(
            IResend resend,
            IOptions<EmailProviderSettings> settings,
            ILogger<ResendEmailNotificationService> logger)
        {
            _resend = resend;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task SendAsync(
            IEnumerable<string> toEmails,
            string subject,
            string htmlBody,
            string? textBody = null,
            IEnumerable<EmailInlineImage>? inlineImages = null,
            CancellationToken cancellationToken = default)
        {
            var recipients = toEmails
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!recipients.Any())
            {
                _logger.LogInformation("Skip sending email because there are no valid recipients.");
                return;
            }

            var message = new EmailMessage
            {
                From    = $"{_settings.FromName} <{_settings.FromEmail}>",
                Subject = subject,
                HtmlBody = htmlBody,
                TextBody = textBody ?? string.Empty
            };

            foreach (var email in recipients)
                message.To.Add(email);

            // Attach inline images (CID embedding)
            var images = inlineImages?.ToList() ?? new List<EmailInlineImage>();
            if (images.Any())
            {
                message.Attachments ??= new List<EmailAttachment>();
                foreach (var img in images)
                {
                    message.Attachments.Add(new EmailAttachment
                    {
                        Filename    = img.Filename,
                        Content     = Convert.ToBase64String(img.Data),
                        ContentType = img.ContentType,
                        ContentId   = img.ContentId
                    });
                }
            }

            var response = await _resend.EmailSendAsync(message, cancellationToken);

            _logger.LogInformation(
                "Sent email via Resend. Recipients: {RecipientCount}. EmailId: {EmailId}",
                recipients.Count,
                response?.Content.ToString() ?? "unknown");
        }
    }
}
