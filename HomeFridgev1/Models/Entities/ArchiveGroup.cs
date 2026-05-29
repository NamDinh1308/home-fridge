using System.ComponentModel.DataAnnotations;

namespace HomeFridgev1.Models.Entities
{
    public class ArchiveGroup
    {
        public int Id { get; set; }

        public int HouseholdId { get; set; }
        public Household? Household { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public int? CreatedByMemberId { get; set; }
        public MemberProfile? CreatedByMember { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<FoodItem> FoodItems { get; set; } = new List<FoodItem>();
    }
}
