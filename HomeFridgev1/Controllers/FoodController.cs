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
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class FoodController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ICurrentMemberService _currentMemberService;
        private readonly IFoodStatusService _foodStatusService;
        private readonly IFoodImageLibraryService _foodImageLibraryService;
        private readonly IFoodStatusNotificationService _foodStatusNotificationService;

        public FoodController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ICurrentMemberService currentMemberService,
            IFoodStatusService foodStatusService,
            IFoodImageLibraryService foodImageLibraryService,
            IFoodStatusNotificationService foodStatusNotificationService)
        {
            _context = context;
            _userManager = userManager;
            _currentMemberService = currentMemberService;
            _foodStatusService = foodStatusService;
            _foodImageLibraryService = foodImageLibraryService;
            _foodStatusNotificationService = foodStatusNotificationService;
        }

        public async Task<IActionResult> Index(
            string? searchTerm,
            int? categoryId,
            int? storageLocationId,
            string? status,
            string? sortBy)
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // Apply auto-hide rule dynamically so the list is always fresh
            await _foodStatusService.ApplyAutoHideRuleAsync(household.Id);

            var query = _context.FoodItems
                .Include(f => f.Category)
                .Include(f => f.StorageLocation)
                .Where(f => f.HouseholdId == household.Id && !f.IsArchived)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var trimmedSearch = searchTerm.Trim();
                query = query.Where(f => f.Name.Contains(trimmedSearch));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(f => f.CategoryId == categoryId.Value);
            }

            if (storageLocationId.HasValue)
            {
                query = query.Where(f => f.StorageLocationId == storageLocationId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<FoodStatus>(status, out var parsedStatus))
            {
                query = query.Where(f => f.CurrentStatus == parsedStatus);
            }

            query = sortBy switch
            {
                "name_asc" => query.OrderBy(f => f.Name),
                "newest" => query.OrderByDescending(f => f.AddedDate),
                "expiry_desc" => query.OrderByDescending(f => f.ExpiryDate),
                _ => query.OrderBy(f => f.ExpiryDate)
            };

            var items = await query.ToListAsync();

            var model = new FoodIndexViewModel
            {
                Items = items,
                SearchTerm = searchTerm,
                CategoryId = categoryId,
                StorageLocationId = storageLocationId,
                Status = status,
                SortBy = sortBy
            };

            await PopulateIndexFiltersAsync(model, household.Id);

            return View(model);
        }

        public async Task<IActionResult> Create()
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

            var model = new FoodCreateViewModel
            {
                ExpiryDate = DateTime.Today.AddDays(3)
            };

            await PopulateSelectionsAsync(model, household.Id);
            await PopulateImageLibraryAsync(model);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FoodCreateViewModel model)
        {
            var currentMemberId = _currentMemberService.GetCurrentMemberId();
            if (!currentMemberId.HasValue)
            {
                return RedirectToAction("Select", "Members", new { returnUrl = Request.Path + Request.QueryString });
            }

            var household = await GetCurrentHouseholdAsync();
            if (household == null || household.Settings == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var validCategory = await _context.Categories
                .AnyAsync(c => c.Id == model.CategoryId && c.HouseholdId == household.Id && c.IsActive);

            var validLocation = await _context.StorageLocations
                .AnyAsync(s => s.Id == model.StorageLocationId && s.HouseholdId == household.Id && s.IsActive);

            if (!validCategory)
            {
                ModelState.AddModelError(nameof(model.CategoryId), "Danh mục không hợp lệ.");
            }

            if (!validLocation)
            {
                ModelState.AddModelError(nameof(model.StorageLocationId), "Vị trí lưu trữ không hợp lệ.");
            }

            var imageLibrary = await _foodImageLibraryService.GetAllAsync();

            if (!string.IsNullOrWhiteSpace(model.SelectedImagePath) &&
                !imageLibrary.Any(i => i.ImagePath == model.SelectedImagePath))
            {
                ModelState.AddModelError(nameof(model.SelectedImagePath), "Ảnh thực phẩm không hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateSelectionsAsync(model, household.Id);
                await PopulateImageLibraryAsync(model);
                return View(model);
            }

            var foodItem = new FoodItem
            {
                HouseholdId = household.Id,
                CategoryId = model.CategoryId,
                StorageLocationId = model.StorageLocationId,
                Name = model.Name,
                ImagePath = model.SelectedImagePath,
                Unit = model.Unit,
                CurrentQuantity = model.CurrentQuantity,
                MinQuantityThreshold = model.MinQuantityThreshold,
                ExpiryDate = model.ExpiryDate,
                AddedDate = DateTime.UtcNow,
                AddedByMemberId = currentMemberId.Value,
                LastUpdatedByMemberId = currentMemberId.Value,
                Notes = model.Notes,
                IsArchived = false
            };

            var oldStatus = FoodStatus.Normal;
            UpdateStatusAndOutOfStockTracking(foodItem, household.Settings);

            _context.FoodItems.Add(foodItem);
            await _context.SaveChangesAsync();

            var activityLog = new FoodActivityLog
            {
                FoodItemId = foodItem.Id,
                MemberProfileId = currentMemberId.Value,
                ActivityType = FoodActivityType.Create,
                QuantityDelta = model.CurrentQuantity,
                QuantityBefore = 0,
                QuantityAfter = model.CurrentQuantity,
                Reason = "Tạo mới thực phẩm",
                CreatedAt = DateTime.UtcNow
            };

            _context.FoodActivityLogs.Add(activityLog);
            await _context.SaveChangesAsync();

            await NotifyStatusChangeAsync(foodItem, oldStatus);

            TempData["SuccessMessage"] = "Đã thêm thực phẩm thành công.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
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

            var foodItem = await _context.FoodItems
                .FirstOrDefaultAsync(f => f.Id == id && f.HouseholdId == household.Id && !f.IsArchived);

            if (foodItem == null)
            {
                return NotFound();
            }

            var model = new FoodEditViewModel
            {
                Id = foodItem.Id,
                Name = foodItem.Name,
                CategoryId = foodItem.CategoryId,
                StorageLocationId = foodItem.StorageLocationId,
                Unit = foodItem.Unit,
                MinQuantityThreshold = foodItem.MinQuantityThreshold,
                ExpiryDate = foodItem.ExpiryDate,
                Notes = foodItem.Notes,
                SelectedImagePath = foodItem.ImagePath
            };

            await PopulateEditSelectionsAsync(model, household.Id);
            await PopulateImageLibraryAsync(model);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(FoodEditViewModel model)
        {
            var currentMemberId = _currentMemberService.GetCurrentMemberId();
            if (!currentMemberId.HasValue)
            {
                return RedirectToAction("Select", "Members", new { returnUrl = Request.Path + Request.QueryString });
            }

            var household = await GetCurrentHouseholdAsync();
            if (household == null || household.Settings == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var foodItem = await _context.FoodItems
                .FirstOrDefaultAsync(f => f.Id == model.Id && f.HouseholdId == household.Id && !f.IsArchived);

            if (foodItem == null)
            {
                return NotFound();
            }

            var validCategory = await _context.Categories
                .AnyAsync(c => c.Id == model.CategoryId && c.HouseholdId == household.Id && c.IsActive);

            var validLocation = await _context.StorageLocations
                .AnyAsync(s => s.Id == model.StorageLocationId && s.HouseholdId == household.Id && s.IsActive);

            if (!validCategory)
            {
                ModelState.AddModelError(nameof(model.CategoryId), "Danh mục không hợp lệ.");
            }

            if (!validLocation)
            {
                ModelState.AddModelError(nameof(model.StorageLocationId), "Vị trí lưu trữ không hợp lệ.");
            }

            var imageLibrary = await _foodImageLibraryService.GetAllAsync();

            if (!string.IsNullOrWhiteSpace(model.SelectedImagePath) &&
                !imageLibrary.Any(i => i.ImagePath == model.SelectedImagePath))
            {
                ModelState.AddModelError(nameof(model.SelectedImagePath), "Ảnh thực phẩm không hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateEditSelectionsAsync(model, household.Id);
                await PopulateImageLibraryAsync(model);
                return View(model);
            }

            var quantityBefore = foodItem.CurrentQuantity;
            var oldStatus = foodItem.CurrentStatus;

            foodItem.Name = model.Name;
            foodItem.CategoryId = model.CategoryId;
            foodItem.StorageLocationId = model.StorageLocationId;
            foodItem.Unit = model.Unit;
            foodItem.MinQuantityThreshold = model.MinQuantityThreshold;
            foodItem.ExpiryDate = model.ExpiryDate;
            foodItem.Notes = model.Notes;
            foodItem.ImagePath = model.SelectedImagePath;
            foodItem.LastUpdatedByMemberId = currentMemberId.Value;

            UpdateStatusAndOutOfStockTracking(foodItem, household.Settings);

            await _context.SaveChangesAsync();

            var activityLog = new FoodActivityLog
            {
                FoodItemId = foodItem.Id,
                MemberProfileId = currentMemberId.Value,
                ActivityType = FoodActivityType.Adjust,
                QuantityDelta = 0,
                QuantityBefore = quantityBefore,
                QuantityAfter = foodItem.CurrentQuantity,
                Reason = "Cập nhật thông tin thực phẩm",
                CreatedAt = DateTime.UtcNow
            };

            _context.FoodActivityLogs.Add(activityLog);
            await _context.SaveChangesAsync();

            await NotifyStatusChangeAsync(foodItem, oldStatus);

            TempData["SuccessMessage"] = "Đã cập nhật thực phẩm thành công.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Consume(int id, string? source = null)
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

            var foodItem = await GetFoodItemForActionAsync(id, household.Id, source);
            if (foodItem == null)
            {
                return NotFound();
            }

            if (!CanConsume(foodItem))
            {
                TempData["ErrorMessage"] = GetBlockedActionMessage(foodItem, "Consume");
                return RedirectToSource(source, foodItem.IsArchived);
            }

            var model = BuildQuantityActionModel(foodItem, "Consume", source);
            return View("QuantityAction", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Consume(FoodQuantityActionViewModel model)
        {
            var currentMemberId = _currentMemberService.GetCurrentMemberId();
            if (!currentMemberId.HasValue)
            {
                return RedirectToAction("Select", "Members", new { returnUrl = Request.Path + Request.QueryString });
            }

            var household = await GetCurrentHouseholdAsync();
            if (household == null || household.Settings == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var foodItem = await GetFoodItemForActionAsync(model.Id, household.Id, model.Source);
            if (foodItem == null)
            {
                return NotFound();
            }

            if (!CanConsume(foodItem))
            {
                TempData["ErrorMessage"] = GetBlockedActionMessage(foodItem, "Consume");
                return RedirectToSource(model.Source, foodItem.IsArchived);
            }

            model.ActionType = "Consume";
            model.FoodName = foodItem.Name;
            model.ImagePath = foodItem.ImagePath;
            model.CurrentQuantity = foodItem.CurrentQuantity;
            model.Unit = foodItem.Unit;
            model.ExpiryDate = foodItem.ExpiryDate;
            model.CurrentStatus = foodItem.CurrentStatus.ToString();

            if (model.Quantity > foodItem.CurrentQuantity)
            {
                ModelState.AddModelError(nameof(model.Quantity), "Số lượng dùng bớt không được lớn hơn số lượng hiện tại.");
            }

            if (!ModelState.IsValid)
            {
                return View("QuantityAction", model);
            }

            var quantityBefore = foodItem.CurrentQuantity;
            var oldStatus = foodItem.CurrentStatus;

            foodItem.CurrentQuantity -= model.Quantity;
            if (foodItem.CurrentQuantity < 0)
            {
                foodItem.CurrentQuantity = 0;
            }

            foodItem.LastUpdatedByMemberId = currentMemberId.Value;
            UpdateStatusAndOutOfStockTracking(foodItem, household.Settings);

            await _context.SaveChangesAsync();

            var activityLog = new FoodActivityLog
            {
                FoodItemId = foodItem.Id,
                MemberProfileId = currentMemberId.Value,
                ActivityType = FoodActivityType.Consume,
                QuantityDelta = -model.Quantity,
                QuantityBefore = quantityBefore,
                QuantityAfter = foodItem.CurrentQuantity,
                Reason = string.IsNullOrWhiteSpace(model.Reason) ? GetDefaultReason("Consume") : model.Reason,
                CreatedAt = DateTime.UtcNow
            };

            _context.FoodActivityLogs.Add(activityLog);
            await _context.SaveChangesAsync();

            await NotifyStatusChangeAsync(foodItem, oldStatus);

            TempData["SuccessMessage"] = "Đã cập nhật thao tác dùng bớt thực phẩm.";
            return RedirectToSource(model.Source, foodItem.IsArchived);
        }

        public async Task<IActionResult> Restock(int id, string? source = null)
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

            var foodItem = await GetFoodItemForActionAsync(id, household.Id, source);
            if (foodItem == null)
            {
                return NotFound();
            }

            if (!CanRestock(foodItem))
            {
                TempData["ErrorMessage"] = GetBlockedActionMessage(foodItem, "Restock");
                return RedirectToSource(source, foodItem.IsArchived);
            }

            var model = BuildQuantityActionModel(foodItem, "Restock", source);
            return View("QuantityAction", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restock(FoodQuantityActionViewModel model)
        {
            var currentMemberId = _currentMemberService.GetCurrentMemberId();
            if (!currentMemberId.HasValue)
            {
                return RedirectToAction("Select", "Members", new { returnUrl = Request.Path + Request.QueryString });
            }

            var household = await GetCurrentHouseholdAsync();
            if (household == null || household.Settings == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var foodItem = await GetFoodItemForActionAsync(model.Id, household.Id, model.Source);
            if (foodItem == null)
            {
                return NotFound();
            }

            if (!CanRestock(foodItem))
            {
                TempData["ErrorMessage"] = GetBlockedActionMessage(foodItem, "Restock");
                return RedirectToSource(model.Source, foodItem.IsArchived);
            }

            model.ActionType = "Restock";
            model.FoodName = foodItem.Name;
            model.ImagePath = foodItem.ImagePath;
            model.CurrentQuantity = foodItem.CurrentQuantity;
            model.Unit = foodItem.Unit;
            model.ExpiryDate = foodItem.ExpiryDate;
            model.CurrentStatus = foodItem.CurrentStatus.ToString();

            if (!ModelState.IsValid)
            {
                return View("QuantityAction", model);
            }

            var quantityBefore = foodItem.CurrentQuantity;
            var oldStatus = foodItem.CurrentStatus;

            foodItem.CurrentQuantity += model.Quantity;
            foodItem.LastUpdatedByMemberId = currentMemberId.Value;
            UpdateStatusAndOutOfStockTracking(foodItem, household.Settings);

            await _context.SaveChangesAsync();

            var activityLog = new FoodActivityLog
            {
                FoodItemId = foodItem.Id,
                MemberProfileId = currentMemberId.Value,
                ActivityType = FoodActivityType.Restock,
                QuantityDelta = model.Quantity,
                QuantityBefore = quantityBefore,
                QuantityAfter = foodItem.CurrentQuantity,
                Reason = string.IsNullOrWhiteSpace(model.Reason) ? GetDefaultReason("Restock") : model.Reason,
                CreatedAt = DateTime.UtcNow
            };

            _context.FoodActivityLogs.Add(activityLog);
            await _context.SaveChangesAsync();

            await NotifyStatusChangeAsync(foodItem, oldStatus);

            TempData["SuccessMessage"] = "Đã bổ sung thêm số lượng thực phẩm.";
            return RedirectToSource(model.Source);
        }

        public async Task<IActionResult> Discard(int id)
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

            var foodItem = await GetOwnedFoodItemAsync(id, household.Id);
            if (foodItem == null)
            {
                return NotFound();
            }

            if (!CanDiscard(foodItem))
            {
                TempData["ErrorMessage"] = GetBlockedActionMessage(foodItem, "Discard");
                return RedirectToAction(nameof(Index));
            }

            var model = BuildQuantityActionModel(foodItem, "Discard");
            return View("QuantityAction", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Discard(FoodQuantityActionViewModel model)
        {
            var currentMemberId = _currentMemberService.GetCurrentMemberId();
            if (!currentMemberId.HasValue)
            {
                return RedirectToAction("Select", "Members", new { returnUrl = Request.Path + Request.QueryString });
            }

            var household = await GetCurrentHouseholdAsync();
            if (household == null || household.Settings == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var foodItem = await GetOwnedFoodItemAsync(model.Id, household.Id);
            if (foodItem == null)
            {
                return NotFound();
            }

            if (!CanDiscard(foodItem))
            {
                TempData["ErrorMessage"] = GetBlockedActionMessage(foodItem, "Discard");
                return RedirectToAction(nameof(Index));
            }

            model.ActionType = "Discard";
            model.FoodName = foodItem.Name;
            model.ImagePath = foodItem.ImagePath;
            model.CurrentQuantity = foodItem.CurrentQuantity;
            model.Unit = foodItem.Unit;
            model.ExpiryDate = foodItem.ExpiryDate;
            model.CurrentStatus = foodItem.CurrentStatus.ToString();

            if (model.Quantity > foodItem.CurrentQuantity)
            {
                ModelState.AddModelError(nameof(model.Quantity), "Số lượng bỏ đi không được lớn hơn số lượng hiện tại.");
            }

            if (!ModelState.IsValid)
            {
                return View("QuantityAction", model);
            }

            var quantityBefore = foodItem.CurrentQuantity;
            var oldStatus = foodItem.CurrentStatus;

            foodItem.CurrentQuantity -= model.Quantity;
            if (foodItem.CurrentQuantity < 0)
            {
                foodItem.CurrentQuantity = 0;
            }

            foodItem.LastUpdatedByMemberId = currentMemberId.Value;
            UpdateStatusAndOutOfStockTracking(foodItem, household.Settings);

            await _context.SaveChangesAsync();

            var activityLog = new FoodActivityLog
            {
                FoodItemId = foodItem.Id,
                MemberProfileId = currentMemberId.Value,
                ActivityType = FoodActivityType.Discard,
                QuantityDelta = -model.Quantity,
                QuantityBefore = quantityBefore,
                QuantityAfter = foodItem.CurrentQuantity,
                Reason = string.IsNullOrWhiteSpace(model.Reason) ? GetDefaultReason("Discard") : model.Reason,
                CreatedAt = DateTime.UtcNow
            };

            _context.FoodActivityLogs.Add(activityLog);
            await _context.SaveChangesAsync();

            await NotifyStatusChangeAsync(foodItem, oldStatus);

            TempData["SuccessMessage"] = "Đã cập nhật thao tác bỏ đi thực phẩm.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Archived()
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var items = await _context.FoodItems
                .Include(f => f.Category)
                .Include(f => f.StorageLocation)
                .Include(f => f.ArchiveGroup)
                .Where(f => f.HouseholdId == household.Id && f.IsArchived)
                .OrderBy(f => f.ArchiveGroup != null ? f.ArchiveGroup.Name : "Chưa phân vùng")
                .ThenBy(f => f.Name)
                .ToListAsync();

            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var currentMemberId = _currentMemberService.GetCurrentMemberId();
            if (!currentMemberId.HasValue)
            {
                return RedirectToAction("Select", "Members", new { returnUrl = Request.Path + Request.QueryString });
            }

            var household = await GetCurrentHouseholdAsync();
            if (household == null || household.Settings == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var foodItem = await _context.FoodItems
                .FirstOrDefaultAsync(f => f.Id == id && f.HouseholdId == household.Id && f.IsArchived);

            if (foodItem == null)
            {
                return NotFound();
            }

            var oldStatus = foodItem.CurrentStatus;

            foodItem.IsArchived = false;
            foodItem.ArchiveGroupId = null;
            foodItem.LastUpdatedByMemberId = currentMemberId.Value;

            UpdateStatusAndOutOfStockTracking(foodItem, household.Settings);

            if (foodItem.CurrentStatus == FoodStatus.OutOfStock)
            {
                foodItem.OutOfStockSince = null;
            }

            await _context.SaveChangesAsync();

            var activityLog = new FoodActivityLog
            {
                FoodItemId = foodItem.Id,
                MemberProfileId = currentMemberId.Value,
                ActivityType = FoodActivityType.Restore,
                QuantityDelta = 0,
                QuantityBefore = foodItem.CurrentQuantity,
                QuantityAfter = foodItem.CurrentQuantity,
                Reason = "Khôi phục từ kho lưu trữ",
                CreatedAt = DateTime.UtcNow
            };

            _context.FoodActivityLogs.Add(activityLog);
            await _context.SaveChangesAsync();

            await NotifyStatusChangeAsync(foodItem, oldStatus);

            TempData["SuccessMessage"] = "Đã khôi phục thực phẩm khỏi kho lưu trữ.";
            return RedirectToAction(nameof(Archived));
        }

        [HttpGet]
        public async Task<IActionResult> ArchiveSelected(List<int> selectedIds)
        {
            var currentMemberId = _currentMemberService.GetCurrentMemberId();
            if (!currentMemberId.HasValue)
            {
                return RedirectToAction("Select", "Members", new { returnUrl = Request.Path + Request.QueryString });
            }

            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction(nameof(Index));
            }

            if (selectedIds == null || !selectedIds.Any())
            {
                TempData["ErrorMessage"] = "Bạn chưa chọn thực phẩm nào để lưu trữ.";
                return RedirectToAction(nameof(Index));
            }

            var items = await _context.FoodItems
                .Include(f => f.Category)
                .Include(f => f.StorageLocation)
                .Where(f => selectedIds.Contains(f.Id) && f.HouseholdId == household.Id && !f.IsArchived)
                .OrderBy(f => f.Name)
                .ToListAsync();

            if (!items.Any())
            {
                TempData["ErrorMessage"] = "Không tìm thấy thực phẩm hợp lệ để lưu trữ.";
                return RedirectToAction(nameof(Index));
            }

            var model = new FoodBulkArchiveViewModel
            {
                SelectedIds = items.Select(x => x.Id).ToList(),
                Items = items.Select(x => new FoodBulkArchiveItemViewModel
                {
                    Id = x.Id,
                    Name = x.Name,
                    ImagePath = x.ImagePath,
                    CategoryName = x.Category?.Name,
                    StorageLocationName = x.StorageLocation?.Name,
                    CurrentQuantity = x.CurrentQuantity,
                    Unit = x.Unit,
                    CurrentStatus = x.CurrentStatus.ToString()
                }).ToList()
            };

            await PopulateArchiveGroupsAsync(model, household.Id);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveSelected(FoodBulkArchiveViewModel model)
        {
            var currentMemberId = _currentMemberService.GetCurrentMemberId();
            if (!currentMemberId.HasValue)
            {
                return RedirectToAction("Select", "Members", new { returnUrl = Request.Path + Request.QueryString });
            }

            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction(nameof(Index));
            }

            if (model.SelectedIds == null || !model.SelectedIds.Any())
            {
                TempData["ErrorMessage"] = "Bạn chưa chọn thực phẩm nào để lưu trữ.";
                return RedirectToAction(nameof(Index));
            }

            ArchiveGroup? archiveGroup = null;

            if (model.CreateNewArchiveGroup)
            {
                var newGroupName = model.NewArchiveGroupName?.Trim();

                if (string.IsNullOrWhiteSpace(newGroupName))
                {
                    ModelState.AddModelError(nameof(model.NewArchiveGroupName), "Vui lòng nhập tên vùng lưu trữ mới.");
                }
                else
                {
                    var existingGroup = await _context.ArchiveGroups
                        .FirstOrDefaultAsync(g =>
                            g.HouseholdId == household.Id &&
                            g.IsActive &&
                            g.Name.ToLower() == newGroupName.ToLower());

                    if (existingGroup != null)
                    {
                        ModelState.AddModelError(nameof(model.NewArchiveGroupName), "Vùng lưu trữ đã tồn tại. Vui lòng chọn vùng có sẵn hoặc đặt tên khác.");
                    }
                    else
                    {
                        archiveGroup = new ArchiveGroup
                        {
                            HouseholdId = household.Id,
                            Name = newGroupName,
                            CreatedAt = DateTime.UtcNow,
                            CreatedByMemberId = currentMemberId.Value,
                            IsActive = true
                        };

                        _context.ArchiveGroups.Add(archiveGroup);
                        await _context.SaveChangesAsync();
                    }
                }
            }
            else
            {
                if (!model.ArchiveGroupId.HasValue)
                {
                    ModelState.AddModelError(nameof(model.ArchiveGroupId), "Vui lòng chọn một vùng lưu trữ.");
                }
                else
                {
                    archiveGroup = await _context.ArchiveGroups
                        .FirstOrDefaultAsync(g =>
                            g.Id == model.ArchiveGroupId.Value &&
                            g.HouseholdId == household.Id &&
                            g.IsActive);

                    if (archiveGroup == null)
                    {
                        ModelState.AddModelError(nameof(model.ArchiveGroupId), "Vùng lưu trữ không hợp lệ.");
                    }
                }
            }

            var items = await _context.FoodItems
                .Where(f => model.SelectedIds.Contains(f.Id) && f.HouseholdId == household.Id && !f.IsArchived)
                .ToListAsync();

            if (!items.Any())
            {
                TempData["ErrorMessage"] = "Không có thực phẩm hợp lệ để lưu trữ.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                model.Items = await _context.FoodItems
                    .Include(f => f.Category)
                    .Include(f => f.StorageLocation)
                    .Where(f => model.SelectedIds.Contains(f.Id) && f.HouseholdId == household.Id && !f.IsArchived)
                    .OrderBy(f => f.Name)
                    .Select(x => new FoodBulkArchiveItemViewModel
                    {
                        Id = x.Id,
                        Name = x.Name,
                        ImagePath = x.ImagePath,
                        CategoryName = x.Category != null ? x.Category.Name : null,
                        StorageLocationName = x.StorageLocation != null ? x.StorageLocation.Name : null,
                        CurrentQuantity = x.CurrentQuantity,
                        Unit = x.Unit,
                        CurrentStatus = x.CurrentStatus.ToString()
                    })
                    .ToListAsync();

                await PopulateArchiveGroupsAsync(model, household.Id);
                return View(model);
            }

            var now = DateTime.UtcNow;

            foreach (var item in items)
            {
                item.IsArchived = true;
                item.ArchiveGroupId = archiveGroup!.Id;
                item.LastUpdatedByMemberId = currentMemberId.Value;
                item.LastStatusChangedAt = now;
            }

            await _context.SaveChangesAsync();

            var logs = items.Select(item => new FoodActivityLog
            {
                FoodItemId = item.Id,
                MemberProfileId = currentMemberId.Value,
                ActivityType = FoodActivityType.Archive,
                QuantityDelta = 0,
                QuantityBefore = item.CurrentQuantity,
                QuantityAfter = item.CurrentQuantity,
                Reason = string.IsNullOrWhiteSpace(model.Reason)
                    ? $"Lưu trữ vào vùng: {archiveGroup!.Name}"
                    : model.Reason,
                CreatedAt = now
            }).ToList();

            _context.FoodActivityLogs.AddRange(logs);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã lưu trữ {items.Count} thực phẩm vào vùng '{archiveGroup!.Name}'.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateArchiveGroupsAsync(FoodBulkArchiveViewModel model, int householdId)
        {
            model.AvailableArchiveGroups = await _context.ArchiveGroups
                .Where(g => g.HouseholdId == householdId && g.IsActive)
                .OrderBy(g => g.Name)
                .Select(g => new SelectListItem
                {
                    Value = g.Id.ToString(),
                    Text = g.Name
                })
                .ToListAsync();

            model.AvailableArchiveGroups.Insert(0, new SelectListItem
            {
                Value = "",
                Text = "Chọn vùng lưu trữ"
            });
        }

        private FoodQuantityActionViewModel BuildQuantityActionModel(FoodItem foodItem, string actionType, string? source = null)
        {
            return new FoodQuantityActionViewModel
            {
                Id = foodItem.Id,
                ActionType = actionType,
                FoodName = foodItem.Name,
                ImagePath = foodItem.ImagePath,
                CurrentQuantity = foodItem.CurrentQuantity,
                Unit = foodItem.Unit,
                ExpiryDate = foodItem.ExpiryDate,
                CurrentStatus = foodItem.CurrentStatus.ToString(),
                Source = source
            };
        }

        private async Task<FoodItem?> GetOwnedFoodItemAsync(int id, int householdId)
        {
            return await _context.FoodItems
                .FirstOrDefaultAsync(f => f.Id == id && f.HouseholdId == householdId && !f.IsArchived);
        }

        private async Task<FoodItem?> GetFoodItemForActionAsync(int id, int householdId, string? source)
        {
            var query = _context.FoodItems
                .Where(f => f.Id == id && f.HouseholdId == householdId);

            if (IsArchivedSource(source))
            {
                query = query.Where(f => f.IsArchived);
            }
            else
            {
                query = query.Where(f => !f.IsArchived);
            }

            return await query.FirstOrDefaultAsync();
        }

        private static bool IsArchivedSource(string? source)
        {
            return string.Equals(source, "archived", StringComparison.OrdinalIgnoreCase);
        }

        private IActionResult RedirectToSource(string? source, bool isArchived = false)
        {
            if (isArchived || IsArchivedSource(source))
            {
                return RedirectToAction(nameof(Archived));
            }

            return RedirectToAction(nameof(Index));
        }

        private string GetDefaultReason(string actionType)
        {
            return actionType switch
            {
                "Consume" => "Sử dụng bớt thực phẩm",
                "Restock" => "Bổ sung thêm thực phẩm",
                "Discard" => "Loại bỏ thực phẩm",
                _ => "Cập nhật số lượng thực phẩm"
            };
        }

        private static bool CanConsume(FoodItem foodItem)
        {
            return foodItem.CurrentStatus != FoodStatus.OutOfStock
                && foodItem.CurrentStatus != FoodStatus.Expired;
        }

        private static bool CanRestock(FoodItem foodItem)
        {
            return foodItem.CurrentStatus != FoodStatus.Expired;
        }

        private static bool CanDiscard(FoodItem foodItem)
        {
            return foodItem.CurrentStatus != FoodStatus.OutOfStock;
        }

        private static string GetBlockedActionMessage(FoodItem foodItem, string actionType)
        {
            return actionType switch
            {
                "Consume" when foodItem.CurrentStatus == FoodStatus.OutOfStock
                    => "Không thể dùng bớt vì thực phẩm đã hết hàng.",

                "Consume" when foodItem.CurrentStatus == FoodStatus.Expired
                    => "Không thể dùng bớt vì thực phẩm đã hết hạn.",

                "Restock" when foodItem.CurrentStatus == FoodStatus.Expired
                    => "Không thể bổ sung vì thực phẩm đã hết hạn. Với món hết hạn, bạn chỉ có thể thực hiện thao tác bỏ đi.",

                "Discard" when foodItem.CurrentStatus == FoodStatus.OutOfStock
                    => "Không thể bỏ đi vì thực phẩm đã hết hàng.",

                _ => "Thao tác này hiện không hợp lệ với trạng thái hiện tại của thực phẩm."
            };
        }

        private void UpdateStatusAndOutOfStockTracking(FoodItem foodItem, HouseholdSettings settings)
        {
            var newStatus = _foodStatusService.CalculateStatus(
                foodItem.CurrentQuantity,
                foodItem.ExpiryDate,
                settings);

            var now = DateTime.UtcNow;

            foodItem.CurrentStatus = newStatus;

            if (newStatus == FoodStatus.OutOfStock)
            {
                if (!foodItem.OutOfStockSince.HasValue)
                {
                    foodItem.OutOfStockSince = now;
                }
            }
            else
            {
                foodItem.OutOfStockSince = null;
            }

            foodItem.LastStatusChangedAt = now;
        }

        private async Task NotifyStatusChangeAsync(FoodItem foodItem, FoodStatus oldStatus)
        {
            await _foodStatusNotificationService.NotifyStatusTransitionAsync(
                foodItem,
                oldStatus,
                foodItem.CurrentStatus);
        }

        private async Task<Household?> GetCurrentHouseholdAsync()
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

        private async Task PopulateSelectionsAsync(FoodCreateViewModel model, int householdId)
        {
            model.Categories = await _context.Categories
                .Where(c => c.HouseholdId == householdId && c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                })
                .ToListAsync();

            model.StorageLocations = await _context.StorageLocations
                .Where(s => s.HouseholdId == householdId && s.IsActive)
                .OrderBy(s => s.SortOrder)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Name
                })
                .ToListAsync();
        }

        private async Task PopulateEditSelectionsAsync(FoodEditViewModel model, int householdId)
        {
            model.Categories = await _context.Categories
                .Where(c => c.HouseholdId == householdId && c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                })
                .ToListAsync();

            model.StorageLocations = await _context.StorageLocations
                .Where(s => s.HouseholdId == householdId && s.IsActive)
                .OrderBy(s => s.SortOrder)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Name
                })
                .ToListAsync();
        }

        private async Task PopulateIndexFiltersAsync(FoodIndexViewModel model, int householdId)
        {
            model.Categories = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "Tất cả danh mục" }
            };

            model.Categories.AddRange(await _context.Categories
                .Where(c => c.HouseholdId == householdId && c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                })
                .ToListAsync());

            model.StorageLocations = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "Tất cả vị trí" }
            };

            model.StorageLocations.AddRange(await _context.StorageLocations
                .Where(s => s.HouseholdId == householdId && s.IsActive)
                .OrderBy(s => s.SortOrder)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Name
                })
                .ToListAsync());

            model.Statuses = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "Tất cả trạng thái" },
                new SelectListItem { Value = FoodStatus.Normal.ToString(), Text = "Bình thường" },
                new SelectListItem { Value = FoodStatus.Warning.ToString(), Text = "Cảnh báo" },
                new SelectListItem { Value = FoodStatus.Urgent.ToString(), Text = "Cấp bách" },
                new SelectListItem { Value = FoodStatus.Expired.ToString(), Text = "Quá hạn" },
                new SelectListItem { Value = FoodStatus.OutOfStock.ToString(), Text = "Hết hàng" }
            };

            model.SortOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "Hạn dùng gần nhất" },
                new SelectListItem { Value = "expiry_desc", Text = "Hạn dùng xa nhất" },
                new SelectListItem { Value = "name_asc", Text = "Tên A-Z" },
                new SelectListItem { Value = "newest", Text = "Mới thêm gần nhất" }
            };
        }

        [HttpGet]
        public async Task<IActionResult> History(string? searchTerm, string? activityType)
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var query = _context.FoodActivityLogs
                .Include(a => a.FoodItem)
                .Include(a => a.MemberProfile)
                .Where(a => a.FoodItem!.HouseholdId == household.Id)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerSearchTerm = searchTerm.ToLower();
                query = query.Where(a => a.FoodItem!.Name.ToLower().Contains(lowerSearchTerm));
            }

            if (!string.IsNullOrWhiteSpace(activityType) && Enum.TryParse<FoodActivityType>(activityType, out var parsedType))
            {
                query = query.Where(a => a.ActivityType == parsedType);
            }

            var logs = await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(100)
                .ToListAsync();

            var viewModel = new FoodHistoryViewModel
            {
                SearchTerm = searchTerm,
                ActivityType = activityType,
                ActivityTypeOptions = new List<SelectListItem>
                {
                    new SelectListItem { Value = "", Text = "Tất cả thao tác" },
                    new SelectListItem { Value = "Create", Text = "Tạo mới" },
                    new SelectListItem { Value = "Adjust", Text = "Chỉnh sửa" },
                    new SelectListItem { Value = "Consume", Text = "Dùng bớt" },
                    new SelectListItem { Value = "Restock", Text = "Bổ sung" },
                    new SelectListItem { Value = "Discard", Text = "Bỏ đi" },
                    new SelectListItem { Value = "Archive", Text = "Lưu trữ" },
                    new SelectListItem { Value = "Restore", Text = "Khôi phục" }
                },
                Items = logs.Select(a => new FoodActivityHistoryItemViewModel
                {
                    Id = a.Id,
                    FoodItemId = a.FoodItemId,
                    FoodName = a.FoodItem?.Name ?? "N/A",
                    FoodImagePath = a.FoodItem?.ImagePath,
                    MemberProfileId = a.MemberProfileId,
                    MemberName = a.MemberProfile?.DisplayName ?? "N/A",
                    ActivityType = a.ActivityType,
                    QuantityDelta = a.QuantityDelta,
                    QuantityBefore = a.QuantityBefore,
                    QuantityAfter = a.QuantityAfter,
                    Reason = a.Reason,
                    CreatedAt = a.CreatedAt
                }).ToList()
            };

            return View(viewModel);
        }

        private async Task PopulateImageLibraryAsync(FoodCreateViewModel model)
        {
            model.ImageLibrary = await _foodImageLibraryService.GetAllAsync();
        }

        private async Task PopulateImageLibraryAsync(FoodEditViewModel model)
        {
            model.ImageLibrary = await _foodImageLibraryService.GetAllAsync();
        }
    }
}
