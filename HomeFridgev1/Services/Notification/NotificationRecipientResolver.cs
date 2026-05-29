using HomeFridgev1.Data;
using HomeFridgev1.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HomeFridgev1.Services.Notification
{
    public class NotificationRecipientResolver : INotificationRecipientResolver
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationRecipientResolver> _logger;

        public NotificationRecipientResolver(ApplicationDbContext context, ILogger<NotificationRecipientResolver> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<string>> GetRecipientsForExpiryAsync(int householdId, CancellationToken cancellationToken = default)
        {
            var settings = await _context.HouseholdSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.HouseholdId == householdId, cancellationToken);

            if (settings == null)
            {
                _logger.LogWarning("[Notification] HouseholdSettings not found for HouseholdId={HouseholdId}", householdId);
                return new List<string>();
            }

            _logger.LogDebug("[Notification] HouseholdSettings: EnableEmail={EnableEmail}, EnableExpiry={EnableExpiry}",
                settings.EnableEmailNotifications, settings.EnableExpiryNotifications);

            if (!settings.EnableEmailNotifications || !settings.EnableExpiryNotifications)
            {
                _logger.LogInformation("[Notification] Expiry email skipped — email or expiry notifications disabled in household settings.");
                return new List<string>();
            }

            var recipients = await _context.MemberProfiles
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId == householdId &&
                    x.IsActive &&
                    !string.IsNullOrWhiteSpace(x.Email) &&
                    x.ReceiveEmailNotification &&
                    x.ReceiveExpiryNotification)
                .Select(x => x.Email!)
                .Distinct()
                .ToListAsync(cancellationToken);

            _logger.LogInformation("[Notification] Expiry recipients found: {Count} — {Emails}",
                recipients.Count, string.Join(", ", recipients));

            return recipients;
        }

        public async Task<List<string>> GetRecipientsForOutOfStockAsync(int householdId, CancellationToken cancellationToken = default)
        {
            var settings = await _context.HouseholdSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.HouseholdId == householdId, cancellationToken);

            if (settings == null)
            {
                _logger.LogWarning("[Notification] HouseholdSettings not found for HouseholdId={HouseholdId}", householdId);
                return new List<string>();
            }

            _logger.LogDebug("[Notification] HouseholdSettings: EnableEmail={EnableEmail}, EnableOutOfStock={EnableOutOfStock}",
                settings.EnableEmailNotifications, settings.EnableOutOfStockNotifications);

            if (!settings.EnableEmailNotifications || !settings.EnableOutOfStockNotifications)
            {
                _logger.LogInformation("[Notification] OutOfStock email skipped — email or out-of-stock notifications disabled in household settings.");
                return new List<string>();
            }

            var recipients = await _context.MemberProfiles
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId == householdId &&
                    x.IsActive &&
                    !string.IsNullOrWhiteSpace(x.Email) &&
                    x.ReceiveEmailNotification &&
                    x.ReceiveOutOfStockNotification)
                .Select(x => x.Email!)
                .Distinct()
                .ToListAsync(cancellationToken);

            _logger.LogInformation("[Notification] OutOfStock recipients found: {Count} — {Emails}",
                recipients.Count, string.Join(", ", recipients));

            return recipients;
        }
    }
}
