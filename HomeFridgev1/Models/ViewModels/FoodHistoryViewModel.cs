using Microsoft.AspNetCore.Mvc.Rendering;

namespace HomeFridgev1.Models.ViewModels
{
    public class FoodHistoryViewModel
    {
        public string? SearchTerm { get; set; }
        public string? ActivityType { get; set; }

        public List<SelectListItem> ActivityTypeOptions { get; set; } = new();

        public List<FoodActivityHistoryItemViewModel> Items { get; set; } = new();
    }
}