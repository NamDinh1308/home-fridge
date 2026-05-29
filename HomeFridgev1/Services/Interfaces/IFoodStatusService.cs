using HomeFridgev1.Models.Entities;
using HomeFridgev1.Models.Enums;
using System.Threading;
using System.Threading.Tasks;

namespace HomeFridgev1.Services.Interfaces
{
    public interface IFoodStatusService
    {
        FoodStatus CalculateStatus(decimal currentQuantity, DateTime expiryDate, HouseholdSettings settings);
        Task ApplyAutoHideRuleAsync(int householdId, CancellationToken cancellationToken = default);
    }
}