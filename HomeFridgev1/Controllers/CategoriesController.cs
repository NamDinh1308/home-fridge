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
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IHouseholdInitializationService _householdInitializationService;

        public CategoriesController(
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

            var categories = await _context.Categories
                .Where(c => c.HouseholdId == household.Id)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return View(categories);
        }

        public async Task<IActionResult> Create()
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var model = new CategoryFormViewModel
            {
                IsActive = true,
                SortOrder = 0
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryFormViewModel model)
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            if (await NameExistsAsync(household.Id, model.Name))
            {
                ModelState.AddModelError(nameof(model.Name), "Tên danh mục đã tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var category = new Category
            {
                HouseholdId = household.Id,
                Name = model.Name.Trim(),
                Description = model.Description?.Trim(),
                SortOrder = model.SortOrder,
                IsActive = model.IsActive,
                IsDefault = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã thêm danh mục mới thành công.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id && c.HouseholdId == household.Id);

            if (category == null)
            {
                return NotFound();
            }

            var model = new CategoryFormViewModel
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                SortOrder = category.SortOrder,
                IsActive = category.IsActive
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CategoryFormViewModel model)
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == model.Id && c.HouseholdId == household.Id);

            if (category == null)
            {
                return NotFound();
            }

            if (await NameExistsAsync(household.Id, model.Name, model.Id))
            {
                ModelState.AddModelError(nameof(model.Name), "Tên danh mục đã tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            category.Name = model.Name.Trim();
            category.Description = model.Description?.Trim();
            category.SortOrder = model.SortOrder;
            category.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã cập nhật thông tin danh mục.";
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

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id && c.HouseholdId == household.Id);

            if (category == null)
            {
                return NotFound();
            }

            category.IsActive = !category.IsActive;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã thay đổi trạng thái hoạt động của danh mục: {category.Name}.";
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

            return await _context.Categories.AnyAsync(c =>
                c.HouseholdId == householdId &&
                c.Name.ToLower() == normalized &&
                (!ignoreId.HasValue || c.Id != ignoreId.Value));
        }
    }
}
