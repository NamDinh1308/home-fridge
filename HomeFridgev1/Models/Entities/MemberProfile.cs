using HomeFridgev1.Models.Enums;

namespace HomeFridgev1.Models.Entities
{
    public class MemberProfile
    {
        public int Id { get; set; }

        public int HouseholdId { get; set; }
        public Household? Household { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string? AvatarPath { get; set; }

        public MemberRole Role { get; set; } = MemberRole.Member;

        public string? Email { get; set; }

        public bool ReceiveEmailNotification { get; set; } = true;

        public bool ReceiveExpiryNotification { get; set; } = true;

        public bool ReceiveOutOfStockNotification { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public ICollection<FoodActivityLog> FoodActivityLogs { get; set; } = new List<FoodActivityLog>();

        public ICollection<NotificationLog> NotificationLogs { get; set; } = new List<NotificationLog>();
    }
}