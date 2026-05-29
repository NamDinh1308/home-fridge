using HomeFridgev1.Data;
using HomeFridgev1.Models;
using HomeFridgev1.Models.Entities;
using HomeFridgev1.Models.Enums;
using HomeFridgev1.Models.ViewModels;
using HomeFridgev1.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace HomeFridgev1.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IHouseholdInitializationService _householdInitializationService;
        private readonly ApplicationDbContext _context;
        private readonly IFoodStatusService _foodStatusService;

        public HomeController(
            ILogger<HomeController> logger,
            UserManager<IdentityUser> userManager,
            IHouseholdInitializationService householdInitializationService,
            ApplicationDbContext context,
            IFoodStatusService foodStatusService)
        {
            _logger = logger;
            _userManager = userManager;
            _householdInitializationService = householdInitializationService;
            _context = context;
            _foodStatusService = foodStatusService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            var model = new DashboardViewModel
            {
                IsAuthenticated = User.Identity?.IsAuthenticated ?? false
            };

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                model.IsAuthenticated = false;
                return View(model);
            }

            var householdName = $"Nhà của {user.Email}";
            var ownerDisplayName = user.Email ?? "Chủ gia đình";

            await _householdInitializationService.EnsureInitializedAsync(
                user,
                householdName,
                ownerDisplayName);

            var household = await _context.Households
                .FirstOrDefaultAsync(h => h.OwnerUserId == user.Id);

            if (household == null)
            {
                return View(model);
            }

            // Apply auto-hide rule dynamically so dashboard stats are fresh
            await _foodStatusService.ApplyAutoHideRuleAsync(household.Id);

            var activeFoodsQuery = _context.FoodItems
                .Where(f => f.HouseholdId == household.Id && !f.IsArchived);

            model.UserEmail = user.Email;
            model.HouseholdName = household.Name;
            model.ActiveFoodCount = await activeFoodsQuery.CountAsync();
            model.ExpiringSoonCount = await activeFoodsQuery.CountAsync(f =>
                f.CurrentStatus == FoodStatus.Warning || f.CurrentStatus == FoodStatus.Urgent);
            model.OutOfStockCount = await activeFoodsQuery.CountAsync(f =>
                f.CurrentStatus == FoodStatus.OutOfStock);
            model.ArchivedCount = await _context.FoodItems.CountAsync(f =>
                f.HouseholdId == household.Id && f.IsArchived);

            // Giai đoạn 3: Tính số lượng chi tiết từng trạng thái để vẽ biểu đồ tròn
            model.NormalCount = await activeFoodsQuery.CountAsync(f => f.CurrentStatus == FoodStatus.Normal);
            model.WarningCount = await activeFoodsQuery.CountAsync(f => f.CurrentStatus == FoodStatus.Warning);
            model.UrgentCount = await activeFoodsQuery.CountAsync(f => f.CurrentStatus == FoodStatus.Urgent);
            model.ExpiredCount = await activeFoodsQuery.CountAsync(f => f.CurrentStatus == FoodStatus.Expired);

            // Giai đoạn 3: Phân bổ thực phẩm theo Danh mục (Category Distribution)
            // SQLite không hỗ trợ Sum trên decimal, nên materialize trước rồi tính trên client
            var activeFoodsList = await activeFoodsQuery
                .Include(f => f.Category)
                .ToListAsync();

            model.CategoryDistribution = activeFoodsList
                .GroupBy(f => f.Category?.Name ?? "Chưa phân loại")
                .Select(g => new CategoryDistributionViewModel
                {
                    CategoryName = g.Key,
                    FoodItemCount = g.Count(),
                    TotalQuantity = g.Sum(f => f.CurrentQuantity)
                })
                .ToList();

            // Giai đoạn 3: Phân tích lượng Tiêu thụ vs Lãng phí (Waste vs Consumption)
            // Truy vấn 30 ngày để phục vụ cả chế độ 7 ngày lẫn 1 tháng
            var monthStartDate = DateTime.UtcNow.Date.AddDays(-29);
            var logs = await _context.FoodActivityLogs
                .Include(x => x.FoodItem)
                .Where(x => x.FoodItem!.HouseholdId == household.Id &&
                            (x.ActivityType == FoodActivityType.Consume || x.ActivityType == FoodActivityType.Discard) &&
                            x.CreatedAt >= monthStartDate)
                .ToListAsync();

            // --- Chế độ 7 ngày (theo ngày) ---
            var weekStartDate = DateTime.UtcNow.Date.AddDays(-6);
            var wasteAnalysis = new List<WasteAnalysisDayViewModel>();
            for (int i = 0; i < 7; i++)
            {
                var day = weekStartDate.AddDays(i);
                var dayStr = day.ToString("dd/MM");
                
                var dayLogs = logs.Where(x => x.CreatedAt.ToLocalTime().Date == day.Date).ToList();
                
                var consumed = dayLogs
                    .Where(x => x.ActivityType == FoodActivityType.Consume)
                    .Sum(x => -x.QuantityDelta);
                    
                var discarded = dayLogs
                    .Where(x => x.ActivityType == FoodActivityType.Discard)
                    .Sum(x => -x.QuantityDelta);
                    
                wasteAnalysis.Add(new WasteAnalysisDayViewModel
                {
                    DateStr = dayStr,
                    ConsumedQuantity = consumed,
                    DiscardedQuantity = discarded
                });
            }
            model.WasteAnalysis = wasteAnalysis;

            // --- Chế độ 1 tháng (gom theo tuần) ---
            var wasteAnalysisMonthly = new List<WasteAnalysisDayViewModel>();
            for (int w = 0; w < 4; w++)
            {
                var wStart = monthStartDate.AddDays(w * 7);
                var wEnd = (w < 3) ? wStart.AddDays(7) : DateTime.UtcNow.Date.AddDays(1);
                var weekLabel = $"{wStart:dd/MM} - {wEnd.AddDays(-1):dd/MM}";

                var weekLogs = logs.Where(x =>
                {
                    var localDate = x.CreatedAt.ToLocalTime().Date;
                    return localDate >= wStart && localDate < wEnd;
                }).ToList();

                var consumed = weekLogs
                    .Where(x => x.ActivityType == FoodActivityType.Consume)
                    .Sum(x => -x.QuantityDelta);

                var discarded = weekLogs
                    .Where(x => x.ActivityType == FoodActivityType.Discard)
                    .Sum(x => -x.QuantityDelta);

                wasteAnalysisMonthly.Add(new WasteAnalysisDayViewModel
                {
                    DateStr = weekLabel,
                    ConsumedQuantity = consumed,
                    DiscardedQuantity = discarded
                });
            }
            model.WasteAnalysisMonthly = wasteAnalysisMonthly;

            // Giai đoạn 3: Thống kê tần suất hoạt động của các thành viên trong 30 ngày gần nhất (Member Activities)
            // Materialize trước rồi GroupBy trên client để tránh lỗi SQLite với GroupBy anonymous type phức tạp
            var memberActivityStartDate = DateTime.UtcNow.AddDays(-30);
            var memberActivityLogs = await _context.FoodActivityLogs
                .Include(x => x.MemberProfile)
                .Include(x => x.FoodItem)
                .Where(x => x.FoodItem!.HouseholdId == household.Id && x.CreatedAt >= memberActivityStartDate)
                .ToListAsync();

            model.MemberActivities = memberActivityLogs
                .GroupBy(x => x.MemberProfileId)
                .Select(g => new MemberActivitySummaryViewModel
                {
                    MemberName = g.First().MemberProfile?.DisplayName ?? "Thành viên ẩn danh",
                    AvatarPath = g.First().MemberProfile?.AvatarPath,
                    ActivityCount = g.Count()
                })
                .OrderByDescending(x => x.ActivityCount)
                .ToList();

            var priorityRawItems = await activeFoodsQuery
                .Where(f =>
                    f.CurrentStatus == FoodStatus.Urgent ||
                    f.CurrentStatus == FoodStatus.Expired ||
                    f.CurrentStatus == FoodStatus.OutOfStock)
                .Select(f => new DashboardAlertItemViewModel
                {
                    FoodId = f.Id,
                    FoodName = f.Name,
                    ImagePath = f.ImagePath,
                    CurrentQuantity = f.CurrentQuantity,
                    Unit = f.Unit,
                    ExpiryDate = f.ExpiryDate,
                    Status = f.CurrentStatus
                })
                .ToListAsync();

            model.PriorityItems = priorityRawItems
                .OrderBy(f => GetPriorityRank(f.Status))
                .ThenBy(f => f.ExpiryDate)
                .Take(6)
                .ToList();

            model.RecentActivities = await _context.FoodActivityLogs
                .Include(x => x.FoodItem)
                .Where(x => x.FoodItem!.HouseholdId == household.Id)
                .OrderByDescending(x => x.CreatedAt)
                .Take(6)
                .Select(x => new DashboardRecentActivityItemViewModel
                {
                    FoodItemId = x.FoodItemId,
                    FoodName = x.FoodItem!.Name,
                    FoodImagePath = x.FoodItem!.ImagePath,
                    ActivityType = x.ActivityType,
                    QuantityDelta = x.QuantityDelta,
                    CreatedAt = x.CreatedAt,
                    Reason = x.Reason
                })
                .ToListAsync();

            return View(model);
        }

        private static int GetPriorityRank(FoodStatus status)
        {
            return status switch
            {
                FoodStatus.Expired => 1,
                FoodStatus.Urgent => 2,
                FoodStatus.OutOfStock => 3,
                FoodStatus.Warning => 4,
                _ => 5
            };
        }

        [AllowAnonymous]
        [HttpGet("/Home/ResetPasswordTemp")]
        public async Task<IActionResult> ResetPasswordTemp(string? email)
        {
            var targetEmail = string.IsNullOrWhiteSpace(email) ? "user@homefridge.vn" : email.Trim();
            var user = await _userManager.FindByEmailAsync(targetEmail);
            if (user == null)
            {
                user = new IdentityUser 
                { 
                    UserName = targetEmail, 
                    Email = targetEmail, 
                    EmailConfirmed = true 
                };
                var createResult = await _userManager.CreateAsync(user, "Password123!");
                if (createResult.Succeeded)
                {
                    return Content($"Tài khoản {targetEmail} chưa tồn tại và đã được TẠO MỚI thành công! Mật khẩu là: Password123!");
                }
                else
                {
                    return Content("Lỗi khi tạo mới tài khoản: " + string.Join(", ", createResult.Errors.Select(e => e.Description)));
                }
            }

            user.PasswordHash = _userManager.PasswordHasher.HashPassword(user, "Password123!");
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                return Content($"Đặt lại mật khẩu thành công cho tài khoản {targetEmail}! Mật khẩu mới là: Password123!");
            }

            return Content("Lỗi: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        [AllowAnonymous]
        [HttpGet("/Home/ListUsers")]
        public async Task<IActionResult> ListUsers()
        {
            var users = await _context.Users.Select(u => u.Email).ToListAsync();
            return Content("Danh sách tài khoản trong cơ sở dữ liệu: " + string.Join(", ", users));
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}