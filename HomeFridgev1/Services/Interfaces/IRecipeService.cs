using System.Collections.Generic;
using System.Threading.Tasks;
using HomeFridgev1.Models.Entities;
using HomeFridgev1.Models.ViewModels;

namespace HomeFridgev1.Services.Interfaces
{
    public interface IRecipeService
    {
        Task<List<Recipe>> GetRecipesAsync(int householdId);
        Task<Recipe?> GetRecipeDetailsAsync(int recipeId, int householdId);
        Task CreateRecipeAsync(Recipe recipe, int householdId);
        Task UpdateRecipeAsync(Recipe recipe, int householdId);
        Task DeleteRecipeAsync(int recipeId, int householdId);

        Task<List<RecipeSuggestionViewModel>> GetSuggestionsAsync(int householdId);
        Task<bool> CookRecipeAsync(int recipeId, int householdId, int memberProfileId);
        Task AddMissingIngredientsToShoppingListAsync(int recipeId, int householdId, int memberProfileId);
    }
}
