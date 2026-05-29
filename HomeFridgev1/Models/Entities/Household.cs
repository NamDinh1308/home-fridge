using Microsoft.AspNetCore.Identity;

namespace HomeFridgev1.Models.Entities
{
    public class Household
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string OwnerUserId { get; set; } = string.Empty;

        public IdentityUser? OwnerUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public bool IsActive { get; set; } = true;

        public HouseholdSettings? Settings { get; set; }

        public ICollection<MemberProfile> MemberProfiles { get; set; } = new List<MemberProfile>();

        public ICollection<Category> Categories { get; set; } = new List<Category>();

        public ICollection<StorageLocation> StorageLocations { get; set; } = new List<StorageLocation>();

        public ICollection<FoodItem> FoodItems { get; set; } = new List<FoodItem>();

        public ICollection<NotificationLog> NotificationLogs { get; set; } = new List<NotificationLog>();

        public ICollection<ArchiveGroup> ArchiveGroups { get; set; } = new List<ArchiveGroup>();
    }
}