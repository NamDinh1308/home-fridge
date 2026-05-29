using HomeFridgev1.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace HomeFridgev1.Services
{
    public class CurrentMemberService : ICurrentMemberService
    {
        private const string SessionKey = "CurrentMemberId";
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentMemberService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int? GetCurrentMemberId()
        {
            return _httpContextAccessor.HttpContext?.Session.GetInt32(SessionKey);
        }

        public void SetCurrentMemberId(int memberId)
        {
            _httpContextAccessor.HttpContext?.Session.SetInt32(SessionKey, memberId);
        }

        public void ClearCurrentMember()
        {
            _httpContextAccessor.HttpContext?.Session.Remove(SessionKey);
        }
    }
}