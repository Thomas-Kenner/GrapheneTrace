using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using GrapheneTrace.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.AspNetCore.DataProtection;

namespace GrapheneTrace.Web.Tests.Services;

/// <summary>
/// Unit tests for DatabaseSeeder service.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Test Coverage:
/// 1. System account creation when it doesn't exist
/// 2. Password reset when System account already exists
/// 3. System account is auto-approved with correct fields
/// 4. Seeding respects configuration enabled/disabled flag
/// 5. Error handling and logging
///
/// Testing Strategy:
/// - Uses in-memory database for fast, isolated tests
/// - Real UserManager (not mocked) to ensure password hashing works correctly
/// - Mocked IConfiguration and ILogger for control over behavior
/// - Each test is independent with its own database context
/// </remarks>
public class DatabaseSeederTests : IDisposable
{
    private ApplicationDbContext _context;
    private UserManager<ApplicationUser> _userManager;
    private Mock<ILogger<DatabaseSeeder>> _mockLogger;
    private Mock<IConfiguration> _mockConfiguration;
    private DatabaseSeeder _seeder;

    private const string SystemEmail = "system@graphenetrace.local";
    private const string SystemPassword = "System@Admin123";

    public DatabaseSeederTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB per test
            .Options;

        _context = new ApplicationDbContext(options);

        // Setup UserManager with real Identity configuration
        var userStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<ApplicationUser, IdentityRole<Guid>, ApplicationDbContext, Guid>(_context);

        _userManager = new UserManager<ApplicationUser>(
            userStore,
            null!, // Options
            new PasswordHasher<ApplicationUser>(),
            null!, // User validators
            null!, // Password validators
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!, // Services
            new Mock<ILogger<UserManager<ApplicationUser>>>().Object);

        // Register simple token provider for password reset functionality
        _userManager.RegisterTokenProvider("Default", new TestTwoFactorTokenProvider());

        // Setup mocks
        _mockLogger = new Mock<ILogger<DatabaseSeeder>>();
        _mockConfiguration = new Mock<IConfiguration>();

        // Default: seeding enabled - mock the indexer instead of GetValue extension method
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("true");
        _mockConfiguration.Setup(c => c.GetSection("DatabaseSeeding:Enabled")).Returns(mockSection.Object);

        _seeder = new DatabaseSeeder(_userManager, _mockLogger.Object, _mockConfiguration.Object);
    }

    public void Dispose()
    {
        _userManager?.Dispose();
        _context?.Dispose();
    }

    /// <summary>
    /// Test: System account should be created when it doesn't exist.
    /// </summary>
    [Fact]
    public async Task SeedAsync_CreatesSystemAccount_WhenItDoesNotExist()
    {
        // Act
        await _seeder.SeedAsync();

        // Assert
        var systemUser = await _userManager.FindByEmailAsync(SystemEmail);

        Assert.NotNull(systemUser);
        Assert.Equal(SystemEmail, systemUser.Email);
        Assert.Equal(SystemEmail, systemUser.UserName);
        Assert.Equal("System", systemUser.FirstName);
        Assert.Equal("Administrator", systemUser.LastName);
        Assert.Equal("admin", systemUser.UserType);
        Assert.True(systemUser.EmailConfirmed);

        // Verify password is set correctly
        var passwordValid = await _userManager.CheckPasswordAsync(systemUser, SystemPassword);
        Assert.True(passwordValid);
    }

    /// <summary>
    /// Test: System account should be auto-approved with correct fields.
    /// </summary>
    [Fact]
    public async Task SeedAsync_SystemAccountIsAutoApproved()
    {
        // Act
        await _seeder.SeedAsync();

        // Assert
        var systemUser = await _userManager.FindByEmailAsync(SystemEmail);

        Assert.NotNull(systemUser);
        Assert.NotNull(systemUser.ApprovedAt);
        Assert.Null(systemUser.ApprovedBy); // System approves itself (no approver)
        Assert.Equal(systemUser.CreatedAt, systemUser.ApprovedAt.Value, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Test: When System account exists, password should be reset to default.
    /// </summary>
    [Fact]
    public async Task SeedAsync_ResetsPassword_WhenSystemAccountExists()
    {
        // Arrange: Create System account with different password
        var systemUser = new ApplicationUser
        {
            UserName = SystemEmail,
            Email = SystemEmail,
            EmailConfirmed = true,
            FirstName = "System",
            LastName = "Administrator",
            UserType = "admin",
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(systemUser, "DifferentPassword123!");
        Assert.True(createResult.Succeeded);

        // Verify old password works
        var oldPasswordValid = await _userManager.CheckPasswordAsync(systemUser, "DifferentPassword123!");
        Assert.True(oldPasswordValid);

        // Act: Run seeder
        await _seeder.SeedAsync();

        // Assert: Password should be reset to default
        systemUser = await _userManager.FindByEmailAsync(SystemEmail);
        Assert.NotNull(systemUser);

        var newPasswordValid = await _userManager.CheckPasswordAsync(systemUser, SystemPassword);
        Assert.True(newPasswordValid);

        var oldPasswordStillValid = await _userManager.CheckPasswordAsync(systemUser, "DifferentPassword123!");
        Assert.False(oldPasswordStillValid);
    }

    /// <summary>
    /// Test: Seeding should be skipped when disabled in configuration.
    /// </summary>
    [Fact]
    public async Task SeedAsync_SkipsSeeding_WhenDisabledInConfiguration()
    {
        // Arrange: Disable seeding
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("false");
        _mockConfiguration.Setup(c => c.GetSection("DatabaseSeeding:Enabled")).Returns(mockSection.Object);

        // Act
        await _seeder.SeedAsync();

        // Assert: System account should NOT be created
        var systemUser = await _userManager.FindByEmailAsync(SystemEmail);
        Assert.Null(systemUser);

        // Verify log message
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Database seeding is disabled")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test: Seeder should log when creating new System account.
    /// </summary>
    [Fact]
    public async Task SeedAsync_LogsAccountCreation_WhenSystemAccountIsCreated()
    {
        // Act
        await _seeder.SeedAsync();

        // Assert: Verify creation log message
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("System account created successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test: Seeder should log when resetting password of existing account.
    /// </summary>
    [Fact]
    public async Task SeedAsync_LogsPasswordReset_WhenSystemAccountExists()
    {
        // Arrange: Create existing System account
        var systemUser = new ApplicationUser
        {
            UserName = SystemEmail,
            Email = SystemEmail,
            EmailConfirmed = true,
            FirstName = "System",
            LastName = "Administrator",
            UserType = "admin",
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow
        };
        await _userManager.CreateAsync(systemUser, "OldPassword123!");

        // Act
        await _seeder.SeedAsync();

        // Assert: Verify password reset log messages
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("resetting password to default")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("System account password reset successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test: Seeder should be idempotent (safe to run multiple times).
    /// </summary>
    [Fact]
    public async Task SeedAsync_IsIdempotent_CanBeRunMultipleTimes()
    {
        // Act: Run seeder multiple times
        await _seeder.SeedAsync();
        await _seeder.SeedAsync();
        await _seeder.SeedAsync();

        // Assert: Only one System account should exist
        var allUsers = _userManager.Users.Where(u => u.Email == SystemEmail).ToList();
        Assert.Single(allUsers);

        // Verify password still works
        var systemUser = allUsers.First();
        var passwordValid = await _userManager.CheckPasswordAsync(systemUser, SystemPassword);
        Assert.True(passwordValid);
    }

    /// <summary>
    /// Test: System account should not be deactivated.
    /// </summary>
    [Fact]
    public async Task SeedAsync_SystemAccountIsNotDeactivated()
    {
        // Act
        await _seeder.SeedAsync();

        // Assert
        var systemUser = await _userManager.FindByEmailAsync(SystemEmail);

        Assert.NotNull(systemUser);
        Assert.Null(systemUser.DeactivatedAt);
    }

    /// <summary>
    /// Test: Password hashing should use Identity's PBKDF2 algorithm.
    /// </summary>
    [Fact]
    public async Task SeedAsync_UsesIdentityPasswordHashing()
    {
        // Act
        await _seeder.SeedAsync();

        // Assert
        var systemUser = await _userManager.FindByEmailAsync(SystemEmail);

        Assert.NotNull(systemUser);
        Assert.NotNull(systemUser.PasswordHash);
        Assert.NotEmpty(systemUser.PasswordHash);

        // Identity password hashes are base64-encoded and start with specific format
        // They should NOT be BCrypt format (which starts with $2)
        Assert.DoesNotContain("$2", systemUser.PasswordHash);

        // Verify the hash can be validated by UserManager
        var passwordValid = await _userManager.CheckPasswordAsync(systemUser, SystemPassword);
        Assert.True(passwordValid);
    }

    /// <summary>
    /// Test: Seeder should log completion message.
    /// </summary>
    [Fact]
    public async Task SeedAsync_LogsCompletionMessage()
    {
        // Act
        await _seeder.SeedAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Database seeding completed successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

/// <summary>
/// Simple token provider for testing password reset functionality.
/// </summary>
internal class TestTwoFactorTokenProvider : IUserTwoFactorTokenProvider<ApplicationUser>
{
    public Task<string> GenerateAsync(string purpose, UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        return Task.FromResult("test-token");
    }

    public Task<bool> ValidateAsync(string purpose, string token, UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        return Task.FromResult(true);
    }

    public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        return Task.FromResult(true);
    }
}
