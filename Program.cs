using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ExpenseTracker.Data;
using ExpenseTracker.Models;

var builder = WebApplication.CreateBuilder(args);

// Add Database Context using PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// If the connection string is in URI format (e.g., postgresql://...), parse it into standard ADO.NET format
if (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://"))
{
    var uri = new Uri(connectionString);
    var userInfo = uri.UserInfo.Split(':');
    var username = Uri.UnescapeDataString(userInfo[0]);
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
    var host = uri.Host;
    var port = uri.Port > 0 ? uri.Port : 5432;
    var database = uri.AbsolutePath.TrimStart('/');
    
    var sslMode = "Require";
    if (uri.Query.Contains("sslmode="))
    {
        var match = System.Text.RegularExpressions.Regex.Match(uri.Query, @"sslmode=([^&]+)");
        if (match.Success)
        {
            sslMode = match.Groups[1].Value;
            if (!string.IsNullOrEmpty(sslMode))
            {
                sslMode = char.ToUpper(sslMode[0]) + sslMode.Substring(1);
            }
        }
    }
    
    connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode={sslMode};Trust Server Certificate=true;";
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Add Identity Services
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure Cookie Authentication paths
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

// Add MVC services
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

// Authentication MUST be called before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

// Database seeding at startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        // 1. Seed Roles
        string[] roleNames = { "Admin", "User" };
        foreach (var roleName in roleNames)
        {
            if (!roleManager.RoleExistsAsync(roleName).GetAwaiter().GetResult())
            {
                roleManager.CreateAsync(new IdentityRole(roleName)).GetAwaiter().GetResult();
            }
        }

        // 2. Seed Default Categories
        if (!context.Categories.Any())
        {
            var defaultCategories = new List<Category>
            {
                // Incomes
                new Category { Name = "Salary", Type = "Income", Icon = "fa-wallet", Color = "#2ec4b6" },
                new Category { Name = "Freelance", Type = "Income", Icon = "fa-laptop-code", Color = "#00b4d8" },
                new Category { Name = "Investments", Type = "Income", Icon = "fa-chart-line", Color = "#2a9d8f" },
                new Category { Name = "Gifts/Other", Type = "Income", Icon = "fa-coins", Color = "#4caf50" },

                // Expenses
                new Category { Name = "Housing", Type = "Expense", Icon = "fa-home", Color = "#e63946" },
                new Category { Name = "Groceries", Type = "Expense", Icon = "fa-shopping-basket", Color = "#f4a261" },
                new Category { Name = "Utilities", Type = "Expense", Icon = "fa-bolt", Color = "#457b9d" },
                new Category { Name = "Transportation", Type = "Expense", Icon = "fa-car", Color = "#1d3557" },
                new Category { Name = "Entertainment", Type = "Expense", Icon = "fa-film", Color = "#a8dadc" },
                new Category { Name = "Dining Out", Type = "Expense", Icon = "fa-utensils", Color = "#e76f51" },
                new Category { Name = "Healthcare", Type = "Expense", Icon = "fa-heartbeat", Color = "#e0aaff" },
                new Category { Name = "Shopping", Type = "Expense", Icon = "fa-shopping-bag", Color = "#ff006e" },
                new Category { Name = "Other Expense", Type = "Expense", Icon = "fa-tags", Color = "#9b5de5" }
            };

            context.Categories.AddRange(defaultCategories);
            context.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during database migration or seeding.");
    }
}

app.Run();

