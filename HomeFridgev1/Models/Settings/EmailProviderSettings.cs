namespace HomeFridgev1.Models.Settings
{
    public class EmailProviderSettings
    {
        public string Provider { get; set; } = "Resend";

        public string ApiKey { get; set; } = string.Empty;

        public string FromEmail { get; set; } = string.Empty;

        public string FromName { get; set; } = "Home's Fridge";
    }
}
