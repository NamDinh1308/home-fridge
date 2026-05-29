using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HomeFridgev1.Data;
using HomeFridgev1.Models.Entities;
using HomeFridgev1.Models.Enums;
using HomeFridgev1.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HomeFridgev1.Services
{
    public class FoodStatusService : IFoodStatusService
    {
        private readonly ApplicationDbContext _context;

        public FoodStatusService(ApplicationDbContext context)
        {
            _context = context;
        }

        public FoodStatus CalculateStatus(decimal currentQuantity, DateTime expiryDate, HouseholdSettings settings)
        {
            if (currentQuantity <= 0)
            {
                return FoodStatus.OutOfStock;
            }

            var today = DateTime.UtcNow.Date;
            var daysRemaining = (expiryDate.Date - today).TotalDays;

            if (daysRemaining < 0)
            {
                return FoodStatus.Expired;
            }

            if (daysRemaining < settings.UrgentDaysBeforeExpiry)
            {
                return FoodStatus.Urgent;
            }

            if (daysRemaining < settings.WarningDaysBeforeExpiry)
            {
                return FoodStatus.Warning;
            }

            return FoodStatus.Normal;
        }

        public async Task ApplyAutoHideRuleAsync(int householdId, CancellationToken cancellationToken = default)
        {
            var settings = await _context.HouseholdSettings
                .FirstOrDefaultAsync(s => s.HouseholdId == householdId, cancellationToken);
            
            if (settings == null || !settings.EnableAutoHideOutOfStock)
            {
                return;
            }

            var outOfStockItems = await _context.FoodItems
                .Where(f => f.HouseholdId == householdId && !f.IsArchived && f.CurrentQuantity <= 0)
                .ToListAsync(cancellationToken);

            var nowUtc = DateTime.UtcNow;
            bool changed = false;

            foreach (var foodItem in outOfStockItems)
            {
                if (!foodItem.OutOfStockSince.HasValue)
                {
                    foodItem.OutOfStockSince = nowUtc;
                    changed = true;
                }

                var daysOutOfStock = (nowUtc - foodItem.OutOfStockSince.Value).TotalDays;
                if (daysOutOfStock >= settings.OutOfStockAutoHideDays)
                {
                    foodItem.IsArchived = true;
                    foodItem.ArchivedAt = nowUtc;
                    foodItem.ArchivedReason = $"Tự động lưu trữ sau {settings.OutOfStockAutoHideDays} ngày hết hàng.";

                    var ownerMember = await _context.MemberProfiles
                        .FirstOrDefaultAsync(m => m.HouseholdId == householdId && m.Role == MemberRole.Owner, cancellationToken);
                    if (ownerMember != null)
                    {
                        foodItem.ArchivedByMemberId = ownerMember.Id;
                    }

                    var log = new FoodActivityLog
                    {
                        FoodItemId = foodItem.Id,
                        MemberProfileId = ownerMember?.Id ?? foodItem.AddedByMemberId,
                        ActivityType = FoodActivityType.Archive,
                        QuantityDelta = 0,
                        QuantityBefore = foodItem.CurrentQuantity,
                        QuantityAfter = foodItem.CurrentQuantity,
                        Reason = "Hệ thống tự động lưu trữ món hết hàng.",
                        Note = $"Lưu trữ tự động sau {settings.OutOfStockAutoHideDays} ngày hết hàng.",
                        CreatedAt = nowUtc
                    };
                    _context.FoodActivityLogs.Add(log);
                    changed = true;
                }
            }

            if (changed)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}