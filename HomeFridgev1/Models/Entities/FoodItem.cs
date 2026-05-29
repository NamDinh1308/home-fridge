using HomeFridgev1.Models.Enums;

namespace HomeFridgev1.Models.Entities
{
    public class FoodItem
    {
        public int Id { get; set; }

        public int HouseholdId { get; set; }
        public Household? Household { get; set; }

        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public int StorageLocationId { get; set; }
        public StorageLocation? StorageLocation { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? ImagePath { get; set; }

        public string Unit { get; set; } = string.Empty;

        public decimal CurrentQuantity { get; set; }

        public decimal MinQuantityThreshold { get; set; } = 0;

        public DateTime ExpiryDate { get; set; }

        public DateTime AddedDate { get; set; } = DateTime.UtcNow;

        public int AddedByMemberId { get; set; }
        public MemberProfile? AddedByMember { get; set; }

        public int? LastUpdatedByMemberId { get; set; }
        public MemberProfile? LastUpdatedByMember { get; set; }

        public FoodStatus CurrentStatus { get; set; } = FoodStatus.Normal;

        public DateTime? LastStatusChangedAt { get; set; }

        public string? Notes { get; set; }

        public bool IsArchived { get; set; } = false;
        public DateTime? OutOfStockSince { get; set; }

        public string? ArchivedReason { get; set; }

        public DateTime? ArchivedAt { get; set; }

        public int? ArchivedByMemberId { get; set; }
        public MemberProfile? ArchivedByMember { get; set; }

        public int? ArchiveGroupId { get; set; }
        public ArchiveGroup? ArchiveGroup { get; set; }

        public ICollection<FoodActivityLog> ActivityLogs { get; set; } = new List<FoodActivityLog>();

        public ICollection<NotificationLog> NotificationLogs { get; set; } = new List<NotificationLog>();
    }
}