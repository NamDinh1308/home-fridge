using System.ComponentModel.DataAnnotations;

namespace HomeFridgev1.Models.ViewModels
{
    public class CategoryFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên danh mục.")]
        [StringLength(100, ErrorMessage = "Tên danh mục không được vượt quá 100 ký tự.")]
        [Display(Name = "Tên danh mục")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập thứ tự sắp xếp.")]
        [Range(0, 10000, ErrorMessage = "Thứ tự sắp xếp phải từ 0 đến 10000.")]
        [Display(Name = "Thứ tự sắp xếp")]
        public int SortOrder { get; set; } = 0;

        [Display(Name = "Trạng thái hoạt động")]
        public bool IsActive { get; set; } = true;
    }
}
