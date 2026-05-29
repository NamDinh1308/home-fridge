using HomeFridgev1.Data;
using HomeFridgev1.Models.Entities;
using HomeFridgev1.Models.Enums;
using HomeFridgev1.Models.ViewModels;
using HomeFridgev1.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HomeFridgev1.Controllers
{
    [Authorize]
    public class MembersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ICurrentMemberService _currentMemberService;
        private readonly IHouseholdInitializationService _householdInitializationService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public MembersController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ICurrentMemberService currentMemberService,
            IHouseholdInitializationService householdInitializationService,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _currentMemberService = currentMemberService;
            _householdInitializationService = householdInitializationService;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var currentMemberId = _currentMemberService.GetCurrentMemberId();

            var members = await _context.MemberProfiles
                .Where(m => m.HouseholdId == household.Id)
                .OrderByDescending(m => m.Role == MemberRole.Owner)
                .ThenByDescending(m => m.IsActive)
                .ThenBy(m => m.DisplayName)
                .ToListAsync();

            var model = new MemberIndexViewModel
            {
                HouseholdName = household.Name,
                CurrentMemberId = currentMemberId,
                TotalMembers = members.Count,
                ActiveMembers = members.Count(m => m.IsActive),
                OwnerName = members.FirstOrDefault(m => m.Role == MemberRole.Owner)?.DisplayName,
                Items = members.Select(m => new MemberIndexItemViewModel
                {
                    Id = m.Id,
                    DisplayName = m.DisplayName,
                    AvatarPath = m.AvatarPath,
                    Role = m.Role,
                    Email = m.Email,
                    IsActive = m.IsActive,
                    ReceiveEmailNotification = m.ReceiveEmailNotification,
                    ReceiveExpiryNotification = m.ReceiveExpiryNotification,
                    ReceiveOutOfStockNotification = m.ReceiveOutOfStockNotification,
                    CreatedAt = m.CreatedAt,
                    IsCurrentMember = currentMemberId.HasValue && currentMemberId.Value == m.Id
                }).ToList()
            };

            return View(model);
        }

        public async Task<IActionResult> Create()
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var accessRedirect = await ValidateOwnerAccessAsync(household.Id);
            if (accessRedirect != null)
            {
                return accessRedirect;
            }

            var model = new MemberFormViewModel
            {
                Role = MemberRole.Member,
                ReceiveEmailNotification = true,
                ReceiveExpiryNotification = true,
                ReceiveOutOfStockNotification = true,
                IsActive = true
            };

            PopulateRoleOptions(model, includeOwner: false);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MemberFormViewModel model)
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var accessRedirect = await ValidateOwnerAccessAsync(household.Id);
            if (accessRedirect != null)
            {
                return accessRedirect;
            }

            if (model.Role == MemberRole.Owner)
            {
                ModelState.AddModelError(nameof(model.Role), "Không thể tạo thêm thành viên có vai trò Owner.");
            }

            if (await DisplayNameExistsAsync(household.Id, model.DisplayName))
            {
                ModelState.AddModelError(nameof(model.DisplayName), "Tên thành viên đã tồn tại trong household.");
            }

            if (!ModelState.IsValid)
            {
                PopulateRoleOptions(model, includeOwner: false);
                return View(model);
            }

            var avatarPath = await HandleAvatarUploadAsync(model.AvatarFile, model.AvatarPath);

            var member = new MemberProfile
            {
                HouseholdId = household.Id,
                DisplayName = model.DisplayName.Trim(),
                AvatarPath = avatarPath,
                Role = model.Role,
                Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                ReceiveEmailNotification = model.ReceiveEmailNotification,
                ReceiveExpiryNotification = model.ReceiveExpiryNotification,
                ReceiveOutOfStockNotification = model.ReceiveOutOfStockNotification,
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };

            _context.MemberProfiles.Add(member);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã thêm thành viên mới thành công.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var accessRedirect = await ValidateOwnerAccessAsync(household.Id);
            if (accessRedirect != null)
            {
                return accessRedirect;
            }

            var member = await _context.MemberProfiles
                .FirstOrDefaultAsync(m => m.Id == id && m.HouseholdId == household.Id);

            if (member == null)
            {
                return NotFound();
            }

            var model = new MemberFormViewModel
            {
                Id = member.Id,
                DisplayName = member.DisplayName,
                AvatarPath = member.AvatarPath,
                Role = member.Role,
                Email = member.Email,
                ReceiveEmailNotification = member.ReceiveEmailNotification,
                ReceiveExpiryNotification = member.ReceiveExpiryNotification,
                ReceiveOutOfStockNotification = member.ReceiveOutOfStockNotification,
                IsActive = member.IsActive,
                IsOwner = member.Role == MemberRole.Owner
            };

            PopulateRoleOptions(model, includeOwner: member.Role == MemberRole.Owner);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MemberFormViewModel model)
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var accessRedirect = await ValidateOwnerAccessAsync(household.Id);
            if (accessRedirect != null)
            {
                return accessRedirect;
            }

            var member = await _context.MemberProfiles
                .FirstOrDefaultAsync(m => m.Id == model.Id && m.HouseholdId == household.Id);

            if (member == null)
            {
                return NotFound();
            }

            var isOwner = member.Role == MemberRole.Owner;
            model.IsOwner = isOwner;

            if (isOwner && model.Role != MemberRole.Owner)
            {
                ModelState.AddModelError(nameof(model.Role), "Không thể thay đổi vai trò của Owner.");
            }

            if (isOwner && !model.IsActive)
            {
                ModelState.AddModelError(nameof(model.IsActive), "Không thể ngừng hoạt động Owner.");
            }

            if (await DisplayNameExistsAsync(household.Id, model.DisplayName, model.Id))
            {
                ModelState.AddModelError(nameof(model.DisplayName), "Tên thành viên đã tồn tại trong household.");
            }

            if (!ModelState.IsValid)
            {
                PopulateRoleOptions(model, includeOwner: isOwner);
                return View(model);
            }

            var avatarPath = await HandleAvatarUploadAsync(model.AvatarFile, model.AvatarPath);

            member.DisplayName = model.DisplayName.Trim();
            member.AvatarPath = avatarPath;
            member.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
            member.ReceiveEmailNotification = model.ReceiveEmailNotification;
            member.ReceiveExpiryNotification = model.ReceiveExpiryNotification;
            member.ReceiveOutOfStockNotification = model.ReceiveOutOfStockNotification;
            member.IsActive = model.IsActive;
            member.UpdatedAt = DateTime.UtcNow;

            if (!isOwner)
            {
                member.Role = model.Role;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã cập nhật thông tin thành viên.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Select(string? returnUrl = null)
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var members = await _context.MemberProfiles
                .Where(m => m.HouseholdId == household.Id && m.IsActive)
                .OrderByDescending(m => m.Role == MemberRole.Owner)
                .ThenBy(m => m.DisplayName)
                .ToListAsync();

            var model = new SelectMemberViewModel
            {
                HouseholdName = household.Name,
                ReturnUrl = returnUrl,
                Members = members.Select(m => new SelectMemberItemViewModel
                {
                    Id = m.Id,
                    DisplayName = m.DisplayName,
                    AvatarPath = m.AvatarPath,
                    Role = m.Role.ToString()
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Select(int? id, int? memberId, string? returnUrl = null)
        {
            var targetId = id ?? memberId;
            if (!targetId.HasValue)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin thành viên.";
                return RedirectToAction(nameof(Index));
            }

            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var member = await _context.MemberProfiles
                .FirstOrDefaultAsync(m => m.Id == targetId.Value && m.HouseholdId == household.Id && m.IsActive);

            if (member == null)
            {
                TempData["ErrorMessage"] = "Không thể chọn thành viên này.";
                return RedirectToAction(nameof(Index));
            }

            _currentMemberService.SetCurrentMemberId(member.Id);
            TempData["SuccessMessage"] = $"Đã chuyển sang thành viên: {member.DisplayName}.";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            var household = await GetCurrentHouseholdAsync();
            if (household == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var accessRedirect = await ValidateOwnerAccessAsync(household.Id);
            if (accessRedirect != null)
            {
                return accessRedirect;
            }

            var member = await _context.MemberProfiles
                .FirstOrDefaultAsync(m => m.Id == id && m.HouseholdId == household.Id);

            if (member == null)
            {
                return NotFound();
            }

            if (member.Role == MemberRole.Owner)
            {
                TempData["ErrorMessage"] = "Không thể ngừng hoạt động Owner.";
                return RedirectToAction(nameof(Index));
            }

            var currentMemberId = _currentMemberService.GetCurrentMemberId();
            if (currentMemberId.HasValue && currentMemberId.Value == member.Id)
            {
                TempData["ErrorMessage"] = "Không thể ngừng hoạt động thành viên đang được chọn. Hãy chuyển sang thành viên khác trước.";
                return RedirectToAction(nameof(Index));
            }

            member.IsActive = false;
            member.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã ngừng hoạt động thành viên: {member.DisplayName}.";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Clear()
        {
            _currentMemberService.ClearCurrentMember();
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

        private async Task<bool> DisplayNameExistsAsync(int householdId, string displayName, int? ignoreId = null)
        {
            var normalized = displayName.Trim();

            return await _context.MemberProfiles.AnyAsync(m =>
                m.HouseholdId == householdId &&
                m.DisplayName == normalized &&
                (!ignoreId.HasValue || m.Id != ignoreId.Value));
        }

        private async Task<string?> HandleAvatarUploadAsync(IFormFile? avatarFile, string? existingAvatarPath)
        {
            if (avatarFile == null || avatarFile.Length == 0)
            {
                return existingAvatarPath;
            }

            try
            {
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "members");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                if (!string.IsNullOrWhiteSpace(existingAvatarPath) && existingAvatarPath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                {
                    var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, existingAvatarPath.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(avatarFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await avatarFile.CopyToAsync(fileStream);
                }

                return "/uploads/members/" + uniqueFileName;
            }
            catch
            {
                return existingAvatarPath;
            }
        }

        private static void PopulateRoleOptions(MemberFormViewModel model, bool includeOwner)
        {
            var roles = Enum.GetValues<MemberRole>()
                .Where(r => includeOwner || r != MemberRole.Owner)
                .Select(r => new SelectListItem
                {
                    Value = r.ToString(),
                    Text = r.ToString(),
                    Selected = r == model.Role
                })
                .ToList();

            model.RoleOptions = roles;
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
                TempData["ErrorMessage"] = "Chỉ chủ hộ (Owner) mới có quyền thực hiện chức năng này.";
                return RedirectToAction(nameof(Index));
            }

            return null; // Access granted
        }
    }
}
