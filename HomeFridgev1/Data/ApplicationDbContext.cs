using HomeFridgev1.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HomeFridgev1.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Household> Households { get; set; }
        public DbSet<HouseholdSettings> HouseholdSettings { get; set; }
        public DbSet<MemberProfile> MemberProfiles { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<StorageLocation> StorageLocations { get; set; }
        public DbSet<FoodItem> FoodItems { get; set; }
        public DbSet<FoodActivityLog> FoodActivityLogs { get; set; }
        public DbSet<NotificationLog> NotificationLogs { get; set; }
        public DbSet<ArchiveGroup> ArchiveGroups { get; set; }
        public DbSet<ShoppingItem> ShoppingItems { get; set; }
        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<RecipeIngredient> RecipeIngredients { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ... (các cấu hình hiện có giữ nguyên) ...
            // [Giữ nguyên cấu hình cũ, chỉ nối thêm cấu hình Recipe và RecipeIngredient vào cuối OnModelCreating]

            // Household
            builder.Entity<Household>()
                .HasOne<IdentityUser>(h => h.OwnerUser)
                .WithMany()
                .HasForeignKey(h => h.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Household>()
                .Property(h => h.Name)
                .HasMaxLength(200)
                .IsRequired();

            // HouseholdSettings (1-1)
            builder.Entity<HouseholdSettings>()
                .HasOne(hs => hs.Household)
                .WithOne(h => h.Settings)
                .HasForeignKey<HouseholdSettings>(hs => hs.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            // MemberProfile
            builder.Entity<MemberProfile>()
                .Property(m => m.DisplayName)
                .HasMaxLength(100)
                .IsRequired();

            builder.Entity<MemberProfile>()
                .HasOne(m => m.Household)
                .WithMany(h => h.MemberProfiles)
                .HasForeignKey(m => m.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            // Category
            builder.Entity<Category>()
                .Property(c => c.Name)
                .HasMaxLength(100)
                .IsRequired();

            builder.Entity<Category>()
                .HasOne(c => c.Household)
                .WithMany(h => h.Categories)
                .HasForeignKey(c => c.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            // StorageLocation
            builder.Entity<StorageLocation>()
                .Property(s => s.Name)
                .HasMaxLength(100)
                .IsRequired();

            builder.Entity<StorageLocation>()
                .HasOne(s => s.Household)
                .WithMany(h => h.StorageLocations)
                .HasForeignKey(s => s.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            // FoodItem
            builder.Entity<FoodItem>()
                .Property(f => f.Name)
                .HasMaxLength(200)
                .IsRequired();

            builder.Entity<FoodItem>()
                .Property(f => f.Unit)
                .HasMaxLength(50)
                .IsRequired();

            builder.Entity<FoodItem>()
                .Property(f => f.CurrentQuantity)
                .HasColumnType("decimal(18,2)");

            builder.Entity<FoodItem>()
                .Property(f => f.MinQuantityThreshold)
                .HasColumnType("decimal(18,2)");

            builder.Entity<FoodItem>()
                .HasOne(f => f.Household)
                .WithMany(h => h.FoodItems)
                .HasForeignKey(f => f.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<FoodItem>()
                .HasOne(f => f.Category)
                .WithMany(c => c.FoodItems)
                .HasForeignKey(f => f.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<FoodItem>()
                .HasOne(f => f.StorageLocation)
                .WithMany(s => s.FoodItems)
                .HasForeignKey(f => f.StorageLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<FoodItem>()
                .HasOne(f => f.AddedByMember)
                .WithMany()
                .HasForeignKey(f => f.AddedByMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<FoodItem>()
                .HasOne(f => f.LastUpdatedByMember)
                .WithMany()
                .HasForeignKey(f => f.LastUpdatedByMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<FoodItem>()
                .HasOne(f => f.ArchivedByMember)
                .WithMany()
                .HasForeignKey(f => f.ArchivedByMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            // FoodActivityLog
            builder.Entity<FoodActivityLog>()
                .Property(a => a.QuantityDelta)
                .HasColumnType("decimal(18,2)");

            builder.Entity<FoodActivityLog>()
                .Property(a => a.QuantityBefore)
                .HasColumnType("decimal(18,2)");

            builder.Entity<FoodActivityLog>()
                .Property(a => a.QuantityAfter)
                .HasColumnType("decimal(18,2)");

            builder.Entity<FoodActivityLog>()
                .HasOne(a => a.FoodItem)
                .WithMany(f => f.ActivityLogs)
                .HasForeignKey(a => a.FoodItemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<FoodActivityLog>()
                .HasOne(a => a.MemberProfile)
                .WithMany(m => m.FoodActivityLogs)
                .HasForeignKey(a => a.MemberProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            // NotificationLog
            builder.Entity<NotificationLog>()
                .Property(n => n.Channel)
                .HasMaxLength(50)
                .IsRequired();

            builder.Entity<NotificationLog>()
                .Property(n => n.EventType)
                .HasMaxLength(100)
                .IsRequired();

            builder.Entity<NotificationLog>()
                .HasOne(n => n.Household)
                .WithMany(h => h.NotificationLogs)
                .HasForeignKey(n => n.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<NotificationLog>()
                .HasOne(n => n.FoodItem)
                .WithMany(f => f.NotificationLogs)
                .HasForeignKey(n => n.FoodItemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<NotificationLog>()
                .HasOne(n => n.RecipientMember)
                .WithMany(m => m.NotificationLogs)
                .HasForeignKey(n => n.RecipientMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            // ArchiveGroup
            builder.Entity<ArchiveGroup>()
                .Property(g => g.Name)
                .HasMaxLength(100)
                .IsRequired();

            builder.Entity<ArchiveGroup>()
                .HasOne(g => g.Household)
                .WithMany(h => h.ArchiveGroups)
                .HasForeignKey(g => g.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ArchiveGroup>()
                .HasOne(g => g.CreatedByMember)
                .WithMany()
                .HasForeignKey(g => g.CreatedByMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<FoodItem>()
                .HasOne(f => f.ArchiveGroup)
                .WithMany(g => g.FoodItems)
                .HasForeignKey(f => f.ArchiveGroupId)
                .OnDelete(DeleteBehavior.SetNull);

            // ShoppingItem
            builder.Entity<ShoppingItem>()
                .Property(s => s.Name)
                .HasMaxLength(200)
                .IsRequired();

            builder.Entity<ShoppingItem>()
                .Property(s => s.Unit)
                .HasMaxLength(50)
                .IsRequired();

            builder.Entity<ShoppingItem>()
                .Property(s => s.Quantity)
                .HasColumnType("decimal(18,2)");

            builder.Entity<ShoppingItem>()
                .HasOne(s => s.Household)
                .WithMany()
                .HasForeignKey(s => s.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ShoppingItem>()
                .HasOne(s => s.FoodItem)
                .WithMany()
                .HasForeignKey(s => s.FoodItemId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ShoppingItem>()
                .HasOne(s => s.Category)
                .WithMany()
                .HasForeignKey(s => s.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ShoppingItem>()
                .HasOne(s => s.AddedByMember)
                .WithMany()
                .HasForeignKey(s => s.AddedByMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ShoppingItem>()
                .HasOne(s => s.PurchasedByMember)
                .WithMany()
                .HasForeignKey(s => s.PurchasedByMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            // Recipe
            builder.Entity<Recipe>()
                .Property(r => r.Name)
                .HasMaxLength(200)
                .IsRequired();

            builder.Entity<Recipe>()
                .Property(r => r.Description)
                .HasMaxLength(500);

            builder.Entity<Recipe>()
                .HasOne(r => r.Household)
                .WithMany()
                .HasForeignKey(r => r.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            // RecipeIngredient
            builder.Entity<RecipeIngredient>()
                .Property(ri => ri.FoodName)
                .HasMaxLength(100)
                .IsRequired();

            builder.Entity<RecipeIngredient>()
                .Property(ri => ri.RequiredQuantity)
                .HasColumnType("decimal(18,2)");

            builder.Entity<RecipeIngredient>()
                .Property(ri => ri.Unit)
                .HasMaxLength(50)
                .IsRequired();

            builder.Entity<RecipeIngredient>()
                .HasOne(ri => ri.Recipe)
                .WithMany(r => r.Ingredients)
                .HasForeignKey(ri => ri.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}