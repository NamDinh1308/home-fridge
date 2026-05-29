using HomeFridgev1.Data;
using HomeFridgev1.Models.Entities;
using HomeFridgev1.Models.ViewModels;
using HomeFridgev1.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeFridgev1.Controllers
{
    [Authorize]
    public class StorageLocationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IHouseholdInitializationService _householdInitializationService;

        public StorageLocationsController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            IHouseholdInitializationService householdInitializationService)
        {
            _context = context;
            _userManager = userManager;
            _householdInitializationService = householdInitializationService;
        }

        public async Task<IActionResult> Index()
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var locations = await _context.StorageLocations
                .Where(s => s.HouseholdId == household.Id)
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.Name)
                .ToListAsync();

            return View(locations);
        }

        public async Task<IActionResult> Create()
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var model = new StorageLocationFormViewModel
            {
                IsActive = true,
                SortOrder = 0
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StorageLocationFormViewModel model)
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            if (await NameExistsAsync(household.Id, model.Name))
            {
                ModelState.AddModelError(nameof(model.Name), "Tên vị trí lưu trữ đã tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var location = new StorageLocation
            {
                HouseholdId = household.Id,
                Name = model.Name.Trim(),
                Description = model.Description?.Trim(),
                SortOrder = model.SortOrder,
                IsActive = model.IsActive,
                IsDefault = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.StorageLocations.Add(location);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã thêm vị trí lưu trữ mới thành công.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var location = await _context.StorageLocations
                .FirstOrDefaultAsync(s => s.Id == id && s.HouseholdId == household.Id);

            if (location == null)
            {
                return NotFound();
            }

            var model = new StorageLocationFormViewModel
            {
                Id = location.Id,
                Name = location.Name,
                Description = location.Description,
                SortOrder = location.SortOrder,
                IsActive = location.IsActive
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(StorageLocationFormViewModel model)
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var location = await _context.StorageLocations
                .FirstOrDefaultAsync(s => s.Id == model.Id && s.HouseholdId == household.Id);

            if (location == null)
            {
                return NotFound();
            }

            if (await NameExistsAsync(household.Id, model.Name, model.Id))
            {
                ModelState.AddModelError(nameof(model.Name), "Tên vị trí lưu trữ đã tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            location.Name = model.Name.Trim();
            location.Description = model.Description?.Trim();
            location.SortOrder = model.SortOrder;
            location.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã cập nhật thông tin vị trí lưu trữ.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var location = await _context.StorageLocations
                .FirstOrDefaultAsync(s => s.Id == id && s.HouseholdId == household.Id);

            if (location == null)
            {
                return NotFound();
            }

            location.IsActive = !location.IsActive;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã thay đổi trạng thái hoạt động của vị trí lưu trữ: {location.Name}.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<Household?> GetCurrentHouseholdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return null;
            }

            await _householdInitializationService.EnsureInitializedAsync(
                user,
                $"Nhà của {user.Email}",
                user.Email ?? "Chủ gia đình");

            return await _context.Households.FirstOrDefaultAsync(h => h.OwnerUserId == user.Id);
        }

        private async Task<bool> NameExistsAsync(int householdId, string name, int? ignoreId = null)
        {
            var normalized = name.Trim().ToLower();

            return await _context.StorageLocations.AnyAsync(s =>
                s.HouseholdId == householdId &&
                s.Name.ToLower() == normalized &&
                (!ignoreId.HasValue || s.Id != ignoreId.Value));
        }
    }
}
