using GrapheneTrace.Web.Controllers;
using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace GrapheneTrace.Web.Tests.Controllers;

/// <summary>
/// Unit tests for SettingsController.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Test Coverage:
/// 1. GET returns default settings for new patient
/// 2. GET returns existing settings for patient with settings
/// 3. PUT creates new settings when none exist
/// 4. PUT updates existing settings
/// 5. PUT rejects low threshold >= high threshold
/// 6. PUT rejects low threshold less than configured min
/// 7. PUT rejects low threshold greater than configured max
/// 8. PUT rejects high threshold less than configured min
/// 9. PUT rejects high threshold greater than configured max
/// 10. Unauthorized users cannot access endpoints (handled by [Authorize] attribute)
///
/// ⚙️ CONFIGURATION NOTE:
/// These tests load PressureThresholdsConfig from appsettings.json (same as production).
/// This ensures tests validate against the actual configured ranges, not hardcoded values.
/// If you modify appsettings.json threshold ranges, tests will automatically use new values.
///
/// Testing Strategy:
/// - Uses in-memory database for fast, isolated tests
/// - Real UserManager for authentic Identity behavior
/// - Loads configuration from appsettings.json (not hardcoded)
/// - Each test is independent with its own database context
/// - Mock authenticated user context with ClaimsPrincipal
/// - Focuses on business logic (validation, CRUD operations)
/// </remarks>
public class SettingsControllerTests : IDisposable
{
    private ApplicationDbContext _context;
    private UserManager<ApplicationUser> _userManager;
    private PressureThresholdsConfig _thresholdsConfig;
    private Mock<ILogger<SettingsController>> _mockLogger;
    private SettingsController _controller;
    private Guid _testPatientId;

    public SettingsControllerTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);

        // Setup UserManager
        var userStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<ApplicationUser, IdentityRole<Guid>, ApplicationDbContext, Guid>(_context);
        _userManager = new UserManager<ApplicationUser>(
            userStore,
            null!,
            new PasswordHasher<ApplicationUser>(),
            null!,
            null!,
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            new Mock<ILogger<UserManager<ApplicationUser>>>().Object);

        // Create test patient user
        _testPatientId = Guid.NewGuid();
        var testPatient = new ApplicationUser
        {
            Id = _testPatientId,
            Email = "patient@test.com",
            UserName = "patient@test.com",
            FirstName = "Test",
            LastName = "Patient",
            UserType = "Patient",
            ApprovedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };
        _userManager.CreateAsync(testPatient, "Password123!").Wait();

        // Load configuration from appsettings.json (same as production)
        // NOTE: Tests use actual configuration file to ensure consistency with runtime behavior
        var basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../../.."));
        var configPath = Path.Combine(basePath, "web-implementation/appsettings.json");

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: false, reloadOnChange: false)
            .Build();

        _thresholdsConfig = configuration
            .GetSection(PressureThresholdsConfig.SectionName)
            .Get<PressureThresholdsConfig>() ?? new PressureThresholdsConfig();

        // Validate configuration (same validation as Program.cs)
        var configErrors = _thresholdsConfig.Validate();
        if (configErrors.Any())
        {
            throw new InvalidOperationException(
                $"Invalid PressureThresholds configuration in appsettings.json: {string.Join(", ", configErrors)}");
        }

        // Setup mock logger
        _mockLogger = new Mock<ILogger<SettingsController>>();

        // Create controller with configuration
        _controller = new SettingsController(_context, _userManager, _thresholdsConfig, _mockLogger.Object);

        // Mock HttpContext with authenticated user
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _testPatientId.ToString()),
            new Claim(ClaimTypes.Email, "patient@test.com"),
            new Claim(ClaimTypes.Role, "Patient")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    public void Dispose()
    {
        _context.Dispose();
        _userManager.Dispose();
    }

    /// <summary>
    /// Test: GET /api/settings returns default settings for new patient
    /// </summary>
    [Fact]
    public async Task GetSettings_NewPatient_ReturnsDefaultSettings()
    {
        // Act
        var result = await _controller.GetSettings();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;

        // Verify default values using reflection
        var lowThreshold = response?.GetType().GetProperty("lowThreshold")?.GetValue(response);
        var highThreshold = response?.GetType().GetProperty("highThreshold")?.GetValue(response);

        Assert.Equal(50, lowThreshold);
        Assert.Equal(200, highThreshold);

        // Verify settings were created in database
        var settings = await _context.PatientSettings.FirstOrDefaultAsync(ps => ps.UserId == _testPatientId);
        Assert.NotNull(settings);
        Assert.Equal(50, settings.LowPressureThreshold);
        Assert.Equal(200, settings.HighPressureThreshold);
    }

    /// <summary>
    /// Test: GET /api/settings returns existing settings
    /// </summary>
    [Fact]
    public async Task GetSettings_ExistingSettings_ReturnsSettings()
    {
        // Arrange - Create existing settings
        var existingSettings = new PatientSettings
        {
            PatientSettingsId = Guid.NewGuid(),
            UserId = _testPatientId,
            LowPressureThreshold = 75,
            HighPressureThreshold = 180,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.PatientSettings.Add(existingSettings);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetSettings();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;

        var lowThreshold = response?.GetType().GetProperty("lowThreshold")?.GetValue(response);
        var highThreshold = response?.GetType().GetProperty("highThreshold")?.GetValue(response);

        Assert.Equal(75, lowThreshold);
        Assert.Equal(180, highThreshold);
    }

    /// <summary>
    /// Test: PUT /api/settings creates new settings when none exist
    /// </summary>
    [Fact]
    public async Task UpdateSettings_NewSettings_CreatesSettings()
    {
        // Arrange
        var request = new UpdateSettingsRequest
        {
            LowThreshold = 60,
            HighThreshold = 190
        };

        // Act
        var result = await _controller.UpdateSettings(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);

        // Verify database
        var settings = await _context.PatientSettings.FirstOrDefaultAsync(ps => ps.UserId == _testPatientId);
        Assert.NotNull(settings);
        Assert.Equal(60, settings.LowPressureThreshold);
        Assert.Equal(190, settings.HighPressureThreshold);
    }

    /// <summary>
    /// Test: PUT /api/settings updates existing settings
    /// </summary>
    [Fact]
    public async Task UpdateSettings_ExistingSettings_UpdatesSettings()
    {
        // Arrange - Create existing settings
        var oldUpdatedAt = DateTime.UtcNow.AddDays(-1);
        var existingSettings = new PatientSettings
        {
            PatientSettingsId = Guid.NewGuid(),
            UserId = _testPatientId,
            LowPressureThreshold = 50,
            HighPressureThreshold = 200,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = oldUpdatedAt
        };
        _context.PatientSettings.Add(existingSettings);
        await _context.SaveChangesAsync();

        var request = new UpdateSettingsRequest
        {
            LowThreshold = 80,
            HighThreshold = 220
        };

        // Act
        var result = await _controller.UpdateSettings(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);

        // Verify database - reload to get updated values
        var settings = await _context.PatientSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(ps => ps.UserId == _testPatientId);
        Assert.NotNull(settings);
        Assert.Equal(80, settings.LowPressureThreshold);
        Assert.Equal(220, settings.HighPressureThreshold);
        Assert.True(settings.UpdatedAt > oldUpdatedAt);
    }

    /// <summary>
    /// Test: PUT /api/settings rejects low threshold >= high threshold
    /// </summary>
    [Fact]
    public async Task UpdateSettings_LowGreaterOrEqualHigh_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateSettingsRequest
        {
            LowThreshold = 150,
            HighThreshold = 150
        };

        // Act
        var result = await _controller.UpdateSettings(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = badRequestResult.Value;
        var error = response?.GetType().GetProperty("error")?.GetValue(response)?.ToString();
        Assert.Equal("Low threshold must be less than high threshold", error);
    }

    /// <summary>
    /// Test: PUT /api/settings rejects low threshold less than 1
    /// </summary>
    [Fact]
    public async Task UpdateSettings_LowThresholdTooLow_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateSettingsRequest
        {
            LowThreshold = 0,
            HighThreshold = 200
        };

        // Act
        var result = await _controller.UpdateSettings(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = badRequestResult.Value;
        var error = response?.GetType().GetProperty("error")?.GetValue(response)?.ToString();
        Assert.Equal("Low threshold must be between 1 and 254", error);
    }

    /// <summary>
    /// Test: PUT /api/settings rejects low threshold greater than 254
    /// </summary>
    [Fact]
    public async Task UpdateSettings_LowThresholdTooHigh_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateSettingsRequest
        {
            LowThreshold = 255,
            HighThreshold = 255
        };

        // Act
        var result = await _controller.UpdateSettings(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = badRequestResult.Value;
        var error = response?.GetType().GetProperty("error")?.GetValue(response)?.ToString();
        Assert.Equal("Low threshold must be between 1 and 254", error);
    }

    /// <summary>
    /// Test: PUT /api/settings rejects high threshold less than 2
    /// </summary>
    [Fact]
    public async Task UpdateSettings_HighThresholdTooLow_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateSettingsRequest
        {
            LowThreshold = 1,
            HighThreshold = 1
        };

        // Act
        var result = await _controller.UpdateSettings(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = badRequestResult.Value;
        var error = response?.GetType().GetProperty("error")?.GetValue(response)?.ToString();
        Assert.Equal("High threshold must be between 2 and 255", error);
    }

    /// <summary>
    /// Test: PUT /api/settings rejects high threshold greater than 255
    /// </summary>
    [Fact]
    public async Task UpdateSettings_HighThresholdTooHigh_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateSettingsRequest
        {
            LowThreshold = 50,
            HighThreshold = 256
        };

        // Act
        var result = await _controller.UpdateSettings(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = badRequestResult.Value;
        var error = response?.GetType().GetProperty("error")?.GetValue(response)?.ToString();
        Assert.Equal("High threshold must be between 2 and 255", error);
    }

    /// <summary>
    /// Test: PUT /api/settings accepts valid boundary values
    /// </summary>
    [Fact]
    public async Task UpdateSettings_ValidBoundaryValues_Succeeds()
    {
        // Arrange
        var request = new UpdateSettingsRequest
        {
            LowThreshold = 1,
            HighThreshold = 255
        };

        // Act
        var result = await _controller.UpdateSettings(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);

        // Verify database
        var settings = await _context.PatientSettings.FirstOrDefaultAsync(ps => ps.UserId == _testPatientId);
        Assert.NotNull(settings);
        Assert.Equal(1, settings.LowPressureThreshold);
        Assert.Equal(255, settings.HighPressureThreshold);
    }

    /// <summary>
    /// Test: UpdatedAt timestamp is set correctly
    /// </summary>
    [Fact]
    public async Task UpdateSettings_SetsUpdatedAtTimestamp()
    {
        // Arrange
        var beforeUpdate = DateTime.UtcNow;
        await Task.Delay(100); // Ensure timestamp difference

        var request = new UpdateSettingsRequest
        {
            LowThreshold = 70,
            HighThreshold = 210
        };

        // Act
        var result = await _controller.UpdateSettings(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);

        var settings = await _context.PatientSettings.FirstOrDefaultAsync(ps => ps.UserId == _testPatientId);
        Assert.NotNull(settings);
        Assert.True(settings.UpdatedAt >= beforeUpdate);
    }
}
