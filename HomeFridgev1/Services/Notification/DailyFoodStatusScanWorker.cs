using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HomeFridgev1.Data;
using HomeFridgev1.Models.Entities;
using HomeFridgev1.Models.Enums;
using HomeFridgev1.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HomeFridgev1.Services.Notification
{
    public class DailyFoodStatusScanWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<DailyFoodStatusScanWorker> _logger;
        private readonly Dictionary<int, DateTime> _lastRunDateByHousehold = new();

        public DailyFoodStatusScanWorker(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<DailyFoodStatusScanWorker> _loggerInstance)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = _loggerInstance;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DailyFoodStatusScanWorker is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformScanAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during food status scan.");
                }

                // Sleep for 15 minutes
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }

            _logger.LogInformation("DailyFoodStatusScanWorker is stopping.");
        }

        private async Task PerformScanAsync(CancellationToken stoppingToken)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var foodStatusService = scope.ServiceProvider.GetRequiredService<IFoodStatusService>();
                var notificationService = scope.ServiceProvider.GetRequiredService<IFoodStatusNotificationService>();

                var households = await dbContext.Households
                    .Include(h => h.Settings)
                    .Include(h => h.MemberProfiles)
                    .Where(h => h.IsActive)
                    .ToListAsync(stoppingToken);

                var today = DateTime.Today; // local server date
                var now = DateTime.Now;     // local server time

                foreach (var household in households)
                {
                    if (household.Settings == null) continue;

                    var settings = household.Settings;
                    var checkTimeStr = settings.DailyCheckTime ?? "08:00";

                    if (!TimeSpan.TryParse(checkTimeStr, out var checkTime))
                    {
                        checkTime = new TimeSpan(8, 0, 0); // fallback to 8:00 AM
                    }

                    var alreadyRunToday = _lastRunDateByHousehold.TryGetValue(household.Id, out var lastRunDate) && lastRunDate == today;

                    if (now.TimeOfDay >= checkTime && !alreadyRunToday)
                    {
                        _logger.LogInformation("Running daily food status scan for Household {HouseholdId} ({HouseholdName})", household.Id, household.Name);

                        try
                        {
                            await ScanHouseholdFoodsAsync(dbContext, foodStatusService, notificationService, household, settings, today, stoppingToken);
                            _lastRunDateByHousehold[household.Id] = today;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error scanning foods for Household {HouseholdId}", household.Id);
                        }
                    }
                }
            }
        }

        private async Task ScanHouseholdFoodsAsync(
            ApplicationDbContext dbContext,
            IFoodStatusService foodStatusService,
            IFoodStatusNotificationService notificationService,
            Household household,
            HouseholdSettings settings,
            DateTime today,
            CancellationToken cancellationToken)
        {
            var foodItems = await dbContext.FoodItems
                .Include(f => f.Category)
                .Include(f => f.StorageLocation)
                .Where(f => f.HouseholdId == household.Id && !f.IsArchived)
                .ToListAsync(cancellationToken);

            var nowUtc = DateTime.UtcNow;

            foreach (var foodItem in foodItems)
            {
                var oldStatus = foodItem.CurrentStatus;
                var newStatus = foodStatusService.CalculateStatus(foodItem.CurrentQuantity, foodItem.ExpiryDate, settings);

                // Ensure OutOfStockSince is populated for out of stock items
                if (newStatus == FoodStatus.OutOfStock)
                {
                    if (!foodItem.OutOfStockSince.HasValue)
                    {
                        foodItem.OutOfStockSince = nowUtc;
                    }
                }
                else
                {
                    foodItem.OutOfStockSince = null;
                }

                // Update status and last changed time if changed
                if (newStatus != oldStatus)
                {
                    foodItem.CurrentStatus = newStatus;
                    foodItem.LastStatusChangedAt = nowUtc;

                    // Notify status transition
                    await notificationService.NotifyStatusTransitionAsync(foodItem, oldStatus, newStatus, cancellationToken);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            // Apply auto-hide rule
            await foodStatusService.ApplyAutoHideRuleAsync(household.Id, cancellationToken);
        }
    }
}
