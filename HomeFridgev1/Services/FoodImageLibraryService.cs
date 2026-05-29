using System.Text.Json;
using HomeFridgev1.Models.ViewModels;
using HomeFridgev1.Services.Interfaces;

namespace HomeFridgev1.Services
{
    public class FoodImageLibraryService : IFoodImageLibraryService
    {
        private readonly IWebHostEnvironment _environment;

        public FoodImageLibraryService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<List<FoodImageOptionViewModel>> GetAllAsync()
        {
            var jsonPath = Path.Combine(_environment.ContentRootPath, "Data", "Seed", "food-image-library.json");

            if (!File.Exists(jsonPath))
            {
                return new List<FoodImageOptionViewModel>();
            }

            var json = await File.ReadAllTextAsync(jsonPath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var items = JsonSerializer.Deserialize<List<FoodImageOptionViewModel>>(json, options);

            return items ?? new List<FoodImageOptionViewModel>();
        }
    }
}