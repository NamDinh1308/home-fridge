using System.ComponentModel.DataAnnotations;

namespace HomeFridgev1.Models.ViewModels
{
    public class FoodQuantityActionViewModel
    {
        public int Id { get; set; }

        public string ActionType { get; set; } = string.Empty;

        public string FoodName { get; set; } = string.Empty;

        public string? ImagePath { get; set; }

        public decimal CurrentQuantity { get; set; }

        public string Unit { get; set; } = string.Empty;

        public DateTime ExpiryDate { get; set; }

        public string CurrentStatus { get; set; } = string.Empty;

        public string? Source { get; set; }

        [Range(typeof(decimal), "0.01", "999999999", ErrorMessage = "Số lượng phải lớn hơn 0.")]
        public decimal Quantity { get; set; }

        [StringLength(500)]
        public string? Reason { get; set; }
    }
}
