namespace HomeFridgev1.Models.Entities
{
    public class HouseholdSettings
    {
        public int Id { get; set; }

        public int HouseholdId { get; set; }

        public Household? Household { get; set; }

        public int UrgentDaysBeforeExpiry { get; set; } = 1;

        public int WarningDaysBeforeExpiry { get; set; } = 3;

        public bool EnableEmailNotifications { get; set; } = true;

        public bool EnableOutOfStockNotifications { get; set; } = true;

        public bool EnableExpiryNotifications { get; set; } = true;

        public bool EnableAutoHideOutOfStock { get; set; } = false;

        public int OutOfStockAutoHideDays { get; set; } = 0;

        public string DailyCheckTime { get; set; } = "08:00";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}