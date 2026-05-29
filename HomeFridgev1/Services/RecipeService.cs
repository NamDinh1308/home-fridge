using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HomeFridgev1.Data;
using HomeFridgev1.Models.Entities;
using HomeFridgev1.Models.Enums;
using HomeFridgev1.Models.ViewModels;
using HomeFridgev1.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HomeFridgev1.Services
{
    public class RecipeService : IRecipeService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFoodStatusService _foodStatusService;

        public RecipeService(ApplicationDbContext context, IFoodStatusService foodStatusService)
        {
            _context = context;
            _foodStatusService = foodStatusService;
        }

        public async Task<List<Recipe>> GetRecipesAsync(int householdId)
        {
            return await _context.Recipes
                .Include(r => r.Ingredients)
                .Where(r => r.HouseholdId == householdId)
                .OrderBy(r => r.Name)
                .ToListAsync();
        }

        public async Task<Recipe?> GetRecipeDetailsAsync(int recipeId, int householdId)
        {
            return await _context.Recipes
                .Include(r => r.Ingredients)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId);
        }

        public async Task CreateRecipeAsync(Recipe recipe, int householdId)
        {
            recipe.HouseholdId = householdId;
            recipe.CreatedAt = DateTime.UtcNow;
            _context.Recipes.Add(recipe);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateRecipeAsync(Recipe recipe, int householdId)
        {
            var existingRecipe = await _context.Recipes
                .Include(r => r.Ingredients)
                .FirstOrDefaultAsync(r => r.Id == recipe.Id && r.HouseholdId == householdId);

            if (existingRecipe != null)
            {
                existingRecipe.Name = recipe.Name;
                existingRecipe.Description = recipe.Description;
                existingRecipe.Instructions = recipe.Instructions;
                existingRecipe.ImagePath = recipe.ImagePath;

                // Xóa nguyên liệu cũ
                _context.RecipeIngredients.RemoveRange(existingRecipe.Ingredients);

                // Thêm nguyên liệu mới
                foreach (var ingredient in recipe.Ingredients)
                {
                    existingRecipe.Ingredients.Add(new RecipeIngredient
                    {
                        FoodName = ingredient.FoodName,
                        RequiredQuantity = ingredient.RequiredQuantity,
                        Unit = ingredient.Unit
                    });
                }

                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteRecipeAsync(int recipeId, int householdId)
        {
            var recipe = await _context.Recipes
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId);

            if (recipe != null)
            {
                _context.Recipes.Remove(recipe);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<RecipeSuggestionViewModel>> GetSuggestionsAsync(int householdId)
        {
            var recipes = await _context.Recipes
                .Include(r => r.Ingredients)
                .Where(r => r.HouseholdId == householdId)
                .ToListAsync();

            var foodItems = await _context.FoodItems
                .Where(f => f.HouseholdId == householdId && !f.IsArchived && f.CurrentQuantity > 0)
                .ToListAsync();

            var settings = await _context.HouseholdSettings
                .FirstOrDefaultAsync(s => s.HouseholdId == householdId) 
                ?? new HouseholdSettings { HouseholdId = householdId };

            var suggestions = new List<RecipeSuggestionViewModel>();

            foreach (var recipe in recipes)
            {
                var ingredientStatuses = new List<RecipeIngredientStatusViewModel>();
                bool hasExpiring = false;

                foreach (var ingredient in recipe.Ingredients)
                {
                    // So khớp tên nguyên liệu không phân biệt hoa thường
                    var matchedFoods = foodItems
                        .Where(f => f.Name.Contains(ingredient.FoodName, StringComparison.OrdinalIgnoreCase) ||
                                    ingredient.FoodName.Contains(f.Name, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    decimal availableQuantity = matchedFoods.Sum(f => f.CurrentQuantity);
                    
                    // Kiểm tra xem nguyên liệu khớp có cái nào sắp hết hạn không
                    if (matchedFoods.Any(f => f.CurrentStatus == FoodStatus.Warning || f.CurrentStatus == FoodStatus.Urgent))
                    {
                        hasExpiring = true;
                    }

                    string status = "Missing";
                    if (availableQuantity >= ingredient.RequiredQuantity)
                    {
                        status = "Match";
                    }
                    else if (availableQuantity > 0)
                    {
                        status = "Partial";
                    }

                    ingredientStatuses.Add(new RecipeIngredientStatusViewModel
                    {
                        FoodName = ingredient.FoodName,
                        RequiredQuantity = ingredient.RequiredQuantity,
                        Unit = ingredient.Unit,
                        AvailableQuantity = availableQuantity,
                        Status = status
                    });
                }

                // Tính % so khớp
                int matchedCount = ingredientStatuses.Count(i => i.Status == "Match");
                int totalCount = recipe.Ingredients.Count;
                int matchPercentage = totalCount > 0 ? (matchedCount * 100 / totalCount) : 100;
                bool isCookable = matchedCount == totalCount;

                suggestions.Add(new RecipeSuggestionViewModel
                {
                    RecipeId = recipe.Id,
                    RecipeName = recipe.Name,
                    Description = recipe.Description,
                    ImagePath = recipe.ImagePath,
                    MatchPercentage = matchPercentage,
                    IsCookable = isCookable,
                    HasExpiringIngredients = hasExpiring,
                    IngredientsStatus = ingredientStatuses
                });
            }

            // Sắp xếp các món ăn:
            // 1. Nấu được ngay & có đồ sắp hết hạn
            // 2. Nấu được ngay & không có đồ sắp hết hạn
            // 3. Thiếu một phần (giảm dần theo % khớp)
            // 4. Thiếu hoàn toàn
            return suggestions
                .OrderByDescending(s => s.IsCookable)
                .ThenByDescending(s => s.HasExpiringIngredients)
                .ThenByDescending(s => s.MatchPercentage)
                .ThenBy(s => s.RecipeName)
                .ToList();
        }

        public async Task<bool> CookRecipeAsync(int recipeId, int householdId, int memberProfileId)
        {
            var recipe = await _context.Recipes
                .Include(r => r.Ingredients)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId);

            if (recipe == null) return false;

            var foodItems = await _context.FoodItems
                .Where(f => f.HouseholdId == householdId && !f.IsArchived)
                .ToListAsync();

            var settings = await _context.HouseholdSettings
                .FirstOrDefaultAsync(s => s.HouseholdId == householdId)
                ?? new HouseholdSettings { HouseholdId = householdId };

            // Kiểm tra xem thực tế có đủ nguyên liệu để nấu hay không trước khi trừ kho
            foreach (var ingredient in recipe.Ingredients)
            {
                var matchedFoods = foodItems
                    .Where(f => f.Name.Contains(ingredient.FoodName, StringComparison.OrdinalIgnoreCase) ||
                                ingredient.FoodName.Contains(f.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                decimal totalQty = matchedFoods.Sum(f => f.CurrentQuantity);
                if (totalQty < ingredient.RequiredQuantity)
                {
                    // Không đủ nguyên liệu để thực hiện nấu món này
                    return false;
                }
            }

            // Thực hiện trừ kho
            foreach (var ingredient in recipe.Ingredients)
            {
                var matchedFoods = foodItems
                    .Where(f => f.Name.Contains(ingredient.FoodName, StringComparison.OrdinalIgnoreCase) ||
                                ingredient.FoodName.Contains(f.Name, StringComparison.OrdinalIgnoreCase))
                    // Món sắp hết hạn dùng trước (Warning/Urgent trước, Normal sau, Expired trước nữa nếu có)
                    .OrderBy(f => f.ExpiryDate)
                    .ToList();

                decimal quantityToDeduct = ingredient.RequiredQuantity;

                foreach (var food in matchedFoods)
                {
                    if (quantityToDeduct <= 0) break;

                    decimal quantityBefore = food.CurrentQuantity;
                    decimal deductAmount = Math.Min(food.CurrentQuantity, quantityToDeduct);
                    
                    food.CurrentQuantity -= deductAmount;
                    quantityToDeduct -= deductAmount;

                    // Tính lại trạng thái
                    food.CurrentStatus = _foodStatusService.CalculateStatus(food.CurrentQuantity, food.ExpiryDate, settings);
                    if (food.CurrentQuantity <= 0)
                    {
                        food.OutOfStockSince = DateTime.UtcNow;
                    }
                    food.LastStatusChangedAt = DateTime.UtcNow;
                    food.LastUpdatedByMemberId = memberProfileId;

                    // Ghi FoodActivityLog
                    var activityLog = new FoodActivityLog
                    {
                        FoodItemId = food.Id,
                        MemberProfileId = memberProfileId,
                        ActivityType = FoodActivityType.Consume,
                        QuantityBefore = quantityBefore,
                        QuantityDelta = -deductAmount,
                        QuantityAfter = food.CurrentQuantity,
                        Reason = $"Nấu món {recipe.Name}",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.FoodActivityLogs.Add(activityLog);
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task AddMissingIngredientsToShoppingListAsync(int recipeId, int householdId, int memberProfileId)
        {
            var recipe = await _context.Recipes
                .Include(r => r.Ingredients)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId);

            if (recipe == null) return;

            var foodItems = await _context.FoodItems
                .Where(f => f.HouseholdId == householdId && !f.IsArchived)
                .ToListAsync();

            foreach (var ingredient in recipe.Ingredients)
            {
                var matchedFoods = foodItems
                    .Where(f => f.Name.Contains(ingredient.FoodName, StringComparison.OrdinalIgnoreCase) ||
                                ingredient.FoodName.Contains(f.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                decimal totalQty = matchedFoods.Sum(f => f.CurrentQuantity);

                if (totalQty < ingredient.RequiredQuantity)
                {
                    decimal missingQty = ingredient.RequiredQuantity - totalQty;

                    // Kiểm tra xem mặt hàng này đã có trong danh sách mua sắm hiện tại (chưa mua) hay chưa
                    var existingShoppingItem = await _context.ShoppingItems
                        .FirstOrDefaultAsync(s => s.HouseholdId == householdId &&
                                                  s.Name.ToLower() == ingredient.FoodName.ToLower() &&
                                                  !s.IsPurchased);

                    if (existingShoppingItem != null)
                    {
                        // Cộng dồn số lượng thiếu
                        existingShoppingItem.Quantity += missingQty;
                    }
                    else
                    {
                        // Thêm mới vào danh sách mua sắm
                        var shoppingItem = new ShoppingItem
                        {
                            HouseholdId = householdId,
                            Name = ingredient.FoodName,
                            Quantity = missingQty,
                            Unit = ingredient.Unit,
                            AddedByMemberId = memberProfileId,
                            CreatedAt = DateTime.UtcNow,
                            IsPurchased = false
                        };
                        _context.ShoppingItems.Add(shoppingItem);
                    }
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
