using HomeFridgev1.Models.Enums;

namespace HomeFridgev1.Models.Entities
{
    public class FoodActivityLog
    {
        public int Id { get; set; }

        public int FoodItemId { get; set; }
        public FoodItem? FoodItem { get; set; }

        public int MemberProfileId { get; set; }
        public MemberProfile? MemberProfile { get; set; }

        public FoodActivityType ActivityType { get; set; }

        public decimal QuantityDelta { get; set; }

        public decimal QuantityBefore { get; set; }

        public decimal QuantityAfter { get; set; }

        public string? Reason { get; set; }

        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}