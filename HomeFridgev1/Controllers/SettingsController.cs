using HomeFridgev1.Data;
using HomeFridgev1.Models.ViewModels;
using HomeFridgev1.Models.Enums;
using HomeFridgev1.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeFridgev1.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ICurrentMemberService _currentMemberService;
        private readonly IFoodStatusService _foodStatusService;

        public SettingsController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ICurrentMemberService currentMemberService,
            IFoodStatusService foodStatusService)
        {
            _context = context;
            _userManager = userManager;
            _currentMemberService = currentMemberService;
            _foodStatusService = foodStatusService;
        }

        public async Task<IActionResult> Index()
        {
            var household = await GetCurrentHouseholdWithSettingsAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var accessRedirect = await ValidateOwnerAccessAsync(household.Id);
            if (accessRedirect != null)
            {
                return accessRedirect;
            }

            if (household.Settings == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy cấu hình gia đình. Vui lòng kiểm tra lại dữ liệu HouseholdSettings.";
                return RedirectToAction("Index", "Home");
            }

            var model = new HouseholdSettingsViewModel
            {
                HouseholdId = household.Id,
                HouseholdName = household.Name,
                Description = household.Description,
                WarningDaysBeforeExpiry = household.Settings.WarningDaysBeforeExpiry,
                UrgentDaysBeforeExpiry = household.Settings.UrgentDaysBeforeExpiry,
                EnableEmailNotifications = household.Settings.EnableEmailNotifications,
                EnableExpiryNotifications = household.Settings.EnableExpiryNotifications,
                EnableOutOfStockNotifications = household.Settings.EnableOutOfStockNotifications,
                EnableAutoHideOutOfStock = household.Settings.EnableAutoHideOutOfStock,
                OutOfStockAutoHideDays = household.Settings.OutOfStockAutoHideDays
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(HouseholdSettingsViewModel model)
        {
            var household = await GetCurrentHouseholdWithSettingsAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var accessRedirect = await ValidateOwnerAccessAsync(household.Id);
            if (accessRedirect != null)
            {
                return accessRedirect;
            }

            if (household.Settings == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy cấu hình gia đình. Vui lòng kiểm tra lại dữ liệu HouseholdSettings.";
                return RedirectToAction("Index", "Home");
            }

            if (model.UrgentDaysBeforeExpiry > model.WarningDaysBeforeExpiry)
            {
                ModelState.AddModelError(nameof(model.UrgentDaysBeforeExpiry), "Ngưỡng cấp bách phải nhỏ hơn hoặc bằng ngưỡng cảnh báo.");
            }

            if (!model.EnableAutoHideOutOfStock)
            {
                model.OutOfStockAutoHideDays = 0;
            }
            else if (model.OutOfStockAutoHideDays <= 0)
            {
                ModelState.AddModelError(nameof(model.OutOfStockAutoHideDays), "Số ngày tự ẩn phải lớn hơn 0 khi bật cơ chế tự ẩn.");
            }

            if (!ModelState.IsValid)
            {
                model.HouseholdName = household.Name;
                return View(model);
            }

            household.Name = model.HouseholdName.Trim();
            household.Description = string.IsNullOrWhiteSpace(model.Description)
                ? null
                : model.Description.Trim();

            household.Settings.WarningDaysBeforeExpiry = model.WarningDaysBeforeExpiry;
            household.Settings.UrgentDaysBeforeExpiry = model.UrgentDaysBeforeExpiry;
            household.Settings.EnableEmailNotifications = model.EnableEmailNotifications;
            household.Settings.EnableExpiryNotifications = model.EnableExpiryNotifications;
            household.Settings.EnableOutOfStockNotifications = model.EnableOutOfStockNotifications;
            household.Settings.EnableAutoHideOutOfStock = model.EnableAutoHideOutOfStock;
            household.Settings.OutOfStockAutoHideDays = model.EnableAutoHideOutOfStock
                ? model.OutOfStockAutoHideDays
                : 0;
            household.Settings.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Run the auto-hide rule immediately so changes are visible instantly
            await _foodStatusService.ApplyAutoHideRuleAsync(household.Id);

            TempData["SuccessMessage"] = "Đã cập nhật cài đặt gia đình.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<HomeFridgev1.Models.Entities.Household?> GetCurrentHouseholdWithSettingsAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return null;
            }

            return await _context.Households
                .Include(h => h.Settings)
                .FirstOrDefaultAsync(h => h.OwnerUserId == user.Id);
        }

        private async Task<IActionResult?> ValidateOwnerAccessAsync(int householdId)
        {
            var currentMemberId = _currentMemberService.GetCurrentMemberId();
            if (!currentMemberId.HasValue)
            {
                return RedirectToAction("Select", "Members", new { returnUrl = Request.Path + Request.QueryString });
            }

            var currentMember = await _context.MemberProfiles
                .FirstOrDefaultAsync(m => m.Id == currentMemberId.Value && m.HouseholdId == householdId);

            if (currentMember == null || currentMember.Role != MemberRole.Owner)
            {
                TempData["ErrorMessage"] = "Chỉ chủ hộ (Owner) mới có quyền truy cập cài đặt gia đình.";
                return RedirectToAction("Index", "Home");
            }

            return null; // Access granted
        }
    }
}
