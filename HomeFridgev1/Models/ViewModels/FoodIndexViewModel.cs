using HomeFridgev1.Models.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HomeFridgev1.Models.ViewModels
{
    public class FoodIndexViewModel
    {
        public List<FoodItem> Items { get; set; } = new();

        public string? SearchTerm { get; set; }
        public int? CategoryId { get; set; }
        public int? StorageLocationId { get; set; }
        public string? Status { get; set; }
        public string? SortBy { get; set; }

        public List<SelectListItem> Categories { get; set; } = new();
        public List<SelectListItem> StorageLocations { get; set; } = new();
        public List<SelectListItem> Statuses { get; set; } = new();
        public List<SelectListItem> SortOptions { get; set; } = new();
    }
}