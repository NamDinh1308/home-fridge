namespace HomeFridgev1.Services.Interfaces
{
    /// <summary>Inline image attachment to embed in email HTML via cid: reference.</summary>
    public sealed class EmailInlineImage
    {
        public required string ContentId { get; init; }
        public required string Filename { get; init; }
        public required byte[] Data { get; init; }
        public string ContentType { get; init; } = "image/jpeg";
    }

    public interface IEmailNotificationService
    {
        Task SendAsync(
            IEnumerable<string> toEmails,
            string subject,
            string htmlBody,
            string? textBody = null,
            IEnumerable<EmailInlineImage>? inlineImages = null,
            CancellationToken cancellationToken = default);
    }
}
