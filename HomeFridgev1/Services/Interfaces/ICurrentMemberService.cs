namespace HomeFridgev1.Services.Interfaces
{
    public interface ICurrentMemberService
    {
        int? GetCurrentMemberId();
        void SetCurrentMemberId(int memberId);
        void ClearCurrentMember();
    }
}