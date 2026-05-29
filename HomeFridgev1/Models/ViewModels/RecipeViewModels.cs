using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HomeFridgev1.Models.Entities;

namespace HomeFridgev1.Models.ViewModels
{
    public class RecipeListViewModel
    {
        public List<Recipe> Recipes { get; set; } = new();
    }

    public class RecipeFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên món ăn")]
        [Display(Name = "Tên món ăn")]
        [MaxLength(200, ErrorMessage = "Tên món ăn không được dài quá 200 ký tự")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Mô tả ngắn")]
        [MaxLength(500, ErrorMessage = "Mô tả không được dài quá 500 ký tự")]
        public string? Description { get; set; }

        [Display(Name = "Các bước thực hiện")]
        public string? Instructions { get; set; }

        [Display(Name = "Đường dẫn hình ảnh")]
        public string? ImagePath { get; set; }

        public List<RecipeIngredientFormModel> Ingredients { get; set; } = new();
    }

    public class RecipeIngredientFormModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên nguyên liệu")]
        public string FoodName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số lượng")]
        [Range(0.01, 999999, ErrorMessage = "Số lượng phải lớn hơn 0")]
        public decimal RequiredQuantity { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập đơn vị")]
        public string Unit { get; set; } = string.Empty;
    }

    public class RecipeSuggestionViewModel
    {
        public int RecipeId { get; set; }
        public string RecipeName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImagePath { get; set; }
        public int MatchPercentage { get; set; } // % số nguyên liệu đáp ứng được
        public bool IsCookable { get; set; } // Có thể nấu ngay (đủ tất cả nguyên liệu)
        public bool HasExpiringIngredients { get; set; } // Có nguyên liệu nào sắp hết hạn trong tủ lạnh không
        public List<RecipeIngredientStatusViewModel> IngredientsStatus { get; set; } = new();
    }

    public class RecipeIngredientStatusViewModel
    {
        public string FoodName { get; set; } = string.Empty;
        public decimal RequiredQuantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal AvailableQuantity { get; set; } // Số lượng đang có trong tủ lạnh
        public string Status { get; set; } = "Missing"; // "Match", "Partial", "Missing"
    }
}
