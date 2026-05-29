using HomeFridgev1.Data;
using HomeFridgev1.Models.ViewModels;
using HomeFridgev1.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeFridgev1.ViewComponents
{
    public class CurrentMemberViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentMemberService _currentMemberService;

        public CurrentMemberViewComponent(
            ApplicationDbContext context,
            ICurrentMemberService currentMemberService)
        {
            _context = context;
            _currentMemberService = currentMemberService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var memberId = _currentMemberService.GetCurrentMemberId();

            if (!memberId.HasValue)
            {
                return View(new CurrentMemberInfoViewModel());
            }

            var member = await _context.MemberProfiles
                .FirstOrDefaultAsync(m => m.Id == memberId.Value && m.IsActive);

            if (member == null)
            {
                return View(new CurrentMemberInfoViewModel());
            }

            var model = new CurrentMemberInfoViewModel
            {
                Id = member.Id,
                DisplayName = member.DisplayName,
                Role = member.Role.ToString(),
                AvatarPath = member.AvatarPath
            };

            return View(model);
        }
    }
}