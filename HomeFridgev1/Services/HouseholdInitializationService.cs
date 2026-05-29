using HomeFridgev1.Data;
using HomeFridgev1.Models.Entities;
using HomeFridgev1.Models.Enums;
using HomeFridgev1.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HomeFridgev1.Services
{
    public class HouseholdInitializationService : IHouseholdInitializationService
    {
        private readonly ApplicationDbContext _context;

        public HouseholdInitializationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task EnsureInitializedAsync(IdentityUser user, string householdName, string ownerDisplayName)
        {
            var existingHousehold = await _context.Households
                .Include(h => h.Settings)
                .Include(h => h.MemberProfiles)
                .FirstOrDefaultAsync(h => h.OwnerUserId == user.Id);

            int householdId;

            if (existingHousehold == null)
            {
                var household = new Household
                {
                    Name = string.IsNullOrWhiteSpace(householdName) ? $"Nhà của {ownerDisplayName}" : householdName,
                    OwnerUserId = user.Id,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.Households.Add(household);
                await _context.SaveChangesAsync();

                householdId = household.Id;

                var settings = new HouseholdSettings
                {
                    HouseholdId = household.Id,
                    UrgentDaysBeforeExpiry = 1,
                    WarningDaysBeforeExpiry = 3,
                    EnableEmailNotifications = true,
                    EnableOutOfStockNotifications = true,
                    EnableExpiryNotifications = true,
                    DailyCheckTime = "08:00",
                    CreatedAt = DateTime.UtcNow
                };

                _context.HouseholdSettings.Add(settings);

                var ownerMember = new MemberProfile
                {
                    HouseholdId = household.Id,
                    DisplayName = string.IsNullOrWhiteSpace(ownerDisplayName) ? "Chủ gia đình" : ownerDisplayName,
                    Email = user.Email,
                    Role = MemberRole.Owner,
                    ReceiveEmailNotification = true,
                    ReceiveExpiryNotification = true,
                    ReceiveOutOfStockNotification = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.MemberProfiles.Add(ownerMember);

                var defaultCategories = new List<Category>
                {
                    new Category { HouseholdId = household.Id, Name = "Thịt/Cá", SortOrder = 1, IsDefault = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Category { HouseholdId = household.Id, Name = "Rau củ", SortOrder = 2, IsDefault = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Category { HouseholdId = household.Id, Name = "Trái cây", SortOrder = 3, IsDefault = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Category { HouseholdId = household.Id, Name = "Đồ uống", SortOrder = 4, IsDefault = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Category { HouseholdId = household.Id, Name = "Đồ khô", SortOrder = 5, IsDefault = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Category { HouseholdId = household.Id, Name = "Sữa", SortOrder = 6, IsDefault = true, IsActive = true, CreatedAt = DateTime.UtcNow }
                };

                var defaultLocations = new List<StorageLocation>
                {
                    new StorageLocation { HouseholdId = household.Id, Name = "Ngăn đông", SortOrder = 1, IsDefault = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new StorageLocation { HouseholdId = household.Id, Name = "Ngăn mát", SortOrder = 2, IsDefault = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new StorageLocation { HouseholdId = household.Id, Name = "Tủ khô", SortOrder = 3, IsDefault = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new StorageLocation { HouseholdId = household.Id, Name = "Bếp", SortOrder = 4, IsDefault = true, IsActive = true, CreatedAt = DateTime.UtcNow }
                };

                _context.Categories.AddRange(defaultCategories);
                _context.StorageLocations.AddRange(defaultLocations);

                await _context.SaveChangesAsync();
            }
            else
            {
                householdId = existingHousehold.Id;
            }

            // Seed default recipes if none exist
            var hasRecipes = await _context.Recipes.AnyAsync(r => r.HouseholdId == householdId);
            if (!hasRecipes)
            {
                var defaultRecipes = new List<Recipe>
                {
                    new Recipe
                    {
                        HouseholdId = householdId,
                        Name = "Trứng chiên hành tây",
                        Description = "Món ăn đơn giản, đưa cơm và rất nhanh chóng cho bữa cơm gia đình.",
                        Instructions = "1. Đập trứng vào bát, đánh đều với gia vị.\n2. Thái múi cau hành tây.\n3. Phi thơm hành tây, cho trứng vào chiên vàng đều hai mặt.",
                        ImagePath = "/uploads/trung-chien-hanh-tay.jpg",
                        CreatedAt = DateTime.UtcNow,
                        Ingredients = new List<RecipeIngredient>
                        {
                            new RecipeIngredient { FoodName = "Trứng", RequiredQuantity = 3, Unit = "quả" },
                            new RecipeIngredient { FoodName = "Hành tây", RequiredQuantity = 1, Unit = "củ" },
                            new RecipeIngredient { FoodName = "Dầu ăn", RequiredQuantity = 10, Unit = "ml" }
                        }
                    },
                    new Recipe
                    {
                        HouseholdId = householdId,
                        Name = "Canh cà chua thịt băm",
                        Description = "Món canh thanh mát, dễ ăn, cung cấp đầy đủ chất dinh dưỡng cho cả nhà.",
                        Instructions = "1. Phi thơm hành khô, xào chín thịt băm.\n2. Thêm cà chua thái múi cau vào xào chín mềm.\n3. Cho nước vào đun sôi, nêm nếm gia vị vừa ăn, thêm hành lá cắt nhỏ.",
                        ImagePath = "/uploads/canh-ca-chua-thit-bam.jpg",
                        CreatedAt = DateTime.UtcNow,
                        Ingredients = new List<RecipeIngredient>
                        {
                            new RecipeIngredient { FoodName = "Thịt băm", RequiredQuantity = 200, Unit = "g" },
                            new RecipeIngredient { FoodName = "Cà chua", RequiredQuantity = 2, Unit = "quả" },
                            new RecipeIngredient { FoodName = "Hành lá", RequiredQuantity = 50, Unit = "g" }
                        }
                    },
                    new Recipe
                    {
                        HouseholdId = householdId,
                        Name = "Thịt kho tàu",
                        Description = "Món ăn truyền thống đậm đà, hấp dẫn với thịt ba chỉ mềm béo và trứng chín thấm vị.",
                        Instructions = "1. Thái thịt ba chỉ thành miếng vuông vừa ăn, ướp gia vị.\n2. Luộc chín trứng rồi bóc vỏ.\n3. Thắng đường tạo màu caramel, cho thịt vào xào săn.\n4. Đổ nước dừa và trứng vào kho liu riu cho tới khi thịt chín mềm.",
                        ImagePath = "/uploads/thit-kho-tau.jpg",
                        CreatedAt = DateTime.UtcNow,
                        Ingredients = new List<RecipeIngredient>
                        {
                            new RecipeIngredient { FoodName = "Thịt ba chỉ", RequiredQuantity = 500, Unit = "g" },
                            new RecipeIngredient { FoodName = "Trứng", RequiredQuantity = 5, Unit = "quả" },
                            new RecipeIngredient { FoodName = "Nước dừa", RequiredQuantity = 300, Unit = "ml" }
                        }
                    },
                    new Recipe
                    {
                        HouseholdId = householdId,
                        Name = "Rau muống xào tỏi",
                        Description = "Món rau xào giòn ngon xanh mướt, dậy mùi thơm lừng của tỏi phi.",
                        Instructions = "1. Rau muống nhặt sạch lá úa, cắt khúc vừa ăn, ngâm nước muối loãng rồi rửa sạch.\n2. Tỏi bóc vỏ băm nhỏ.\n3. Phi thơm tỏi với dầu ăn, cho rau muống vào xào nhanh tay trên lửa lớn, nêm nếm gia vị vừa miệng.",
                        ImagePath = "/uploads/rau-muong-xao-toi.jpg",
                        CreatedAt = DateTime.UtcNow,
                        Ingredients = new List<RecipeIngredient>
                        {
                            new RecipeIngredient { FoodName = "Rau muống", RequiredQuantity = 1, Unit = "bó" },
                            new RecipeIngredient { FoodName = "Tỏi", RequiredQuantity = 1, Unit = "củ" },
                            new RecipeIngredient { FoodName = "Dầu ăn", RequiredQuantity = 15, Unit = "ml" }
                        }
                    },
                    new Recipe
                    {
                        HouseholdId = householdId,
                        Name = "Đậu hũ sốt cà chua",
                        Description = "Đậu hũ sốt cà chua là món ăn thanh đạm, bùi béo kết hợp sốt cà chua chua ngọt cực ngon.",
                        Instructions = "1. Đậu hũ cắt miếng vừa ăn, chiên vàng đều các mặt.\n2. Cà chua cắt hạt lựu, cho vào chảo xào nhừ tạo sốt.\n3. Cho đậu hũ đã chiên vào nước sốt, nêm nếm nước mắm, đường, đun nhỏ lửa 5-10 phút cho ngấm gia vị, rắc hành lá lên trên.",
                        ImagePath = "/uploads/dau-hu-sot-ca-chua.jpg",
                        CreatedAt = DateTime.UtcNow,
                        Ingredients = new List<RecipeIngredient>
                        {
                            new RecipeIngredient { FoodName = "Đậu hũ", RequiredQuantity = 3, Unit = "miếng" },
                            new RecipeIngredient { FoodName = "Cà chua", RequiredQuantity = 2, Unit = "quả" },
                            new RecipeIngredient { FoodName = "Hành lá", RequiredQuantity = 20, Unit = "g" },
                            new RecipeIngredient { FoodName = "Dầu ăn", RequiredQuantity = 15, Unit = "ml" }
                        }
                    },
                    new Recipe
                    {
                        HouseholdId = householdId,
                        Name = "Gà xào sả ớt",
                        Description = "Món gà xào cay nồng, đậm đà gia vị với mùi thơm đặc trưng từ sả tươi phi vàng.",
                        Instructions = "1. Thịt gà rửa sạch thái miếng vừa ăn, ướp với hành tỏi băm, hạt nêm, tiêu.\n2. Sả, ớt băm nhỏ.\n3. Phi thơm hành tỏi băm và sả ớt, cho gà vào xào săn trên lửa lớn cho chín vàng đều, nêm lại gia vị vừa ăn.",
                        ImagePath = "/uploads/ga-xao-sa-ot.jpg",
                        CreatedAt = DateTime.UtcNow,
                        Ingredients = new List<RecipeIngredient>
                        {
                            new RecipeIngredient { FoodName = "Thịt gà", RequiredQuantity = 400, Unit = "g" },
                            new RecipeIngredient { FoodName = "Sả", RequiredQuantity = 3, Unit = "củ" },
                            new RecipeIngredient { FoodName = "Ớt", RequiredQuantity = 2, Unit = "quả" },
                            new RecipeIngredient { FoodName = "Tỏi", RequiredQuantity = 1, Unit = "củ" }
                        }
                    },
                    new Recipe
                    {
                        HouseholdId = householdId,
                        Name = "Thịt bò xào hành tây",
                        Description = "Món xào dinh dưỡng kết hợp thịt bò mềm ngọt tự nhiên cùng hành tây và cà rốt giòn ngọt.",
                        Instructions = "1. Thịt bò thái lát mỏng, ướp tỏi băm, dầu hào, dầu ăn để thịt bò mềm ngon.\n2. Cà rốt thái sợi hoặc tỉa hoa thái mỏng, hành tây cắt múi cau.\n3. Phi thơm tỏi, xào thịt bò chín tái rồi múc riêng ra.\n4. Cho tiếp cà rốt, hành tây vào xào chín tới, trút thịt bò vào đảo nhanh tay rồi tắt bếp.",
                        ImagePath = "/uploads/thit-bo-xao-hanh-tay.jpg",
                        CreatedAt = DateTime.UtcNow,
                        Ingredients = new List<RecipeIngredient>
                        {
                            new RecipeIngredient { FoodName = "Thịt bò phi lê", RequiredQuantity = 300, Unit = "g" },
                            new RecipeIngredient { FoodName = "Hành tây", RequiredQuantity = 1, Unit = "củ" },
                            new RecipeIngredient { FoodName = "Cà rốt", RequiredQuantity = 0.5m, Unit = "củ" },
                            new RecipeIngredient { FoodName = "Tỏi", RequiredQuantity = 1, Unit = "củ" }
                        }
                    },
                    new Recipe
                    {
                        HouseholdId = householdId,
                        Name = "Canh sườn bí đỏ",
                        Description = "Món canh ngọt nước, bổ dưỡng từ sườn non ninh nhừ kết hợp với bí đỏ chín mềm ngọt bùi.",
                        Instructions = "1. Sườn non chặt khúc vừa ăn, chần qua nước sôi rồi rửa sạch.\n2. Bí đỏ gọt vỏ, thái miếng vuông dày.\n3. Cho sườn vào nồi ninh mềm với nước.\n4. Cho bí đỏ vào đun chín mềm, nêm gia vị vừa ăn, rắc hành lá lên trên.",
                        ImagePath = "/uploads/canh-suon-bi-do.jpg",
                        CreatedAt = DateTime.UtcNow,
                        Ingredients = new List<RecipeIngredient>
                        {
                            new RecipeIngredient { FoodName = "Sườn non", RequiredQuantity = 400, Unit = "g" },
                            new RecipeIngredient { FoodName = "Bí đỏ", RequiredQuantity = 300, Unit = "g" },
                            new RecipeIngredient { FoodName = "Hành lá", RequiredQuantity = 30, Unit = "g" }
                        }
                    },
                    new Recipe
                    {
                        HouseholdId = householdId,
                        Name = "Trứng đúc thịt băm",
                        Description = "Món trứng chiên mềm mịn xốp vàng rực kết hợp thịt băm ngọt đậm đà và hành lá thơm lừng.",
                        Instructions = "1. Cho trứng, thịt băm và hành lá thái nhỏ vào bát tô.\n2. Nêm hạt nêm, nước mắm, tiêu rồi đánh đều cho hỗn hợp hòa quyện.\n3. Đun nóng dầu ăn trên chảo, đổ hỗn hợp trứng vào chiên lửa nhỏ đậy vung cho chín đều bên trong, sau đó lật mặt chiên vàng đều.",
                        ImagePath = "/uploads/trung-duc-thit-bam.jpg",
                        CreatedAt = DateTime.UtcNow,
                        Ingredients = new List<RecipeIngredient>
                        {
                            new RecipeIngredient { FoodName = "Trứng", RequiredQuantity = 3, Unit = "quả" },
                            new RecipeIngredient { FoodName = "Thịt băm", RequiredQuantity = 150, Unit = "g" },
                            new RecipeIngredient { FoodName = "Hành lá", RequiredQuantity = 20, Unit = "g" }
                        }
                    }
                };

                _context.Recipes.AddRange(defaultRecipes);
                await _context.SaveChangesAsync();
            }

            // Patch ImagePath for existing recipes that have no image set
            var recipeImageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Trứng chiên hành tây",   "/uploads/trung-chien-hanh-tay.jpg"  },
                { "Canh cà chua thịt băm",  "/uploads/canh-ca-chua-thit-bam.jpg" },
                { "Thịt kho tàu",           "/uploads/thit-kho-tau.jpg"           },
                { "Rau muống xào tỏi",      "/uploads/rau-muong-xao-toi.jpg"      },
                { "Đậu hũ sốt cà chua",     "/uploads/dau-hu-sot-ca-chua.jpg"     },
                { "Gà xào sả ớt",           "/uploads/ga-xao-sa-ot.jpg"           },
                { "Thịt bò xào hành tây",   "/uploads/thit-bo-xao-hanh-tay.jpg"  },
                { "Canh sườn bí đỏ",        "/uploads/canh-suon-bi-do.jpg"        },
                { "Trứng đúc thịt băm",     "/uploads/trung-duc-thit-bam.jpg"     }
            };

            var recipesNeedingImage = await _context.Recipes
                .Where(r => r.HouseholdId == householdId && (r.ImagePath == null || r.ImagePath == string.Empty))
                .ToListAsync();

            bool anyPatched = false;
            foreach (var recipe in recipesNeedingImage)
            {
                if (recipeImageMap.TryGetValue(recipe.Name, out var imagePath))
                {
                    recipe.ImagePath = imagePath;
                    anyPatched = true;
                }
            }

            if (anyPatched)
            {
                await _context.SaveChangesAsync();
            }

            // Seed default food items if none exist
            var hasFoodItems = await _context.FoodItems.AnyAsync(f => f.HouseholdId == householdId);
            if (!hasFoodItems)
            {
                var ownerMember = await _context.MemberProfiles
                    .FirstOrDefaultAsync(m => m.HouseholdId == householdId && m.Role == MemberRole.Owner);

                if (ownerMember != null)
                {
                    var categories = await _context.Categories.Where(c => c.HouseholdId == householdId).ToListAsync();
                    var locations = await _context.StorageLocations.Where(l => l.HouseholdId == householdId).ToListAsync();

                    var catMeatFish = categories.FirstOrDefault(c => c.Name == "Thịt/Cá")?.Id ?? categories.FirstOrDefault()?.Id ?? 0;
                    var catVeggies = categories.FirstOrDefault(c => c.Name == "Rau củ")?.Id ?? categories.FirstOrDefault()?.Id ?? 0;
                    var catDrinks = categories.FirstOrDefault(c => c.Name == "Đồ uống")?.Id ?? categories.FirstOrDefault()?.Id ?? 0;
                    var catDryFoods = categories.FirstOrDefault(c => c.Name == "Đồ khô")?.Id ?? categories.FirstOrDefault()?.Id ?? 0;

                    var locFreezer = locations.FirstOrDefault(l => l.Name == "Ngăn đông")?.Id ?? locations.FirstOrDefault()?.Id ?? 0;
                    var locFridge = locations.FirstOrDefault(l => l.Name == "Ngăn mát")?.Id ?? locations.FirstOrDefault()?.Id ?? 0;
                    var locKitchen = locations.FirstOrDefault(l => l.Name == "Bếp")?.Id ?? locations.FirstOrDefault()?.Id ?? 0;

                    var defaultFoodItems = new List<FoodItem>
                    {
                        new FoodItem
                        {
                            HouseholdId = householdId,
                            CategoryId = catMeatFish,
                            StorageLocationId = locFridge,
                            Name = "Trứng gà",
                            ImagePath = "/assets/food-library/meat/egg.jpg",
                            Unit = "quả",
                            CurrentQuantity = 10,
                            MinQuantityThreshold = 2,
                            ExpiryDate = DateTime.Today.AddDays(15),
                            AddedDate = DateTime.UtcNow,
                            AddedByMemberId = ownerMember.Id,
                            LastUpdatedByMemberId = ownerMember.Id,
                            CurrentStatus = FoodStatus.Normal,
                            Notes = "Trứng gà tươi ngon giàu dinh dưỡng."
                        },
                        new FoodItem
                        {
                            HouseholdId = householdId,
                            CategoryId = catMeatFish,
                            StorageLocationId = locFreezer,
                            Name = "Thịt ba chỉ",
                            ImagePath = "/assets/food-library/meat/thit-ba-chi.jpg",
                            Unit = "g",
                            CurrentQuantity = 1000,
                            MinQuantityThreshold = 200,
                            ExpiryDate = DateTime.Today.AddDays(30),
                            AddedDate = DateTime.UtcNow,
                            AddedByMemberId = ownerMember.Id,
                            LastUpdatedByMemberId = ownerMember.Id,
                            CurrentStatus = FoodStatus.Normal,
                            Notes = "Thịt ba chỉ heo tươi ngon, thích hợp kho tàu hoặc luộc."
                        },
                        new FoodItem
                        {
                            HouseholdId = householdId,
                            CategoryId = catMeatFish,
                            StorageLocationId = locFreezer,
                            Name = "Thịt băm",
                            ImagePath = "/assets/food-library/meat/thit-bam.jpg",
                            Unit = "g",
                            CurrentQuantity = 500,
                            MinQuantityThreshold = 100,
                            ExpiryDate = DateTime.Today.AddDays(30),
                            AddedDate = DateTime.UtcNow,
                            AddedByMemberId = ownerMember.Id,
                            LastUpdatedByMemberId = ownerMember.Id,
                            CurrentStatus = FoodStatus.Normal,
                            Notes = "Thịt nạc băm sẵn, tiện nấu canh hoặc đúc trứng."
                        },
                        new FoodItem
                        {
                            HouseholdId = householdId,
                            CategoryId = catMeatFish,
                            StorageLocationId = locFreezer,
                            Name = "Thịt gà",
                            ImagePath = "/assets/food-library/meat/chicken.jpg",
                            Unit = "g",
                            CurrentQuantity = 1500,
                            MinQuantityThreshold = 300,
                            ExpiryDate = DateTime.Today.AddDays(30),
                            AddedDate = DateTime.UtcNow,
                            AddedByMemberId = ownerMember.Id,
                            LastUpdatedByMemberId = ownerMember.Id,
                            CurrentStatus = FoodStatus.Normal,
                            Notes = "Thịt đùi gà tươi sạch, thích hợp xào sả ớt."
                        },
                        new FoodItem
                        {
                            HouseholdId = householdId,
                            CategoryId = catMeatFish,
                            StorageLocationId = locFreezer,
                            Name = "Thịt bò phi lê",
                            ImagePath = "/assets/food-library/meat/thit-bo-phi-le.jpg",
                            Unit = "g",
                            CurrentQuantity = 500,
                            MinQuantityThreshold = 100,
                            ExpiryDate = DateTime.Today.AddDays(30),
                            AddedDate = DateTime.UtcNow,
                            AddedByMemberId = ownerMember.Id,
                            LastUpdatedByMemberId = ownerMember.Id,
                            CurrentStatus = FoodStatus.Normal,
                            Notes = "Thịt bò phi lê mềm ngọt, thích hợp xào hành tây."
                        },
                        new FoodItem
                        {
                            HouseholdId = householdId,
                            CategoryId = catMeatFish,
                            StorageLocationId = locFreezer,
                            Name = "Sườn non",
                            ImagePath = "/assets/food-library/meat/suon-non.jpg",
                            Unit = "g",
                            CurrentQuantity = 800,
                            MinQuantityThreshold = 200,
                            ExpiryDate = DateTime.Today.AddDays(30),
                            AddedDate = DateTime.UtcNow,
                            AddedByMemberId = ownerMember.Id,
                            LastUpdatedByMemberId = ownerMember.Id,
                            CurrentStatus = FoodStatus.Normal,
                            Notes = "Sườn heo non nhiều thịt, thích hợp ninh canh bí đỏ."
                        },
                        new FoodItem
                        {
                            HouseholdId = householdId,
                            CategoryId = catMeatFish,
                            StorageLocationId = locFridge,
                            Name = "Đậu hũ",
                            ImagePath = "/assets/food-library/Dried/dau-hu.jpg",
                            Unit = "miếng",
                            CurrentQuantity = 4,
                            MinQuantityThreshold = 1,
                            ExpiryDate = DateTime.Today.AddDays(3),
                            AddedDate = DateTime.UtcNow,
                            AddedByMemberId = ownerMember.Id,
                            LastUpdatedByMemberId = ownerMember.Id,
                            CurrentStatus = FoodStatus.Normal,
                            Notes = "Đậu hũ trắng thanh đạm, giàu protein thực vật."
                        },
                        new FoodItem
                        {
                            HouseholdId = householdId,
                            CategoryId = catVeggies,
                            StorageLocationId = locFridge,
                            Name = "Cà chua",
                            ImagePath = "/assets/food-library/vegetable/tomato.jpg",
                            Unit = "quả",
                            CurrentQuantity = 5,
                            MinQuantityThreshold = 1,
                            ExpiryDate = DateTime.Today.AddDays(7),
                            AddedDate = DateTime.UtcNow,
                            AddedByMemberId = ownerMember.Id,
                            LastUpdatedByMemberId = ownerMember.Id,
                            CurrentStatus = FoodStatus.Normal,
                            Notes = "Cà chua chín đỏ mọng, nhiều vitamin C."
                        },
                        new FoodItem
                        {
                            HouseholdId = householdId,
                            CategoryId = catVeggies,
                            StorageLocationId = locKitchen,
                            Name = "Hành tây",
                            ImagePath = "/assets/food-library/vegetable/hanh-tay.jpg",
                            Unit = "củ",
                            CurrentQuantity = 3,
                            MinQuantityThreshold = 1,
                            ExpiryDate = DateTime.Today.AddDays(15),
                            AddedDate = DateTime.UtcNow,
                            AddedByMemberId = ownerMember.Id,
                            LastUpdatedByMemberId = ownerMember.Id,
                            CurrentStatus = FoodStatus.Normal,
                            Notes = "Hành tây giòn ngọt."
                        },
                        new FoodItem
                        {
                            HouseholdId = householdId,
                            CategoryId = catVeggies,
                            StorageLocationId = locFridge,
                            Name = "Cà rốt",
                            ImagePath = "/assets/food-library/vegetable/carrot.jpg",
                            Unit = "củ",
                            CurrentQuantity = 2,
                            MinQuantityThreshold = 0,
                            ExpiryDate = DateTime.Today.AddDays(10),
                            AddedDate = DateTime.UtcNow,
                            AddedByMemberId = ownerMember.Id,
                            LastUpdatedByMemberId = ownerMember.Id,
                            CurrentStatus = FoodStatus.Normal,
                            Notes = "Cà rốt tươi ngon ngọt."
                        },
                        new FoodItem
                        {
                            HouseholdId = householdId,
                            CategoryId = catVeggies,
                            StorageLocationId = locKitchen,
                            Name = "Bí đỏ",
                            ImagePath = "/assets/food-library/vegetable/bi-do.jpg",
                            Unit = "quả",
                            CurrentQuantity = 1,
                            MinQuantityThreshold = 0,
                            ExpiryDate = DateTime.Today.AddDays(15),
                            AddedDate = DateTime.UtcNow,
                            AddedByMemberId = ownerMember.Id,
                            LastUpdatedByMemberId = ownerMember.Id,
                            CurrentStatus = FoodStatus.Normal,
                            Notes = "Bí đỏ thơm bùi nhiều dinh dưỡng."
                        },
                        new FoodItem
                        {
                            HouseholdId = householdId,
                            CategoryId = catVeggies,
                            StorageLocationId = locFridge,
                            Name = "Rau muống",
                            ImagePath = "/assets/food-library/vegetable/rau-muong.jpg",
                            Unit = "bó",
                            CurrentQuantity = 1,
                            MinQuantityThreshold = 0,
                            ExpiryDate = DateTime.Today.AddDays(3),
                            AddedDate = DateTime.UtcNow,
                            AddedByMemberId = ownerMember.Id,
                            LastUpdatedByMemberId = ownerMember.Id,
                            CurrentStatus = FoodStatus.Normal,
                            Notes = "Rau muống xanh giòn, xào tỏi cực ngon."
                        },
                        new FoodItem
                        {
                            HouseholdId = householdId,
                            CategoryId = catVeggies,
                            StorageLocationId = locFridge,
                            Name = "Hành lá",
                            ImagePath = "/assets/food-library/vegetable/hanh-la.jpg",
                            Unit = "g",
                            CurrentQuantity = 100,
                            MinQuantityThreshold = 20,
                            ExpiryDate = DateTime.Today.AddDays(5),
                            AddedDate = DateTime.UtcNow,
                            AddedByMemberId = ownerMember.Id,
                            LastUpdatedByMemberId = ownerMember.Id,
                            CurrentStatus = FoodStatus.Normal,
                            Notes = "Hành lá tươi xanh làm gia vị món ăn."
                        },
                        new FoodItem
                        {
                            HouseholdId = householdId,
                            CategoryId = catVeggies,
                            StorageLocationId = locKitchen,
                            Name = "Tỏi",
                            ImagePath = "/assets/food-library/vegetable/toi.jpg",
                            Unit = "củ",
                            CurrentQuantity = 5,
                            MinQuantityThreshold = 1,
                            ExpiryDate = DateTime.Today.AddDays(60),
                            AddedDate = DateTime.UtcNow,
                            AddedByMemberId = ownerMember.Id,
                            LastUpdatedByMemberId = ownerMember.Id,
                            CurrentStatus = FoodStatus.Normal,
                            Notes = "Tỏi củ làm gia vị dậy mùi thơm."
                        },
                        new FoodItem
                        {
                            HouseholdId = householdId,
                            CategoryId = catVeggies,
                            StorageLocationId = locKitchen,
                            Name = "Sả",
                            ImagePath = "/assets/food-library/vegetable/sa.jpg",
                            Unit = "củ",
                            CurrentQuantity = 10,
                            MinQuantityThreshold = 2,
                            ExpiryDate = DateTime.Today.AddDays(15),
                            AddedDate = DateTime.UtcNow,
                            AddedByMemberId = ownerMember.Id,
                            LastUpdatedByMemberId = ownerMember.Id,
                            CurrentStatus = FoodStatus.Normal,
                            Notes = "Sả củ thơm nồng."
                        },
                        new FoodItem
                        {
                            HouseholdId = householdId,
                            CategoryId = catVeggies,
                            StorageLocationId = locFridge,
                            Name = "Ớt",
                            ImagePath = "/assets/food-library/vegetable/ot.jpg",
                            Unit = "quả",
                            CurrentQuantity = 10,
                            MinQuantityThreshold = 2,
                            ExpiryDate = DateTime.Today.AddDays(15),
                            AddedDate = DateTime.UtcNow,
                            AddedByMemberId = ownerMember.Id,
                            LastUpdatedByMemberId = ownerMember.Id,
                            CurrentStatus = FoodStatus.Normal,
                            Notes = "Ớt đỏ cay nồng."
                        },
                        new FoodItem
                        {
                            HouseholdId = householdId,
                            CategoryId = catDrinks,
                            StorageLocationId = locFridge,
                            Name = "Nước dừa",
                            ImagePath = "/assets/food-library/drink/nuoc-dua.jpg",
                            Unit = "ml",
                            CurrentQuantity = 500,
                            MinQuantityThreshold = 0,
                            ExpiryDate = DateTime.Today.AddDays(5),
                            AddedDate = DateTime.UtcNow,
                            AddedByMemberId = ownerMember.Id,
                            LastUpdatedByMemberId = ownerMember.Id,
                            CurrentStatus = FoodStatus.Normal,
                            Notes = "Nước dừa tươi ngọt thanh, kho thịt tàu rất ngon."
                        },
                        new FoodItem
                        {
                            HouseholdId = householdId,
                            CategoryId = catDryFoods,
                            StorageLocationId = locKitchen,
                            Name = "Dầu ăn",
                            ImagePath = "/assets/food-library/Dried/dau-an.jpg",
                            Unit = "ml",
                            CurrentQuantity = 1000,
                            MinQuantityThreshold = 200,
                            ExpiryDate = DateTime.Today.AddDays(365),
                            AddedDate = DateTime.UtcNow,
                            AddedByMemberId = ownerMember.Id,
                            LastUpdatedByMemberId = ownerMember.Id,
                            CurrentStatus = FoodStatus.Normal,
                            Notes = "Dầu ăn thực vật cao cấp chiên rán."
                        }
                    };

                    _context.FoodItems.AddRange(defaultFoodItems);
                    await _context.SaveChangesAsync();
                }
            }
        }
    }
}