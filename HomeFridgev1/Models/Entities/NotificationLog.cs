namespace HomeFridgev1.Models.Entities
{
    public class NotificationLog
    {
        public int Id { get; set; }

        public int HouseholdId { get; set; }
        public Household? Household { get; set; }

        public int FoodItemId { get; set; }
        public FoodItem? FoodItem { get; set; }

        public int? RecipientMemberId { get; set; }
        public MemberProfile? RecipientMember { get; set; }

        public string Channel { get; set; } = "Email";

        public string EventType { get; set; } = "StatusChanged";

        public string? OldStatus { get; set; }

        public string? NewStatus { get; set; }

        public string? Subject { get; set; }

        public string? Message { get; set; }

        public bool IsSuccess { get; set; } = false;

        public DateTime? SentAt { get; set; }

        public string? ErrorMessage { get; set; }

        public string? UniqueEventKey { get; set; }
    }
}