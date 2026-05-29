namespace HomeFridgev1.Models.ViewModels
{
    public class CurrentMemberInfoViewModel
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
    }
}