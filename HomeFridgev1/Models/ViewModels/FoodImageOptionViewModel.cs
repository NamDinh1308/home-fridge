namespace HomeFridgev1.Models.ViewModels
{
    public class FoodImageOptionViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string CategoryKey { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
    }
}