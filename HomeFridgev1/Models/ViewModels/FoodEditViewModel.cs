using HomeFridgev1.Models.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace HomeFridgev1.Models.ViewModels
{
    public class FoodEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int CategoryId { get; set; }

        [Required]
        public int StorageLocationId { get; set; }

        [Required]
        [StringLength(50)]
        public string Unit { get; set; } = string.Empty;

        [Range(typeof(decimal), "0", "999999999")]
        public decimal MinQuantityThreshold { get; set; } = 0;

        [DataType(DataType.Date)]
        public DateTime ExpiryDate { get; set; }

        public string? Notes { get; set; }

        public string? SelectedImagePath { get; set; }

        public List<SelectListItem> Categories { get; set; } = new();
        public List<SelectListItem> StorageLocations { get; set; } = new();
        public List<FoodImageOptionViewModel> ImageLibrary { get; set; } = new();
    }
}