using HomeFridgev1.Models.Enums;

namespace HomeFridgev1.Models.ViewModels
{
    public class FoodActivityHistoryItemViewModel
    {
        public int Id { get; set; }

        public int FoodItemId { get; set; }
        public string FoodName { get; set; } = string.Empty;
        public string? FoodImagePath { get; set; }

        public int MemberProfileId { get; set; }
        public string MemberName { get; set; } = string.Empty;

        public FoodActivityType ActivityType { get; set; }

        public decimal QuantityDelta { get; set; }
        public decimal QuantityBefore { get; set; }
        public decimal QuantityAfter { get; set; }

        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}