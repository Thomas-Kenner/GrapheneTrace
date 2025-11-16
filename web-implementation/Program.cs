using GrapheneTrace.Web.Components;
using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using GrapheneTrace.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Validate PressureThresholds configuration at startup
// Author: SID:2412494
// This ensures invalid configuration is caught immediately rather than at runtime
var thresholdsConfig = builder.Configuration
    .GetSection(PressureThresholdsConfig.SectionName)
    .Get<PressureThresholdsConfig>() ?? new PressureThresholdsConfig();

var configErrors = thresholdsConfig.Validate();
if (configErrors.Any())
{
    var errorMessage = string.Join(Environment.NewLine, new[]
    {
        "❌ INVALID PRESSURE THRESHOLDS CONFIGURATION",
        "The following configuration errors were found in appsettings.json:",
        ""
    }.Concat(configErrors.Select(e => $"  • {e}")).Concat(new[]
    {
        "",
        "Please fix these issues in appsettings.json under the 'PressureThresholds' section.",
        "Application startup has been aborted to prevent runtime errors."
    }));

    Console.Error.WriteLine(errorMessage);
    throw new InvalidOperationException(
        "Invalid PressureThresholds configuration. See console output for details.");
}

// Register validated configuration as singleton
builder.Services.AddSingleton(thresholdsConfig);

// Add services to the container
builder.Services.AddControllers();  // For AccountController

// Configure HttpClient for Blazor Server components
// Author: SID:2412494
// Blazor Server requires explicit BaseAddress configuration for HttpClient
// This enables components to make relative API calls (e.g., "/api/settings")
builder.Services.AddScoped(sp =>
{
    var navigationManager = sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
    return new HttpClient
    {
        BaseAddress = new Uri(navigationManager.BaseUri)
    };
});


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

    // Session expiration settings (Story #36)
    // Author: SID:2412494
    // HIPAA/medical device security standard: 20-minute timeout
    // Sessions automatically expire after 20 minutes of inactivity
    // This protects patient data if users forget to log out
    options.ExpireTimeSpan = TimeSpan.FromMinutes(20);

    // Sliding expiration: session extends by 20 minutes on each request
    // User remains logged in as long as they are active
    // Countdown resets with each page navigation or interaction
    options.SlidingExpiration = true;

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

// Add Patient Settings Service
// Author: SID:2412494
builder.Services.AddScoped<PatientSettingsService>();

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
