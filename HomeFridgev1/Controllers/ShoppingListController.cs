using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HomeFridgev1.Data;
using HomeFridgev1.Models.Entities;
using HomeFridgev1.Models.Enums;
using HomeFridgev1.Models.ViewModels;
using HomeFridgev1.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HomeFridgev1.Controllers
{
    [Authorize]
    public class ShoppingListController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ICurrentMemberService _currentMemberService;
        private readonly IHouseholdInitializationService _householdInitializationService;
        private readonly IFoodStatusService _foodStatusService;
        private readonly IFoodStatusNotificationService _foodStatusNotificationService;

        public ShoppingListController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ICurrentMemberService currentMemberService,
            IHouseholdInitializationService householdInitializationService,
            IFoodStatusService foodStatusService,
            IFoodStatusNotificationService foodStatusNotificationService)
        {
            _context = context;
            _userManager = userManager;
            _currentMemberService = currentMemberService;
            _householdInitializationService = householdInitializationService;
            _foodStatusService = foodStatusService;
            _foodStatusNotificationService = foodStatusNotificationService;
        }

        public async Task<IActionResult> Index()
        {
            var currentMemberId = _currentMemberService.GetCurrentMemberId();
            if (!currentMemberId.HasValue)
            {
                return RedirectToAction("Select", "Members", new { returnUrl = Request.Path + Request.QueryString });
            }

            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // 1. Get active shopping items
            var activeItems = await _context.ShoppingItems
                .Include(s => s.FoodItem)
                .Include(s => s.Category)
                .Include(s => s.AddedByMember)
                .Where(s => s.HouseholdId == household.Id && !s.IsPurchased)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            // 2. Get purchased shopping items (history, limited to 50)
            var purchasedItems = await _context.ShoppingItems
                .Include(s => s.FoodItem)
                .Include(s => s.Category)
                .Include(s => s.PurchasedByMember)
                .Where(s => s.HouseholdId == household.Id && s.IsPurchased)
                .OrderByDescending(s => s.PurchasedAt)
                .Take(50)
                .ToListAsync();

            // 3. Get food items in the fridge that are low stock or out of stock (excluding already listed items)
            var lowStockFoods = await _context.FoodItems
                .Include(f => f.Category)
                .Where(f => f.HouseholdId == household.Id && !f.IsArchived &&
                            (f.CurrentQuantity <= 0 || f.CurrentQuantity < f.MinQuantityThreshold))
                .ToListAsync();

            var activeShoppingFoodIds = activeItems
                .Where(ai => ai.FoodItemId.HasValue)
                .Select(ai => ai.FoodItemId!.Value)
                .ToHashSet();

            var suggestions = lowStockFoods
                .Where(f => !activeShoppingFoodIds.Contains(f.Id))
                .Select(f => new ShoppingSuggestionViewModel
                {
                    FoodItemId = f.Id,
                    FoodName = f.Name,
                    CurrentQuantity = f.CurrentQuantity,
                    MinQuantityThreshold = f.MinQuantityThreshold,
                    Unit = f.Unit,
                    CategoryId = f.CategoryId,
                    CategoryName = f.Category?.Name ?? "Không phân loại",
                    Reason = f.CurrentQuantity <= 0 ? "Hết hàng" : "Dưới ngưỡng tối thiểu"
                })
                .ToList();

            // 4. Populate categories and storage locations dropdown lists
            var categories = await _context.Categories
                .Where(c => c.HouseholdId == household.Id && c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                .ToListAsync();

            var storageLocations = await _context.StorageLocations
                .Where(s => s.HouseholdId == household.Id && s.IsActive)
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.Name)
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name })
                .ToListAsync();

            var viewModel = new ShoppingListViewModel
            {
                ActiveItems = activeItems,
                PurchasedItems = purchasedItems,
                Suggestions = suggestions,
                Categories = categories,
                StorageLocations = storageLocations,
                NewItem = new ShoppingItemFormViewModel
                {
                    Quantity = 1,
                    Unit = "Cái"
                }
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ShoppingListViewModel model)
        {
            var currentMemberId = _currentMemberService.GetCurrentMemberId();
            if (!currentMemberId.HasValue)
            {
                return RedirectToAction("Select", "Members", new { returnUrl = Url.Action("Index") });
            }

            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            if (string.IsNullOrWhiteSpace(model.NewItem.Name))
            {
                TempData["ErrorMessage"] = "Tên mặt hàng không được để trống.";
                return RedirectToAction(nameof(Index));
            }

            var item = new ShoppingItem
            {
                HouseholdId = household.Id,
                Name = model.NewItem.Name.Trim(),
                FoodItemId = model.NewItem.FoodItemId,
                CategoryId = model.NewItem.CategoryId,
                Quantity = model.NewItem.Quantity,
                Unit = string.IsNullOrWhiteSpace(model.NewItem.Unit) ? "Cái" : model.NewItem.Unit.Trim(),
                IsPurchased = false,
                AddedByMemberId = currentMemberId.Value,
                CreatedAt = DateTime.UtcNow
            };

            _context.ShoppingItems.Add(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã thêm '{item.Name}' vào danh sách mua sắm.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSuggestion(int foodItemId)
        {
            var currentMemberId = _currentMemberService.GetCurrentMemberId();
            if (!currentMemberId.HasValue)
            {
                return RedirectToAction("Select", "Members", new { returnUrl = Url.Action("Index") });
            }

            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var foodItem = await _context.FoodItems
                .FirstOrDefaultAsync(f => f.Id == foodItemId && f.HouseholdId == household.Id && !f.IsArchived);

            if (foodItem == null)
            {
                return NotFound();
            }

            var exists = await _context.ShoppingItems
                .AnyAsync(s => s.HouseholdId == household.Id && s.FoodItemId == foodItemId && !s.IsPurchased);

            if (!exists)
            {
                // Calculate suggested quantity
                decimal suggestedQty = 1;
                if (foodItem.MinQuantityThreshold > foodItem.CurrentQuantity)
                {
                    suggestedQty = foodItem.MinQuantityThreshold - foodItem.CurrentQuantity;
                }

                var item = new ShoppingItem
                {
                    HouseholdId = household.Id,
                    FoodItemId = foodItem.Id,
                    Name = foodItem.Name,
                    CategoryId = foodItem.CategoryId,
                    Quantity = suggestedQty,
                    Unit = foodItem.Unit,
                    IsPurchased = false,
                    AddedByMemberId = currentMemberId.Value,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ShoppingItems.Add(item);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã thêm gợi ý '{foodItem.Name}' vào danh sách.";
            }
            else
            {
                TempData["ErrorMessage"] = $"Mặt hàng '{foodItem.Name}' đã có trong danh sách.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePurchase(int id)
        {
            var currentMemberId = _currentMemberService.GetCurrentMemberId();
            if (!currentMemberId.HasValue)
            {
                return RedirectToAction("Select", "Members", new { returnUrl = Url.Action("Index") });
            }

            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var item = await _context.ShoppingItems
                .FirstOrDefaultAsync(s => s.Id == id && s.HouseholdId == household.Id);

            if (item == null)
            {
                return NotFound();
            }

            item.IsPurchased = !item.IsPurchased;
            if (item.IsPurchased)
            {
                item.PurchasedAt = DateTime.UtcNow;
                item.PurchasedByMemberId = currentMemberId.Value;
                TempData["SuccessMessage"] = $"Đã đánh dấu đã mua: '{item.Name}'.";
            }
            else
            {
                item.PurchasedAt = null;
                item.PurchasedByMemberId = null;
                TempData["SuccessMessage"] = $"Đã đưa '{item.Name}' trở lại danh sách cần mua.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestockConfirm(int id, decimal quantity, DateTime expiryDate, int storageLocationId, int? categoryId)
        {
            var currentMemberId = _currentMemberService.GetCurrentMemberId();
            if (!currentMemberId.HasValue)
            {
                return RedirectToAction("Select", "Members", new { returnUrl = Url.Action("Index") });
            }

            var household = await GetCurrentHouseholdAsync();
            if (household == null || household.Settings == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var shoppingItem = await _context.ShoppingItems
                .FirstOrDefaultAsync(s => s.Id == id && s.HouseholdId == household.Id);

            if (shoppingItem == null)
            {
                return NotFound();
            }

            // Resolve Category ID (since CategoryId in FoodItem is non-nullable int)
            int resolvedCategoryId;
            if (categoryId.HasValue)
            {
                resolvedCategoryId = categoryId.Value;
            }
            else if (shoppingItem.CategoryId.HasValue)
            {
                resolvedCategoryId = shoppingItem.CategoryId.Value;
            }
            else
            {
                // Fallback to the first active category of the household
                var fallbackCategory = await _context.Categories
                    .Where(c => c.HouseholdId == household.Id && c.IsActive)
                    .OrderBy(c => c.SortOrder)
                    .FirstOrDefaultAsync();

                if (fallbackCategory == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy danh mục hợp lệ để gán cho thực phẩm mới. Vui lòng tạo danh mục trước.";
                    return RedirectToAction(nameof(Index));
                }
                resolvedCategoryId = fallbackCategory.Id;
            }

            FoodItem foodItem;
            FoodStatus oldStatus = FoodStatus.Normal;
            FoodStatus newStatus;

            if (shoppingItem.FoodItemId.HasValue)
            {
                // 1. Linked Food Item exists
                var dbFoodItem = await _context.FoodItems
                    .FirstOrDefaultAsync(f => f.Id == shoppingItem.FoodItemId.Value && f.HouseholdId == household.Id && !f.IsArchived);

                if (dbFoodItem == null)
                {
                    TempData["ErrorMessage"] = "Thực phẩm liên kết không còn tồn tại trong kho.";
                    return RedirectToAction(nameof(Index));
                }

                foodItem = dbFoodItem;
                oldStatus = foodItem.CurrentStatus;
                var quantityBefore = foodItem.CurrentQuantity;

                // Update inventory
                foodItem.CurrentQuantity += quantity;
                foodItem.ExpiryDate = expiryDate;
                foodItem.StorageLocationId = storageLocationId;
                foodItem.CategoryId = resolvedCategoryId;
                foodItem.LastUpdatedByMemberId = currentMemberId.Value;

                // Recalculate status
                newStatus = _foodStatusService.CalculateStatus(foodItem.CurrentQuantity, foodItem.ExpiryDate, household.Settings);
                foodItem.CurrentStatus = newStatus;
                
                if (newStatus == FoodStatus.OutOfStock)
                {
                    if (!foodItem.OutOfStockSince.HasValue)
                    {
                        foodItem.OutOfStockSince = DateTime.UtcNow;
                    }
                }
                else
                {
                    foodItem.OutOfStockSince = null;
                }
                foodItem.LastStatusChangedAt = DateTime.UtcNow;

                // Activity log
                var activityLog = new FoodActivityLog
                {
                    FoodItemId = foodItem.Id,
                    MemberProfileId = currentMemberId.Value,
                    ActivityType = FoodActivityType.Restock,
                    QuantityDelta = quantity,
                    QuantityBefore = quantityBefore,
                    QuantityAfter = foodItem.CurrentQuantity,
                    Reason = "Nhập kho nhanh từ danh sách mua sắm",
                    CreatedAt = DateTime.UtcNow
                };

                _context.FoodActivityLogs.Add(activityLog);
            }
            else
            {
                // 2. Unlinked item - Create a new FoodItem
                foodItem = new FoodItem
                {
                    HouseholdId = household.Id,
                    CategoryId = resolvedCategoryId,
                    StorageLocationId = storageLocationId,
                    Name = shoppingItem.Name,
                    Unit = string.IsNullOrWhiteSpace(shoppingItem.Unit) ? "Cái" : shoppingItem.Unit,
                    CurrentQuantity = quantity,
                    MinQuantityThreshold = 0,
                    ExpiryDate = expiryDate,
                    AddedDate = DateTime.UtcNow,
                    AddedByMemberId = currentMemberId.Value,
                    LastUpdatedByMemberId = currentMemberId.Value,
                    IsArchived = false
                };

                newStatus = _foodStatusService.CalculateStatus(foodItem.CurrentQuantity, foodItem.ExpiryDate, household.Settings);
                foodItem.CurrentStatus = newStatus;
                
                if (newStatus == FoodStatus.OutOfStock)
                {
                    foodItem.OutOfStockSince = DateTime.UtcNow;
                }
                foodItem.LastStatusChangedAt = DateTime.UtcNow;

                _context.FoodItems.Add(foodItem);
                await _context.SaveChangesAsync(); // save to get foodItem.Id

                // Link the shopping item to this new food item
                shoppingItem.FoodItemId = foodItem.Id;

                // Activity log for creation
                var activityLog = new FoodActivityLog
                {
                    FoodItemId = foodItem.Id,
                    MemberProfileId = currentMemberId.Value,
                    ActivityType = FoodActivityType.Create,
                    QuantityDelta = quantity,
                    QuantityBefore = 0,
                    QuantityAfter = foodItem.CurrentQuantity,
                    Reason = "Tạo mới từ danh sách mua sắm",
                    CreatedAt = DateTime.UtcNow
                };

                _context.FoodActivityLogs.Add(activityLog);
            }

            // Mark shopping item as purchased
            shoppingItem.IsPurchased = true;
            shoppingItem.PurchasedAt = DateTime.UtcNow;
            shoppingItem.PurchasedByMemberId = currentMemberId.Value;

            await _context.SaveChangesAsync();

            // Trigger notification
            await _foodStatusNotificationService.NotifyStatusTransitionAsync(foodItem, oldStatus, newStatus);

            TempData["SuccessMessage"] = $"Đã nhập kho thành công: {foodItem.Name} ({quantity} {foodItem.Unit}).";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var currentMemberId = _currentMemberService.GetCurrentMemberId();
            if (!currentMemberId.HasValue)
            {
                return RedirectToAction("Select", "Members", new { returnUrl = Url.Action("Index") });
            }

            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var item = await _context.ShoppingItems
                .FirstOrDefaultAsync(s => s.Id == id && s.HouseholdId == household.Id);

            if (item == null)
            {
                return NotFound();
            }

            _context.ShoppingItems.Remove(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã xóa '{item.Name}' khỏi danh sách.";
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

            return await _context.Households
                .Include(h => h.Settings)
                .FirstOrDefaultAsync(h => h.OwnerUserId == user.Id);
        }
    }
}
