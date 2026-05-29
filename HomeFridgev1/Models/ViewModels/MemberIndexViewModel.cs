using HomeFridgev1.Models.Enums;

namespace HomeFridgev1.Models.ViewModels
{
    public class MemberIndexViewModel
    {
        public string HouseholdName { get; set; } = string.Empty;

        public int TotalMembers { get; set; }

        public int ActiveMembers { get; set; }

        public string? OwnerName { get; set; }

        public int? CurrentMemberId { get; set; }

        public List<MemberIndexItemViewModel> Items { get; set; } = new();
    }

    public class MemberIndexItemViewModel
    {
        public int Id { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string? AvatarPath { get; set; }

        public MemberRole Role { get; set; }

        public string? Email { get; set; }

        public bool IsActive { get; set; }

        public bool ReceiveEmailNotification { get; set; }

        public bool ReceiveExpiryNotification { get; set; }

        public bool ReceiveOutOfStockNotification { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsCurrentMember { get; set; }
    }
}
