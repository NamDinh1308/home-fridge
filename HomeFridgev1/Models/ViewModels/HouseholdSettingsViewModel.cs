using System.ComponentModel.DataAnnotations;

namespace HomeFridgev1.Models.ViewModels
{
    public class HouseholdSettingsViewModel
    {
        public int HouseholdId { get; set; }

        [Required(ErrorMessage = "Tên gia đình là bắt buộc.")]
        [StringLength(100)]
        [Display(Name = "Tên household")]
        public string HouseholdName { get; set; } = string.Empty;

        [StringLength(300)]
        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Range(1, 30, ErrorMessage = "Số ngày cảnh báo phải nằm trong khoảng từ 1 đến 30.")]
        [Display(Name = "Số ngày cảnh báo")]
        public int WarningDaysBeforeExpiry { get; set; }

        [Range(0, 30, ErrorMessage = "Số ngày cấp bách phải nằm trong khoảng từ 0 đến 30.")]
        [Display(Name = "Số ngày cấp bách")]
        public int UrgentDaysBeforeExpiry { get; set; }

        [Display(Name = "Bật thông báo email chung")]
        public bool EnableEmailNotifications { get; set; }

        [Display(Name = "Cảnh báo thực phẩm sắp hết hạn")]
        public bool EnableExpiryNotifications { get; set; }

        [Display(Name = "Cảnh báo thực phẩm hết hàng")]
        public bool EnableOutOfStockNotifications { get; set; }

        [Display(Name = "Tự ẩn món hết hàng khỏi danh sách")]
        public bool EnableAutoHideOutOfStock { get; set; }

        [Range(0, 30, ErrorMessage = "Số ngày tự ẩn phải nằm trong khoảng từ 0 đến 30.")]
        [Display(Name = "Số ngày tự ẩn")]
        public int OutOfStockAutoHideDays { get; set; }
    }
}
