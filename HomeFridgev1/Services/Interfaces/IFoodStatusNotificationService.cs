using HomeFridgev1.Models.Entities;
using HomeFridgev1.Models.Enums;

namespace HomeFridgev1.Services.Interfaces
{
    public interface IFoodStatusNotificationService
    {
        Task NotifyStatusTransitionAsync(
            FoodItem foodItem,
            FoodStatus oldStatus,
            FoodStatus newStatus,
            CancellationToken cancellationToken = default);
    }
}
