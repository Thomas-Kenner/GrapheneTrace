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
/// 4. Test patient account creation (auto-approved)
/// 5. Approved clinician account creation (approved by system admin)
/// 6. Unapproved clinician account creation (pending approval)
/// 7. All test accounts are reset on each seeding run
/// 8. Approval statuses are maintained correctly across re-seeding
/// 9. Consistent timestamps (build time) used across all accounts
/// 10. Seeding respects configuration enabled/disabled flag
/// 11. Error handling and logging
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
    // Author: SID:2412494 - Added RoleManager field for updated constructor
    private RoleManager<IdentityRole<Guid>> _roleManager;
    private Mock<ILogger<DatabaseSeeder>> _mockLogger;
    private Mock<IConfiguration> _mockConfiguration;
    private DatabaseSeeder _seeder;

    private const string SystemEmail = "system@graphenetrace.local";
    private const string SystemPassword = "System@Admin123";

    private const string TestPatientEmail = "patient.test@graphenetrace.local";
    private const string TestPatientPassword = "Patient@Test123";

    private const string ApprovedClinicianEmail = "clinician.approved@graphenetrace.local";
    private const string ApprovedClinicianPassword = "Clinician@Approved123";

    private const string UnapprovedClinicianEmail = "clinician.pending@graphenetrace.local";
    private const string UnapprovedClinicianPassword = "Clinician@Pending123";

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

        // Author: SID:2412494 - Setup RoleManager for updated DatabaseSeeder constructor
        var roleStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.RoleStore<IdentityRole<Guid>, ApplicationDbContext, Guid>(_context);
        _roleManager = new RoleManager<IdentityRole<Guid>>(
            roleStore,
            null!, // Role validators
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new Mock<ILogger<RoleManager<IdentityRole<Guid>>>>().Object);

        // Setup mocks
        _mockLogger = new Mock<ILogger<DatabaseSeeder>>();
        _mockConfiguration = new Mock<IConfiguration>();

        // Default: seeding enabled - mock the indexer instead of GetValue extension method
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("true");
        _mockConfiguration.Setup(c => c.GetSection("DatabaseSeeding:Enabled")).Returns(mockSection.Object);

        // Author: SID:2412494 - Updated constructor call to include RoleManager and ApplicationDbContext
        _seeder = new DatabaseSeeder(_userManager, _roleManager, _mockLogger.Object, _mockConfiguration.Object, _context);
    }

    public void Dispose()
    {
        _userManager?.Dispose();
        _roleManager?.Dispose();  // Author: SID:2412494 - Added RoleManager disposal
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

    /// <summary>
    /// Test: Test patient account should be created when seeding runs.
    /// </summary>
    [Fact]
    public async Task SeedAsync_CreatesTestPatientAccount()
    {
        // Act
        await _seeder.SeedAsync();

        // Assert
        var testPatient = await _userManager.FindByEmailAsync(TestPatientEmail);

        Assert.NotNull(testPatient);
        Assert.Equal(TestPatientEmail, testPatient.Email);
        Assert.Equal(TestPatientEmail, testPatient.UserName);
        Assert.Equal("Test", testPatient.FirstName);
        Assert.Equal("Patient", testPatient.LastName);
        Assert.Equal("patient", testPatient.UserType);
        Assert.True(testPatient.EmailConfirmed);
        Assert.NotNull(testPatient.ApprovedAt); // Patients are auto-approved
        Assert.Null(testPatient.ApprovedBy); // No approver for patients

        // Verify password
        var passwordValid = await _userManager.CheckPasswordAsync(testPatient, TestPatientPassword);
        Assert.True(passwordValid);
    }

    /// <summary>
    /// Test: Approved clinician account should be created with correct approval status.
    /// </summary>
    [Fact]
    public async Task SeedAsync_CreatesApprovedClinicianAccount()
    {
        // Act
        await _seeder.SeedAsync();

        // Assert
        var systemUser = await _userManager.FindByEmailAsync(SystemEmail);
        var approvedClinician = await _userManager.FindByEmailAsync(ApprovedClinicianEmail);

        Assert.NotNull(approvedClinician);
        Assert.Equal(ApprovedClinicianEmail, approvedClinician.Email);
        Assert.Equal(ApprovedClinicianEmail, approvedClinician.UserName);
        Assert.Equal("Approved", approvedClinician.FirstName);
        Assert.Equal("Clinician", approvedClinician.LastName);
        Assert.Equal("clinician", approvedClinician.UserType);
        Assert.True(approvedClinician.EmailConfirmed);
        Assert.NotNull(approvedClinician.ApprovedAt); // Should be approved
        Assert.Equal(systemUser.Id, approvedClinician.ApprovedBy); // Approved by system admin

        // Verify password
        var passwordValid = await _userManager.CheckPasswordAsync(approvedClinician, ApprovedClinicianPassword);
        Assert.True(passwordValid);
    }

    /// <summary>
    /// Test: Unapproved clinician account should be created without approval.
    /// </summary>
    [Fact]
    public async Task SeedAsync_CreatesUnapprovedClinicianAccount()
    {
        // Act
        await _seeder.SeedAsync();

        // Assert
        var unapprovedClinician = await _userManager.FindByEmailAsync(UnapprovedClinicianEmail);

        Assert.NotNull(unapprovedClinician);
        Assert.Equal(UnapprovedClinicianEmail, unapprovedClinician.Email);
        Assert.Equal(UnapprovedClinicianEmail, unapprovedClinician.UserName);
        Assert.Equal("Pending", unapprovedClinician.FirstName);
        Assert.Equal("Clinician", unapprovedClinician.LastName);
        Assert.Equal("clinician", unapprovedClinician.UserType);
        Assert.True(unapprovedClinician.EmailConfirmed);
        Assert.Null(unapprovedClinician.ApprovedAt); // Should NOT be approved
        Assert.Null(unapprovedClinician.ApprovedBy);

        // Verify password
        var passwordValid = await _userManager.CheckPasswordAsync(unapprovedClinician, UnapprovedClinicianPassword);
        Assert.True(passwordValid);
    }

    /// <summary>
    /// Test: All test accounts should be reset when seeding runs multiple times.
    /// </summary>
    [Fact]
    public async Task SeedAsync_ResetsAllTestAccountPasswords_WhenRunMultipleTimes()
    {
        // Arrange: Create existing accounts with different passwords
        var testPatient = new ApplicationUser
        {
            UserName = TestPatientEmail,
            Email = TestPatientEmail,
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "Patient",
            UserType = "patient",
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow
        };
        await _userManager.CreateAsync(testPatient, "OldPassword123!");

        var approvedClinician = new ApplicationUser
        {
            UserName = ApprovedClinicianEmail,
            Email = ApprovedClinicianEmail,
            EmailConfirmed = true,
            FirstName = "Approved",
            LastName = "Clinician",
            UserType = "clinician",
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow
        };
        await _userManager.CreateAsync(approvedClinician, "OldPassword456!");

        // Act: Run seeder
        await _seeder.SeedAsync();

        // Assert: Passwords should be reset
        testPatient = await _userManager.FindByEmailAsync(TestPatientEmail);
        var testPatientPasswordValid = await _userManager.CheckPasswordAsync(testPatient, TestPatientPassword);
        Assert.True(testPatientPasswordValid);

        approvedClinician = await _userManager.FindByEmailAsync(ApprovedClinicianEmail);
        var approvedClinicianPasswordValid = await _userManager.CheckPasswordAsync(approvedClinician, ApprovedClinicianPassword);
        Assert.True(approvedClinicianPasswordValid);
    }

    /// <summary>
    /// Test: Unapproved clinician should remain unapproved after re-seeding.
    /// </summary>
    [Fact]
    public async Task SeedAsync_MaintainsUnapprovedStatus_WhenRunMultipleTimes()
    {
        // Arrange: Create unapproved clinician, then manually approve it
        await _seeder.SeedAsync();

        var unapprovedClinician = await _userManager.FindByEmailAsync(UnapprovedClinicianEmail);
        Assert.NotNull(unapprovedClinician);
        Assert.Null(unapprovedClinician.ApprovedAt);

        // Manually approve the account
        unapprovedClinician.ApprovedAt = DateTime.UtcNow;
        unapprovedClinician.ApprovedBy = Guid.NewGuid();
        await _userManager.UpdateAsync(unapprovedClinician);

        // Verify it's approved
        unapprovedClinician = await _userManager.FindByEmailAsync(UnapprovedClinicianEmail);
        Assert.NotNull(unapprovedClinician.ApprovedAt);

        // Act: Run seeder again
        await _seeder.SeedAsync();

        // Assert: Should be reset to unapproved state
        unapprovedClinician = await _userManager.FindByEmailAsync(UnapprovedClinicianEmail);
        Assert.NotNull(unapprovedClinician);
        Assert.Null(unapprovedClinician.ApprovedAt); // Reset to unapproved
        Assert.Null(unapprovedClinician.ApprovedBy);
    }

    /// <summary>
    /// Test: All seeded accounts should use consistent timestamps (build time).
    /// </summary>
    [Fact]
    public async Task SeedAsync_UsesConsistentBuildTime_ForAllAccounts()
    {
        // Act
        await _seeder.SeedAsync();

        // Assert
        var systemUser = await _userManager.FindByEmailAsync(SystemEmail);
        var testPatient = await _userManager.FindByEmailAsync(TestPatientEmail);
        var approvedClinician = await _userManager.FindByEmailAsync(ApprovedClinicianEmail);
        var unapprovedClinician = await _userManager.FindByEmailAsync(UnapprovedClinicianEmail);

        Assert.NotNull(systemUser);
        Assert.NotNull(testPatient);
        Assert.NotNull(approvedClinician);
        Assert.NotNull(unapprovedClinician);

        // All accounts should have timestamps within a few seconds of each other
        var timeWindow = TimeSpan.FromSeconds(5);
        Assert.Equal(systemUser.CreatedAt, testPatient.CreatedAt, timeWindow);
        Assert.Equal(systemUser.CreatedAt, approvedClinician.CreatedAt, timeWindow);
        Assert.Equal(systemUser.CreatedAt, unapprovedClinician.CreatedAt, timeWindow);

        // Approved accounts should have matching ApprovedAt times
        Assert.Equal(systemUser.ApprovedAt!.Value, testPatient.ApprovedAt!.Value, timeWindow);
        Assert.Equal(systemUser.ApprovedAt!.Value, approvedClinician.ApprovedAt!.Value, timeWindow);
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
