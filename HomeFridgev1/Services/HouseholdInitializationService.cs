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

            // Seed 2 default food items if none exist
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
                    var locFridge = locations.FirstOrDefault(l => l.Name == "Ngăn mát")?.Id ?? locations.FirstOrDefault()?.Id ?? 0;
                    var locFreezer = locations.FirstOrDefault(l => l.Name == "Ngăn đông")?.Id ?? locations.FirstOrDefault()?.Id ?? 0;

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
                        }
                    };

                    _context.FoodItems.AddRange(defaultFoodItems);
                    await _context.SaveChangesAsync();
                }
            }
        }
    }
}