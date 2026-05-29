using HomeFridgev1.Models.ViewModels;

namespace HomeFridgev1.Services.Interfaces
{
    public interface IFoodImageLibraryService
    {
        Task<List<FoodImageOptionViewModel>> GetAllAsync();
    }
}