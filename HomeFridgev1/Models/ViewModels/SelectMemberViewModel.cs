namespace HomeFridgev1.Models.ViewModels
{
    public class SelectMemberViewModel
    {
        public string HouseholdName { get; set; } = string.Empty;
        public List<SelectMemberItemViewModel> Members { get; set; } = new();
        public string? ReturnUrl { get; set; }
    }

    public class SelectMemberItemViewModel
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
        public string Role { get; set; } = string.Empty;
    }
}