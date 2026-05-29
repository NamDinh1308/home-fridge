using System.Text;
using HomeFridgev1.Models.Entities;
using HomeFridgev1.Models.Enums;
using HomeFridgev1.Services.Interfaces;

namespace HomeFridgev1.Services.Notification
{
    public class FoodStatusNotificationService : IFoodStatusNotificationService
    {
        private readonly INotificationRecipientResolver _recipientResolver;
        private readonly IEmailNotificationService _emailNotificationService;
        private readonly ILogger<FoodStatusNotificationService> _logger;
        private readonly IWebHostEnvironment _env;

        // Đổi thành domain thật khi deploy
        private const string AppBaseUrl = "http://localhost:5033";
        private const string BannerCid  = "banner-homefridge";

        public FoodStatusNotificationService(
            INotificationRecipientResolver recipientResolver,
            IEmailNotificationService emailNotificationService,
            ILogger<FoodStatusNotificationService> logger,
            IWebHostEnvironment env)
        {
            _recipientResolver = recipientResolver;
            _emailNotificationService = emailNotificationService;
            _logger = logger;
            _env = env;
        }

        // Tải ảnh banner và đóng gói để attach inline
        private EmailInlineImage? LoadBannerAttachment()
        {
            try
            {
                var path = Path.Combine(_env.WebRootPath, "assets", "Banner", "banner-email.jpg");
                if (!File.Exists(path)) return null;
                return new EmailInlineImage
                {
                    ContentId   = BannerCid,
                    Filename    = "banner-email.jpg",
                    Data        = File.ReadAllBytes(path),
                    ContentType = "image/jpeg"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load banner image.");
                return null;
            }
        }

        public async Task NotifyStatusTransitionAsync(
            FoodItem foodItem,
            FoodStatus oldStatus,
            FoodStatus newStatus,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "[Notification] Status transition check: FoodItem={FoodItemId} ({Name}), Old={OldStatus}, New={NewStatus}",
                foodItem.Id, foodItem.Name, oldStatus, newStatus);

            if (oldStatus == newStatus)
            {
                _logger.LogInformation("[Notification] Skipped — status unchanged ({Status}).", newStatus);
                return;
            }

            // We no longer attach the image directly to avoid the attachment chip in Gmail.
            // A public URL is used in the HTML template instead.
            var inlineImages = Array.Empty<EmailInlineImage>();
            bool hasBanner = true; // We assume the public banner is always available

            if (newStatus == FoodStatus.Warning || newStatus == FoodStatus.Urgent || newStatus == FoodStatus.Expired)
            {
                var recipients = await _recipientResolver.GetRecipientsForExpiryAsync(foodItem.HouseholdId, cancellationToken);
                if (!recipients.Any())
                {
                    _logger.LogInformation("[Notification] No expiry recipients — email not sent for FoodItem={FoodItemId}.", foodItem.Id);
                    return;
                }

                var subject = GetExpirySubject(foodItem, newStatus);
                var html    = BuildExpiryHtml(foodItem, newStatus, hasBanner);
                var text    = BuildExpiryText(foodItem, newStatus);

                await _emailNotificationService.SendAsync(recipients, subject, html, text, inlineImages, cancellationToken);
                _logger.LogInformation("Sent expiry email for food item {FoodItemId}.", foodItem.Id);
                return;
            }

            if (newStatus == FoodStatus.OutOfStock)
            {
                var recipients = await _recipientResolver.GetRecipientsForOutOfStockAsync(foodItem.HouseholdId, cancellationToken);
                if (!recipients.Any())
                {
                    _logger.LogInformation("[Notification] No out-of-stock recipients — email not sent for FoodItem={FoodItemId}.", foodItem.Id);
                    return;
                }

                var subject = $"[Home's Fridge] Thực phẩm đã hết hàng: {foodItem.Name}";
                var html    = BuildOutOfStockHtml(foodItem, hasBanner);
                var text    = BuildOutOfStockText(foodItem);

                await _emailNotificationService.SendAsync(recipients, subject, html, text, inlineImages, cancellationToken);
                _logger.LogInformation("Sent out-of-stock email for food item {FoodItemId}.", foodItem.Id);
            }
        }

        // ────────────────────────────────────────────────
        // SUBJECT HELPERS
        // ────────────────────────────────────────────────

        private static string GetExpirySubject(FoodItem foodItem, FoodStatus status) => status switch
        {
            FoodStatus.Warning => $"[Home's Fridge] ⚠️ Cảnh báo: {foodItem.Name} sắp hết hạn",
            FoodStatus.Urgent  => $"[Home's Fridge] 🚨 Cấp bách: {foodItem.Name} sắp hết hạn",
            FoodStatus.Expired => $"[Home's Fridge] ❌ Đã hết hạn: {foodItem.Name}",
            _                  => $"[Home's Fridge] Thông báo hạn sử dụng: {foodItem.Name}"
        };

        // ────────────────────────────────────────────────
        // HTML TEMPLATES (dùng cid: để nhúng ảnh inline)
        // ────────────────────────────────────────────────

        private static string BuildExpiryHtml(FoodItem foodItem, FoodStatus status, bool hasBanner)
        {
            var (accentColor, badgeBg, badgeText, icon, headline, description) = status switch
            {
                FoodStatus.Warning => (
                    "#f59e0b", "#fef3c7", "#92400e", "⚠️",
                    "Cảnh báo hạn sử dụng",
                    "Thực phẩm đang tiến gần đến ngày hết hạn. Hãy sử dụng sớm hoặc lên kế hoạch trước."
                ),
                FoodStatus.Urgent => (
                    "#ef4444", "#fee2e2", "#991b1b", "🚨",
                    "Cấp bách — Sắp hết hạn!",
                    "Thực phẩm chỉ còn rất ít thời gian trước khi hết hạn. Vui lòng xử lý ngay hôm nay."
                ),
                FoodStatus.Expired => (
                    "#7f1d1d", "#fecaca", "#7f1d1d", "❌",
                    "Thực phẩm đã hết hạn",
                    "Thực phẩm này đã quá ngày hết hạn. Vui lòng kiểm tra và xử lý ngay để đảm bảo an toàn."
                ),
                _ => (
                    "#6b7280", "#f3f4f6", "#374151", "ℹ️",
                    "Thông báo hạn sử dụng",
                    "Có thay đổi về trạng thái hạn sử dụng của thực phẩm."
                )
            };

            // Sử dụng ảnh public URL (đã upload ảnh thật của project lên cloud) để Gmail không hiện file đính kèm
            var bannerUrl = "https://files.catbox.moe/5xy3fe.jpg";
            var bannerRow = hasBanner
                ? $"""<tr><td style="padding:0;line-height:0;"><img src="{bannerUrl}" alt="Home's Fridge" width="600" style="width:100%;max-width:600px;display:block;border:0;" /></td></tr>"""
                : "";

            return $"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
              <meta charset="UTF-8"/>
              <meta name="viewport" content="width=device-width,initial-scale=1.0"/>
              <title>{headline}</title>
            </head>
            <body style="margin:0;padding:0;background:#ffffff;font-family:'Segoe UI',Arial,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#ffffff;padding:32px 0;">
                <tr><td align="center">
                  <table width="600" cellpadding="0" cellspacing="0"
                         style="max-width:600px;width:100%;background:#ffffff;border-radius:16px;
                                overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);">

                    {bannerRow}
                    <tr><td style="background:{accentColor};height:4px;font-size:0;line-height:0;">&nbsp;</td></tr>

                    <tr><td style="padding:32px 40px 24px;">
                      <div style="display:inline-block;background:{badgeBg};color:{badgeText};
                                  font-size:13px;font-weight:700;padding:6px 14px;border-radius:20px;
                                  margin-bottom:16px;">{icon} {headline}</div>

                      <p style="margin:0 0 8px;font-size:15px;color:#374151;">{description}</p>

                      <table width="100%" cellpadding="0" cellspacing="0"
                             style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:12px;
                                    margin:24px 0;border-left:4px solid {accentColor};">
                        <tr><td style="padding:20px 24px;">
                          <p style="margin:0 0 14px;font-size:18px;font-weight:700;color:#0f172a;">🍽️ {foodItem.Name}</p>
                          <table width="100%" cellpadding="0" cellspacing="0" style="margin-top: 16px;">
                            <tr>
                              <td width="33%" valign="top" style="padding-right:12px;">
                                <p style="margin:0 0 6px;font-size:12px;color:#64748b;text-transform:uppercase;letter-spacing:0.5px;">📦 Số lượng</p>
                                <p style="margin:0;font-size:15px;font-weight:600;color:#0f172a;">{foodItem.CurrentQuantity:0.##} {foodItem.Unit}</p>
                              </td>
                              <td width="33%" valign="top" style="padding:0 12px;border-left:1px solid #e2e8f0;">
                                <p style="margin:0 0 6px;font-size:12px;color:#64748b;text-transform:uppercase;letter-spacing:0.5px;">📅 Hạn sử dụng</p>
                                <p style="margin:0;font-size:15px;font-weight:600;color:{accentColor};">{foodItem.ExpiryDate:dd/MM/yyyy}</p>
                              </td>
                              <td width="33%" valign="top" style="padding-left:12px;border-left:1px solid #e2e8f0;">
                                <p style="margin:0 0 8px;font-size:12px;color:#64748b;text-transform:uppercase;letter-spacing:0.5px;">🔴 Trạng thái</p>
                                <div><span style="background:{badgeBg};color:{badgeText};font-size:12px;font-weight:700;padding:4px 10px;border-radius:12px;display:inline-block;white-space:nowrap;">{status}</span></div>
                              </td>
                            </tr>
                          </table>
                        </td></tr>
                      </table>

                      <table cellpadding="0" cellspacing="0" style="margin:8px 0 24px;">
                        <tr>
                          <td style="border-radius:10px;background:{accentColor};">
                            <a href="{AppBaseUrl}/Food"
                               style="display:inline-block;padding:12px 28px;font-size:14px;
                                      font-weight:700;color:#ffffff;text-decoration:none;border-radius:10px;">
                              Mở Home's Fridge →
                            </a>
                          </td>
                        </tr>
                      </table>
                    </td></tr>

                    {BuildFooterHtml()}
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
        }

        private static string BuildOutOfStockHtml(FoodItem foodItem, bool hasBanner)
        {
            const string accentColor = "#3b82f6";
            const string badgeBg    = "#dbeafe";
            const string badgeText  = "#1e40af";

            var bannerUrl = "https://files.catbox.moe/5xy3fe.jpg";
            var bannerRow = hasBanner
                ? $"""<tr><td style="padding:0;line-height:0;"><img src="{bannerUrl}" alt="Home's Fridge" width="600" style="width:100%;max-width:600px;display:block;border:0;" /></td></tr>"""
                : "";

            return $"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
              <meta charset="UTF-8"/>
              <meta name="viewport" content="width=device-width,initial-scale=1.0"/>
              <title>Thực phẩm đã hết hàng</title>
            </head>
            <body style="margin:0;padding:0;background:#ffffff;font-family:'Segoe UI',Arial,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#ffffff;padding:32px 0;">
                <tr><td align="center">
                  <table width="600" cellpadding="0" cellspacing="0"
                         style="max-width:600px;width:100%;background:#ffffff;border-radius:16px;
                                overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);">

                    {bannerRow}
                    <tr><td style="background:{accentColor};height:4px;font-size:0;line-height:0;">&nbsp;</td></tr>

                    <tr><td style="padding:32px 40px 24px;">
                      <div style="display:inline-block;background:{badgeBg};color:{badgeText};
                                  font-size:13px;font-weight:700;padding:6px 14px;border-radius:20px;
                                  margin-bottom:16px;">📦 Thực phẩm đã hết hàng</div>

                      <p style="margin:0 0 8px;font-size:15px;color:#374151;">
                        Một thực phẩm trong tủ lạnh của bạn đã hết. Bạn có thể bổ sung thêm khi cần thiết.
                      </p>

                      <table width="100%" cellpadding="0" cellspacing="0"
                             style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:12px;
                                    margin:24px 0;border-left:4px solid {accentColor};">
                        <tr><td style="padding:20px 24px;">
                          <p style="margin:0 0 14px;font-size:18px;font-weight:700;color:#0f172a;">🍽️ {foodItem.Name}</p>
                          <table width="100%" cellpadding="0" cellspacing="0" style="margin-top: 16px;">
                            <tr>
                              <td width="33%" valign="top" style="padding-right:12px;">
                                <p style="margin:0 0 6px;font-size:12px;color:#64748b;text-transform:uppercase;letter-spacing:0.5px;">📦 Số lượng</p>
                                <p style="margin:0;font-size:15px;font-weight:600;color:#ef4444;">0 {foodItem.Unit}</p>
                              </td>
                              <td width="33%" valign="top" style="padding:0 12px;border-left:1px solid #e2e8f0;">
                                <p style="margin:0 0 6px;font-size:12px;color:#64748b;text-transform:uppercase;letter-spacing:0.5px;">📅 Hạn sử dụng</p>
                                <p style="margin:0;font-size:15px;font-weight:600;color:#0f172a;">{foodItem.ExpiryDate:dd/MM/yyyy}</p>
                              </td>
                              <td width="33%" valign="top" style="padding-left:12px;border-left:1px solid #e2e8f0;">
                                <p style="margin:0 0 8px;font-size:12px;color:#64748b;text-transform:uppercase;letter-spacing:0.5px;">🔵 Trạng thái</p>
                                <div><span style="background:{badgeBg};color:{badgeText};font-size:12px;font-weight:700;padding:4px 10px;border-radius:12px;display:inline-block;white-space:nowrap;">Hết hàng</span></div>
                              </td>
                            </tr>
                          </table>
                        </td></tr>
                      </table>

                      <table cellpadding="0" cellspacing="0" style="margin:8px 0 24px;">
                        <tr>
                          <td style="border-radius:10px;background:{accentColor};">
                            <a href="{AppBaseUrl}/Food"
                               style="display:inline-block;padding:12px 28px;font-size:14px;
                                      font-weight:700;color:#ffffff;text-decoration:none;border-radius:10px;">
                              Bổ sung thực phẩm →
                            </a>
                          </td>
                        </tr>
                      </table>
                    </td></tr>

                    {BuildFooterHtml()}
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
        }

        private static string BuildFooterHtml() => $"""
            <tr>
              <td style="background:#f8fafc;border-top:1px solid #e2e8f0;padding:20px 40px;text-align:center;">
                <p style="margin:0 0 6px;font-size:13px;color:#94a3b8;">
                  Email này được gửi tự động từ hệ thống
                  <a href="{AppBaseUrl}" style="color:#16a34a;text-decoration:none;font-weight:600;">Home's Fridge</a>.
                </p>
                <p style="margin:0;font-size:12px;color:#cbd5e1;">
                  © 2025 Home's Fridge — Quản lý thực phẩm thông minh
                  &nbsp;|&nbsp;
                  <a href="{AppBaseUrl}/Settings" style="color:#94a3b8;text-decoration:none;">Cài đặt thông báo</a>
                </p>
              </td>
            </tr>
            """;

        // ────────────────────────────────────────────────
        // PLAIN TEXT FALLBACKS
        // ────────────────────────────────────────────────

        private static string BuildExpiryText(FoodItem foodItem, FoodStatus status)
        {
            var sb = new StringBuilder();
            sb.AppendLine("HOME'S FRIDGE — Thông báo hạn sử dụng");
            sb.AppendLine(new string('─', 40));
            sb.AppendLine(status switch
            {
                FoodStatus.Warning => "⚠️  CẢNH BÁO: Thực phẩm sắp hết hạn",
                FoodStatus.Urgent  => "🚨  CẤP BÁCH: Thực phẩm sắp hết hạn",
                FoodStatus.Expired => "❌  HẾT HẠN: Thực phẩm đã quá hạn",
                _                  => "ℹ️  Thông báo hạn sử dụng"
            });
            sb.AppendLine();
            sb.AppendLine($"Thực phẩm    : {foodItem.Name}");
            sb.AppendLine($"Số lượng     : {foodItem.CurrentQuantity:0.##} {foodItem.Unit}");
            sb.AppendLine($"Hạn sử dụng  : {foodItem.ExpiryDate:dd/MM/yyyy}");
            sb.AppendLine($"Trạng thái   : {status}");
            sb.AppendLine();
            sb.AppendLine($"Mở ứng dụng: {AppBaseUrl}/Food");
            return sb.ToString();
        }

        private static string BuildOutOfStockText(FoodItem foodItem)
        {
            var sb = new StringBuilder();
            sb.AppendLine("HOME'S FRIDGE — Thông báo hết hàng");
            sb.AppendLine(new string('─', 40));
            sb.AppendLine("📦  Thực phẩm đã hết hàng");
            sb.AppendLine();
            sb.AppendLine($"Thực phẩm    : {foodItem.Name}");
            sb.AppendLine($"Số lượng     : 0 {foodItem.Unit}");
            sb.AppendLine($"Hạn sử dụng  : {foodItem.ExpiryDate:dd/MM/yyyy}");
            sb.AppendLine($"Trạng thái   : Hết hàng (OutOfStock)");
            sb.AppendLine();
            sb.AppendLine($"Bổ sung tại: {AppBaseUrl}/Food");
            return sb.ToString();
        }
    }
}
