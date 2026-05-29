using HomeFridgev1.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace HomeFridgev1.Models.ViewModels
{
    public class MemberFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên thành viên.")]
        [StringLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? AvatarPath { get; set; }

        public Microsoft.AspNetCore.Http.IFormFile? AvatarFile { get; set; }

        [Required]
        public MemberRole Role { get; set; } = MemberRole.Member;

        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(256)]
        public string? Email { get; set; }

        public bool ReceiveEmailNotification { get; set; } = true;

        public bool ReceiveExpiryNotification { get; set; } = true;

        public bool ReceiveOutOfStockNotification { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public bool IsOwner { get; set; }

        public List<SelectListItem> RoleOptions { get; set; } = new();
    }
}
