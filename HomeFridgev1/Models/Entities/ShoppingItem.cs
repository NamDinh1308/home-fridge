using System;

namespace HomeFridgev1.Models.Entities
{
    public class ShoppingItem
    {
        public int Id { get; set; }

        public int HouseholdId { get; set; }
        public Household? Household { get; set; }

        public int? FoodItemId { get; set; }
        public FoodItem? FoodItem { get; set; }

        public string Name { get; set; } = string.Empty;

        public int? CategoryId { get; set; }
        public Category? Category { get; set; }

        public decimal Quantity { get; set; } = 1;

        public string Unit { get; set; } = string.Empty;

        public bool IsPurchased { get; set; } = false;

        public int? AddedByMemberId { get; set; }
        public MemberProfile? AddedByMember { get; set; }

        public int? PurchasedByMemberId { get; set; }
        public MemberProfile? PurchasedByMember { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? PurchasedAt { get; set; }
    }
}
