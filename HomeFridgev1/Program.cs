using HomeFridgev1.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HomeFridgev1.Services;
using HomeFridgev1.Services.Interfaces;
using HomeFridgev1.Models.Settings;
using HomeFridgev1.Services.Email;
using HomeFridgev1.Services.Notification;
using Resend;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var rawConnectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var connectionString = ConvertPostgresUrlToConnectionString(rawConnectionString);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (connectionString.Contains("Host=") || connectionString.Contains("Server="))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

builder.Services.AddScoped<IHouseholdInitializationService, HouseholdInitializationService>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddScoped<ICurrentMemberService, CurrentMemberService>();
builder.Services.AddScoped<IFoodStatusService, FoodStatusService>();
builder.Services.AddScoped<IFoodImageLibraryService, FoodImageLibraryService>();
builder.Services.AddScoped<IRecipeService, RecipeService>();

builder.Services.AddOptions();
builder.Services.Configure<EmailProviderSettings>(
    builder.Configuration.GetSection("EmailProvider"));

builder.Services.Configure<ResendClientOptions>(options =>
{
    options.ApiToken = builder.Configuration["EmailProvider:ApiKey"] ?? string.Empty;
});
builder.Services.AddHttpClient<ResendClient>();
builder.Services.AddTransient<IResend, ResendClient>();

builder.Services.AddScoped<IEmailNotificationService, ResendEmailNotificationService>();
builder.Services.AddScoped<INotificationRecipientResolver, NotificationRecipientResolver>();
builder.Services.AddScoped<IFoodStatusNotificationService, FoodStatusNotificationService>();
builder.Services.AddHostedService<DailyFoodStatusScanWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// Automatically apply database migrations on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

app.Run();

#region Helper Methods
static string ConvertPostgresUrlToConnectionString(string url)
{
    if (string.IsNullOrEmpty(url) || !url.StartsWith("postgres://"))
    {
        return url;
    }

    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':');
    var username = userInfo[0];
    var password = userInfo.Length > 1 ? userInfo[1] : string.Empty;
    var host = uri.Host;
    var port = uri.Port > 0 ? uri.Port : 5432;
    var database = uri.AbsolutePath.TrimStart('/');

    return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
}
#endregion
