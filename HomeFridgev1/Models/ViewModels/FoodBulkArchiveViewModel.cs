using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HomeFridgev1.Models.ViewModels
{
    public class FoodBulkArchiveViewModel
    {
        public List<int> SelectedIds { get; set; } = new();

        public List<FoodBulkArchiveItemViewModel> Items { get; set; } = new();

        public int? ArchiveGroupId { get; set; }

        public bool CreateNewArchiveGroup { get; set; }

        [StringLength(100, ErrorMessage = "Tên vùng lưu trữ không được vượt quá 100 ký tự.")]
        public string? NewArchiveGroupName { get; set; }

        public List<SelectListItem> AvailableArchiveGroups { get; set; } = new();

        [StringLength(500)]
        public string? Reason { get; set; }
    }

    public class FoodBulkArchiveItemViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? ImagePath { get; set; }

        public string? CategoryName { get; set; }

        public string? StorageLocationName { get; set; }

        public decimal CurrentQuantity { get; set; }

        public string Unit { get; set; } = string.Empty;

        public string CurrentStatus { get; set; } = string.Empty;
    }
}
