namespace HomeFridgev1.Models.Entities
{
    public class StorageLocation
    {
        public int Id { get; set; }

        public int HouseholdId { get; set; }
        public Household? Household { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int SortOrder { get; set; } = 0;

        public bool IsDefault { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<FoodItem> FoodItems { get; set; } = new List<FoodItem>();
    }
}