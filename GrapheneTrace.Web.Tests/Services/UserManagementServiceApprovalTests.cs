using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using GrapheneTrace.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace GrapheneTrace.Web.Tests.Services;

/// <summary>
/// Unit tests for UserManagementService account approval functionality.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Test Coverage:
/// 1. ApproveUserAsync successfully approves pending users
/// 2. ApprovedAt timestamp is set correctly
/// 3. ApprovedBy foreign key tracks approving admin
/// 4. Idempotent behavior - rejects already-approved users
/// 5. Error handling for non-existent users
/// 6. Audit logging for all approval operations
/// 7. Race condition protection via idempotency
/// 8. Integration with Identity UserManager
///
/// Testing Strategy:
/// - Uses in-memory database for fast, isolated tests
/// - Real UserManager (not mocked) to ensure proper persistence
/// - Mocked ILogger for verification of audit trail
/// - Each test is independent with its own database context
/// </remarks>
public class UserManagementServiceApprovalTests : IDisposable
{
    private ApplicationDbContext _context;
    private UserManager<ApplicationUser> _userManager;
    private Mock<ILogger<UserManagementService>> _mockLogger;
    private Mock<IDbContextFactory<ApplicationDbContext>> _mockDbContextFactory;
    private UserManagementService _service;

    public UserManagementServiceApprovalTests()
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

        // Setup mocks
        _mockLogger = new Mock<ILogger<UserManagementService>>();

        // Author: SID:2412494
        // Added mock for IDbContextFactory to support new assignment management methods
        _mockDbContextFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        _mockDbContextFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ApplicationDbContext(options));

        _service = new UserManagementService(_userManager, _mockLogger.Object, _mockDbContextFactory.Object);
    }

    public void Dispose()
    {
        _userManager?.Dispose();
        _context?.Dispose();
    }

    /// <summary>
    /// Test: ApproveUserAsync should successfully approve a pending user.
    /// </summary>
    [Fact]
    public async Task ApproveUserAsync_SuccessfullyApprovesUser_WhenUserIsPending()
    {
        // Arrange: Create a pending clinician account
        var pendingUser = new ApplicationUser
        {
            UserName = "clinician@test.com",
            Email = "clinician@test.com",
            EmailConfirmed = true,
            FirstName = "Jane",
            LastName = "Doe",
            UserType = "clinician",
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = null, // Pending approval
            ApprovedBy = null
        };
        await _userManager.CreateAsync(pendingUser, "Password123!");

        // Create an admin user to perform the approval
        var adminUser = new ApplicationUser
        {
            UserName = "admin@test.com",
            Email = "admin@test.com",
            EmailConfirmed = true,
            FirstName = "Admin",
            LastName = "User",
            UserType = "admin",
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow
        };
        await _userManager.CreateAsync(adminUser, "AdminPass123!");

        // Act
        var result = await _service.ApproveUserAsync(pendingUser.Id, adminUser.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("User approved successfully", result.Message);

        // Verify user was actually updated in database
        var updatedUser = await _userManager.FindByIdAsync(pendingUser.Id.ToString());
        Assert.NotNull(updatedUser);
        Assert.NotNull(updatedUser.ApprovedAt);
        Assert.Equal(adminUser.Id, updatedUser.ApprovedBy);
    }

    /// <summary>
    /// Test: ApprovedAt timestamp should be set to current UTC time.
    /// </summary>
    [Fact]
    public async Task ApproveUserAsync_SetsApprovedAtToCurrentTime()
    {
        // Arrange
        var pendingUser = new ApplicationUser
        {
            UserName = "user@test.com",
            Email = "user@test.com",
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "User",
            UserType = "clinician",
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = null
        };
        await _userManager.CreateAsync(pendingUser, "Password123!");

        var adminId = Guid.NewGuid();
        var beforeApproval = DateTime.UtcNow;

        // Act
        await _service.ApproveUserAsync(pendingUser.Id, adminId);

        var afterApproval = DateTime.UtcNow;

        // Assert
        var updatedUser = await _userManager.FindByIdAsync(pendingUser.Id.ToString());
        Assert.NotNull(updatedUser);
        Assert.NotNull(updatedUser.ApprovedAt);

        // ApprovedAt should be between before and after timestamps (within 5 second tolerance)
        Assert.True(updatedUser.ApprovedAt >= beforeApproval.AddSeconds(-5));
        Assert.True(updatedUser.ApprovedAt <= afterApproval.AddSeconds(5));
    }

    /// <summary>
    /// Test: ApprovedBy foreign key should reference the approving admin.
    /// </summary>
    [Fact]
    public async Task ApproveUserAsync_SetsApprovedByForeignKey()
    {
        // Arrange
        var pendingUser = new ApplicationUser
        {
            UserName = "user@test.com",
            Email = "user@test.com",
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "User",
            UserType = "clinician",
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = null
        };
        await _userManager.CreateAsync(pendingUser, "Password123!");

        var admin = new ApplicationUser
        {
            UserName = "admin@test.com",
            Email = "admin@test.com",
            EmailConfirmed = true,
            FirstName = "Admin",
            LastName = "User",
            UserType = "admin",
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow
        };
        await _userManager.CreateAsync(admin, "AdminPass123!");

        // Act
        await _service.ApproveUserAsync(pendingUser.Id, admin.Id);

        // Assert
        var updatedUser = await _userManager.FindByIdAsync(pendingUser.Id.ToString());
        Assert.NotNull(updatedUser);
        Assert.Equal(admin.Id, updatedUser.ApprovedBy);

        // Verify foreign key relationship is valid
        var approvingAdmin = await _userManager.FindByIdAsync(updatedUser.ApprovedBy.ToString()!);
        Assert.NotNull(approvingAdmin);
        Assert.Equal(admin.Email, approvingAdmin.Email);
    }

    /// <summary>
    /// Test: Approving an already-approved user should fail with idempotent message.
    /// </summary>
    [Fact]
    public async Task ApproveUserAsync_RejectsAlreadyApprovedUser()
    {
        // Arrange: Create a user that's already approved
        var approvedUser = new ApplicationUser
        {
            UserName = "user@test.com",
            Email = "user@test.com",
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "User",
            UserType = "clinician",
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow.AddDays(-1), // Already approved yesterday
            ApprovedBy = Guid.NewGuid()
        };
        await _userManager.CreateAsync(approvedUser, "Password123!");

        var adminId = Guid.NewGuid();

        // Act
        var result = await _service.ApproveUserAsync(approvedUser.Id, adminId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("User already approved", result.Message);

        // Verify original approval data was NOT changed
        var user = await _userManager.FindByIdAsync(approvedUser.Id.ToString());
        Assert.NotNull(user);
        Assert.Equal(approvedUser.ApprovedAt, user.ApprovedAt);
        Assert.Equal(approvedUser.ApprovedBy, user.ApprovedBy);
    }

    /// <summary>
    /// Test: Approving a non-existent user should fail gracefully.
    /// </summary>
    [Fact]
    public async Task ApproveUserAsync_ReturnsError_WhenUserDoesNotExist()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        // Act
        var result = await _service.ApproveUserAsync(nonExistentUserId, adminId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("User not found", result.Message);
    }

    /// <summary>
    /// Test: ApproveUserAsync should log the approval for audit trail.
    /// </summary>
    [Fact]
    public async Task ApproveUserAsync_LogsApprovalAction()
    {
        // Arrange
        var pendingUser = new ApplicationUser
        {
            UserName = "user@test.com",
            Email = "user@test.com",
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "User",
            UserType = "clinician",
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = null
        };
        await _userManager.CreateAsync(pendingUser, "Password123!");

        var adminId = Guid.NewGuid();

        // Act
        await _service.ApproveUserAsync(pendingUser.Id, adminId);

        // Assert: Verify audit log was created
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("approved by admin") &&
                    v.ToString()!.Contains(pendingUser.Email)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test: Attempting to approve already-approved user should log a warning.
    /// </summary>
    [Fact]
    public async Task ApproveUserAsync_LogsWarning_WhenUserAlreadyApproved()
    {
        // Arrange
        var approvedUser = new ApplicationUser
        {
            UserName = "user@test.com",
            Email = "user@test.com",
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "User",
            UserType = "clinician",
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow,
            ApprovedBy = Guid.NewGuid()
        };
        await _userManager.CreateAsync(approvedUser, "Password123!");

        var adminId = Guid.NewGuid();

        // Act
        await _service.ApproveUserAsync(approvedUser.Id, adminId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Attempted to approve already-approved user")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test: Attempting to approve non-existent user should log a warning.
    /// </summary>
    [Fact]
    public async Task ApproveUserAsync_LogsWarning_WhenUserNotFound()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        // Act
        await _service.ApproveUserAsync(nonExistentUserId, adminId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Attempted to approve non-existent user")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test: Idempotency - race condition protection when multiple admins approve simultaneously.
    /// </summary>
    [Fact]
    public async Task ApproveUserAsync_IsIdempotent_PreventsRaceCondition()
    {
        // Arrange
        var pendingUser = new ApplicationUser
        {
            UserName = "user@test.com",
            Email = "user@test.com",
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "User",
            UserType = "clinician",
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = null
        };
        await _userManager.CreateAsync(pendingUser, "Password123!");

        var admin1Id = Guid.NewGuid();
        var admin2Id = Guid.NewGuid();

        // Act: Simulate race condition - two admins approve at the same time
        var result1 = await _service.ApproveUserAsync(pendingUser.Id, admin1Id);
        var result2 = await _service.ApproveUserAsync(pendingUser.Id, admin2Id);

        // Assert: First approval succeeds, second fails
        Assert.True(result1.Success);
        Assert.False(result2.Success);
        Assert.Equal("User already approved", result2.Message);

        // Verify only the first admin is recorded
        var user = await _userManager.FindByIdAsync(pendingUser.Id.ToString());
        Assert.NotNull(user);
        Assert.Equal(admin1Id, user.ApprovedBy);
    }

    /// <summary>
    /// Test: Approval should work for both admin and clinician account types.
    /// </summary>
    [Theory]
    [InlineData("admin")]
    [InlineData("clinician")]
    public async Task ApproveUserAsync_WorksForBothAdminAndClinician(string userType)
    {
        // Arrange
        var pendingUser = new ApplicationUser
        {
            UserName = $"{userType}@test.com",
            Email = $"{userType}@test.com",
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "User",
            UserType = userType,
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = null
        };
        await _userManager.CreateAsync(pendingUser, "Password123!");

        var adminId = Guid.NewGuid();

        // Act
        var result = await _service.ApproveUserAsync(pendingUser.Id, adminId);

        // Assert
        Assert.True(result.Success);

        var user = await _userManager.FindByIdAsync(pendingUser.Id.ToString());
        Assert.NotNull(user);
        Assert.NotNull(user.ApprovedAt);
        Assert.Equal(adminId, user.ApprovedBy);
    }

    /// <summary>
    /// Test: Approval should preserve other user properties unchanged.
    /// </summary>
    [Fact]
    public async Task ApproveUserAsync_PreservesOtherUserProperties()
    {
        // Arrange
        var createdAt = DateTime.UtcNow.AddDays(-5);
        var pendingUser = new ApplicationUser
        {
            UserName = "user@test.com",
            Email = "user@test.com",
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "User",
            UserType = "clinician",
            CreatedAt = createdAt,
            ApprovedAt = null,
            DeactivatedAt = null
        };
        await _userManager.CreateAsync(pendingUser, "Password123!");

        var adminId = Guid.NewGuid();

        // Act
        await _service.ApproveUserAsync(pendingUser.Id, adminId);

        // Assert
        var user = await _userManager.FindByIdAsync(pendingUser.Id.ToString());
        Assert.NotNull(user);
        Assert.Equal("Test", user.FirstName);
        Assert.Equal("User", user.LastName);
        Assert.Equal("user@test.com", user.Email);
        Assert.Equal("clinician", user.UserType);
        Assert.Equal(createdAt, user.CreatedAt);
        Assert.Null(user.DeactivatedAt);
    }

    /// <summary>
    /// Test: Multiple users can be approved by the same admin.
    /// </summary>
    [Fact]
    public async Task ApproveUserAsync_AllowsMultipleApprovalsFromSameAdmin()
    {
        // Arrange: Create multiple pending users
        var user1 = new ApplicationUser
        {
            UserName = "user1@test.com",
            Email = "user1@test.com",
            EmailConfirmed = true,
            FirstName = "User",
            LastName = "One",
            UserType = "clinician",
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = null
        };
        await _userManager.CreateAsync(user1, "Password123!");

        var user2 = new ApplicationUser
        {
            UserName = "user2@test.com",
            Email = "user2@test.com",
            EmailConfirmed = true,
            FirstName = "User",
            LastName = "Two",
            UserType = "admin",
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = null
        };
        await _userManager.CreateAsync(user2, "Password123!");

        var adminId = Guid.NewGuid();

        // Act: Same admin approves both users
        var result1 = await _service.ApproveUserAsync(user1.Id, adminId);
        var result2 = await _service.ApproveUserAsync(user2.Id, adminId);

        // Assert: Both approvals succeed
        Assert.True(result1.Success);
        Assert.True(result2.Success);

        // Verify both users reference the same admin
        var updatedUser1 = await _userManager.FindByIdAsync(user1.Id.ToString());
        var updatedUser2 = await _userManager.FindByIdAsync(user2.Id.ToString());

        Assert.Equal(adminId, updatedUser1!.ApprovedBy);
        Assert.Equal(adminId, updatedUser2!.ApprovedBy);
    }
}
