using HomeFridgev1.Models.Enums;

namespace HomeFridgev1.Models.ViewModels
{
    public class DashboardViewModel
    {
        public bool IsAuthenticated { get; set; }

        public string? UserEmail { get; set; }
        public string? HouseholdName { get; set; }

        public int ActiveFoodCount { get; set; }
        public int ExpiringSoonCount { get; set; }
        public int OutOfStockCount { get; set; }
        public int ArchivedCount { get; set; }
        
        public int NormalCount { get; set; }
        public int WarningCount { get; set; }
        public int UrgentCount { get; set; }
        public int ExpiredCount { get; set; }

        public List<DashboardAlertItemViewModel> PriorityItems { get; set; } = new();
        public List<DashboardRecentActivityItemViewModel> RecentActivities { get; set; } = new();
        
        public List<CategoryDistributionViewModel> CategoryDistribution { get; set; } = new();
        public List<WasteAnalysisDayViewModel> WasteAnalysis { get; set; } = new();
        public List<WasteAnalysisDayViewModel> WasteAnalysisMonthly { get; set; } = new();
        public List<MemberActivitySummaryViewModel> MemberActivities { get; set; } = new();
    }

    public class DashboardAlertItemViewModel
    {
        public int FoodId { get; set; }
        public string FoodName { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public decimal CurrentQuantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public FoodStatus Status { get; set; }
    }

    public class DashboardRecentActivityItemViewModel
    {
        public int FoodItemId { get; set; }
        public string FoodName { get; set; } = string.Empty;
        public string? FoodImagePath { get; set; }

        public FoodActivityType ActivityType { get; set; }
        public decimal QuantityDelta { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Reason { get; set; }
    }

    public class CategoryDistributionViewModel
    {
        public string CategoryName { get; set; } = string.Empty;
        public int FoodItemCount { get; set; }
        public decimal TotalQuantity { get; set; }
    }

    public class WasteAnalysisDayViewModel
    {
        public string DateStr { get; set; } = string.Empty;
        public decimal ConsumedQuantity { get; set; }
        public decimal DiscardedQuantity { get; set; }
    }

    public class MemberActivitySummaryViewModel
    {
        public string MemberName { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
        public int ActivityCount { get; set; }
    }
}