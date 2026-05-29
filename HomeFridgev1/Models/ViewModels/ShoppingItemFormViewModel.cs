using System.ComponentModel.DataAnnotations;

namespace HomeFridgev1.Models.ViewModels
{
    public class ShoppingItemFormViewModel
    {
        public int Id { get; set; }

        public int? FoodItemId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên mặt hàng.")]
        [StringLength(200, ErrorMessage = "Tên mặt hàng không được vượt quá 200 ký tự.")]
        [Display(Name = "Tên mặt hàng")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Danh mục")]
        public int? CategoryId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số lượng.")]
        [Range(0.01, 10000.0, ErrorMessage = "Số lượng phải từ 0.01 đến 10000.")]
        [Display(Name = "Số lượng")]
        public decimal Quantity { get; set; } = 1;

        [Required(ErrorMessage = "Vui lòng nhập đơn vị tính.")]
        [StringLength(50, ErrorMessage = "Đơn vị tính không được vượt quá 50 ký tự.")]
        [Display(Name = "Đơn vị")]
        public string Unit { get; set; } = string.Empty;
    }
}
