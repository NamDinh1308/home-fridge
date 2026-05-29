using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HomeFridgev1.Data;
using HomeFridgev1.Models.Entities;
using HomeFridgev1.Models.ViewModels;
using HomeFridgev1.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeFridgev1.Controllers
{
    [Authorize]
    public class RecipesController : Controller
    {
        private readonly IRecipeService _recipeService;
        private readonly ICurrentMemberService _currentMemberService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IHouseholdInitializationService _householdInitializationService;
        private readonly ApplicationDbContext _context;

        public RecipesController(
            IRecipeService recipeService,
            ICurrentMemberService currentMemberService,
            UserManager<IdentityUser> userManager,
            IHouseholdInitializationService householdInitializationService,
            ApplicationDbContext context)
        {
            _recipeService = recipeService;
            _currentMemberService = currentMemberService;
            _userManager = userManager;
            _householdInitializationService = householdInitializationService;
            _context = context;
        }

        private async Task<Household?> GetCurrentHouseholdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;

            await _householdInitializationService.EnsureInitializedAsync(
                user,
                $"Nhà của {user.Email}",
                user.Email ?? "Chủ gia đình");

            return await _context.Households
                .FirstOrDefaultAsync(h => h.OwnerUserId == user.Id);
        }

        private bool CheckMemberContext(out int memberId, out IActionResult? redirectResult)
        {
            var currentMemberId = _currentMemberService.GetCurrentMemberId();
            if (!currentMemberId.HasValue)
            {
                memberId = 0;
                redirectResult = RedirectToAction("Select", "Members", new { returnUrl = Request.Path + Request.QueryString });
                return false;
            }

            memberId = currentMemberId.Value;
            redirectResult = null;
            return true;
        }

        // 1. Danh sách công thức
        public async Task<IActionResult> Index()
        {
            if (!CheckMemberContext(out _, out var redirect)) return redirect!;

            var household = await GetCurrentHouseholdAsync();
            if (household == null) return RedirectToAction("Index", "Home");

            var recipes = await _recipeService.GetRecipesAsync(household.Id);
            var model = new RecipeListViewModel { Recipes = recipes };

            return View(model);
        }

        // 2. Gợi ý món ăn thông minh
        public async Task<IActionResult> Suggestions()
        {
            if (!CheckMemberContext(out _, out var redirect)) return redirect!;

            var household = await GetCurrentHouseholdAsync();
            if (household == null) return RedirectToAction("Index", "Home");

            var suggestions = await _recipeService.GetSuggestionsAsync(household.Id);
            return View(suggestions);
        }

        // 3. Tạo mới công thức - GET
        public IActionResult Create()
        {
            if (!CheckMemberContext(out _, out var redirect)) return redirect!;

            var model = new RecipeFormViewModel
            {
                Ingredients = new List<RecipeIngredientFormModel>
                {
                    new RecipeIngredientFormModel() // Một dòng nguyên liệu trống mặc định
                }
            };
            return View(model);
        }

        // 3. Tạo mới công thức - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RecipeFormViewModel model)
        {
            if (!CheckMemberContext(out _, out var redirect)) return redirect!;

            var household = await GetCurrentHouseholdAsync();
            if (household == null) return RedirectToAction("Index", "Home");

            // Lọc bỏ các dòng nguyên liệu rỗng người dùng chưa nhập tên nguyên liệu
            model.Ingredients = model.Ingredients
                .Where(i => !string.IsNullOrWhiteSpace(i.FoodName))
                .ToList();

            if (model.Ingredients.Count == 0)
            {
                ModelState.AddModelError("", "Vui lòng nhập ít nhất một nguyên liệu.");
            }

            if (ModelState.IsValid)
            {
                var recipe = new Recipe
                {
                    Name = model.Name,
                    Description = model.Description,
                    Instructions = model.Instructions,
                    ImagePath = model.ImagePath,
                    Ingredients = model.Ingredients.Select(i => new RecipeIngredient
                    {
                        FoodName = i.FoodName.Trim(),
                        RequiredQuantity = i.RequiredQuantity,
                        Unit = i.Unit.Trim()
                    }).ToList()
                };

                await _recipeService.CreateRecipeAsync(recipe, household.Id);
                TempData["SuccessMessage"] = $"Đã thêm thành công công thức món '{recipe.Name}'";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // 4. Sửa công thức - GET
        public async Task<IActionResult> Edit(int id)
        {
            if (!CheckMemberContext(out _, out var redirect)) return redirect!;

            var household = await GetCurrentHouseholdAsync();
            if (household == null) return RedirectToAction("Index", "Home");

            var recipe = await _recipeService.GetRecipeDetailsAsync(id, household.Id);
            if (recipe == null) return NotFound();

            var model = new RecipeFormViewModel
            {
                Id = recipe.Id,
                Name = recipe.Name,
                Description = recipe.Description,
                Instructions = recipe.Instructions,
                ImagePath = recipe.ImagePath,
                Ingredients = recipe.Ingredients.Select(i => new RecipeIngredientFormModel
                {
                    FoodName = i.FoodName,
                    RequiredQuantity = i.RequiredQuantity,
                    Unit = i.Unit
                }).ToList()
            };

            return View(model);
        }

        // 4. Sửa công thức - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(RecipeFormViewModel model)
        {
            if (!CheckMemberContext(out _, out var redirect)) return redirect!;

            var household = await GetCurrentHouseholdAsync();
            if (household == null) return RedirectToAction("Index", "Home");

            model.Ingredients = model.Ingredients
                .Where(i => !string.IsNullOrWhiteSpace(i.FoodName))
                .ToList();

            if (model.Ingredients.Count == 0)
            {
                ModelState.AddModelError("", "Vui lòng nhập ít nhất một nguyên liệu.");
            }

            if (ModelState.IsValid)
            {
                var recipe = new Recipe
                {
                    Id = model.Id,
                    Name = model.Name,
                    Description = model.Description,
                    Instructions = model.Instructions,
                    ImagePath = model.ImagePath,
                    Ingredients = model.Ingredients.Select(i => new RecipeIngredient
                    {
                        FoodName = i.FoodName.Trim(),
                        RequiredQuantity = i.RequiredQuantity,
                        Unit = i.Unit.Trim()
                    }).ToList()
                };

                await _recipeService.UpdateRecipeAsync(recipe, household.Id);
                TempData["SuccessMessage"] = $"Đã cập nhật công thức món '{recipe.Name}'";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // 5. Xóa công thức - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!CheckMemberContext(out _, out var redirect)) return redirect!;

            var household = await GetCurrentHouseholdAsync();
            if (household == null) return RedirectToAction("Index", "Home");

            await _recipeService.DeleteRecipeAsync(id, household.Id);
            TempData["SuccessMessage"] = "Đã xóa công thức nấu ăn thành công.";
            return RedirectToAction(nameof(Index));
        }

        // 6. Nấu ăn trừ kho thực tế
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cook(int id)
        {
            if (!CheckMemberContext(out var memberId, out var redirect)) return redirect!;

            var household = await GetCurrentHouseholdAsync();
            if (household == null) return RedirectToAction("Index", "Home");

            var success = await _recipeService.CookRecipeAsync(id, household.Id, memberId);
            if (success)
            {
                TempData["SuccessMessage"] = "Đã thực hiện nấu ăn thành công! Hệ thống đã trừ các nguyên liệu tương ứng trong tủ lạnh.";
            }
            else
            {
                TempData["ErrorMessage"] = "Không thể thực hiện nấu ăn. Vui lòng kiểm tra lại nguyên liệu trong tủ lạnh!";
            }

            return RedirectToAction(nameof(Suggestions));
        }

        // 7. Thêm nguyên liệu thiếu vào danh sách mua sắm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMissingToShoppingList(int id)
        {
            if (!CheckMemberContext(out var memberId, out var redirect)) return redirect!;

            var household = await GetCurrentHouseholdAsync();
            if (household == null) return RedirectToAction("Index", "Home");

            await _recipeService.AddMissingIngredientsToShoppingListAsync(id, household.Id, memberId);
            TempData["SuccessMessage"] = "Đã thêm các nguyên liệu còn thiếu vào danh sách đi chợ.";

            return RedirectToAction(nameof(Suggestions));
        }
    }
}
