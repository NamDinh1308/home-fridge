using Microsoft.AspNetCore.Identity;

namespace HomeFridgev1.Services.Interfaces
{
    public interface IHouseholdInitializationService
    {
        Task EnsureInitializedAsync(IdentityUser user, string householdName, string ownerDisplayName);
    }
}