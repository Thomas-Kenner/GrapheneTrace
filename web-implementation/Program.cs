using GrapheneTrace.Web.Components;
using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using GrapheneTrace.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();  // For AccountController
builder.Services.AddHttpClient();  // For auth form posts
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add DbContext with PostgreSQL
// Author: SID:2412494
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add ASP.NET Core Identity
// Author: SID:2412494
// Configures authentication with medical device security standards
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    // Password requirements (HIPAA/medical device standards)
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;
    options.Password.RequiredUniqueChars = 4;

    // Lockout settings (prevent brute force attacks)
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;

    // Sign-in settings (email confirmation disabled for now)
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddRoles<IdentityRole<Guid>>()  // Add role manager support
.AddDefaultTokenProviders();

// Configure authentication cookie (HIPAA compliance)
// Author: SID:2412494
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    // In production, set to CookieSecurePolicy.Always for HTTPS only
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest  // Allow HTTP in development
        : CookieSecurePolicy.Always;  // HTTPS only in production
    options.Cookie.SameSite = SameSiteMode.Lax;  // Changed from Strict for better compatibility
    options.Cookie.Name = ".GrapheneTrace.Auth";

    // Session timeout (HIPAA recommended: 20 minutes)
    options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
    options.SlidingExpiration = true;  // Extends on activity

    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
});

// Add authentication state provider for Blazor Server
// Author: SID:2412494
builder.Services.AddScoped<AuthenticationStateProvider,
    RevalidatingIdentityAuthenticationStateProvider<ApplicationUser>>();

// Add Dashboard Service
// Author: SID:2402513
builder.Services.AddScoped<DashboardService>();

// Add User Management Service
// Author: SID:2402513
builder.Services.AddScoped<UserManagementService>();

// Add Database Seeder
// Author: SID:2412494
builder.Services.AddScoped<DatabaseSeeder>();

var app = builder.Build();

// Seed database with essential system accounts
// Author: SID:2412494
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

// Add authentication & authorization middleware
// Author: SID:2412494
// IMPORTANT: Must come before MapRazorComponents
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();  // Map controller endpoints

app.Run();
