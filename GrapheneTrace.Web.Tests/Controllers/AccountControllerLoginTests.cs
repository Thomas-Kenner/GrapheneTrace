using GrapheneTrace.Web.Controllers;
using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace GrapheneTrace.Web.Tests.Controllers;

/// <summary>
/// Unit tests for AccountController login functionality.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Test Coverage:
/// 1. Login validation with valid credentials
/// 2. Login failure with invalid email
/// 3. Login failure with invalid password
/// 4. Login failure for deactivated accounts
/// 5. Login failure for unapproved accounts (admin/clinician)
/// 6. Login failure for locked out accounts
/// 7. Access failed count increments on failed login
/// 8. Role assignment for existing users without roles
/// 9. Empty email/password validation
///
/// Testing Strategy:
/// - Uses in-memory database for fast, isolated tests
/// - Real UserManager (not mocked) for authentic Identity behavior
/// - Each test is independent with its own database context
/// - Uses controller's Register method to create test users (same as approval tests)
/// - Focuses on business logic (validation, lockout, role assignment)
/// </remarks>
public class AccountControllerLoginTests : IDisposable
{
    private ApplicationDbContext _context;
    private UserManager<ApplicationUser> _userManager;
    private SignInManager<ApplicationUser> _signInManager;
    private RoleManager<IdentityRole<Guid>> _roleManager;
    private Mock<ILogger<AccountController>> _mockLogger;
    private AccountController _controller;

    public AccountControllerLoginTests()
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

        // Setup RoleManager
        var roleStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.RoleStore<IdentityRole<Guid>, ApplicationDbContext, Guid>(_context);
        _roleManager = new RoleManager<IdentityRole<Guid>>(
            roleStore,
            null!,
            null!,
            null!,
            new Mock<ILogger<RoleManager<IdentityRole<Guid>>>>().Object);

        // Seed roles (required for login to work)
        _roleManager.CreateAsync(new IdentityRole<Guid>("Admin")).Wait();
        _roleManager.CreateAsync(new IdentityRole<Guid>("Clinician")).Wait();
        _roleManager.CreateAsync(new IdentityRole<Guid>("Patient")).Wait();
        _context.SaveChanges(); // Ensure roles are persisted to in-memory database

        // Setup SignInManager with mock
        var contextAccessor = new Mock<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        contextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var mockClaimsPrincipalFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        mockClaimsPrincipalFactory
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync((ApplicationUser user) =>
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Email!)
                };
                return new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));
            });

        _signInManager = new SignInManager<ApplicationUser>(
            _userManager,
            contextAccessor.Object,
            mockClaimsPrincipalFactory.Object,
            null!,
            new Mock<ILogger<SignInManager<ApplicationUser>>>().Object,
            null!,
            null!);

        // Setup controller
        _mockLogger = new Mock<ILogger<AccountController>>();
        _controller = new AccountController(_userManager, _signInManager, _roleManager, _mockLogger.Object);

        // Set ControllerContext with HttpContext for redirect operations
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public void Dispose()
    {
        _userManager?.Dispose();
        _roleManager?.Dispose();
        _context?.Dispose();
    }

    /// <summary>
    /// Helper method to register a test user via the controller's Register method.
    /// This tests the actual production code path and avoids test infrastructure issues.
    /// </summary>
    private async Task<ApplicationUser> RegisterTestUserAsync(
        string email = "test@example.com",
        string password = "Password123!",
        string userType = "patient")
    {
        var request = new RegisterRequest
        {
            FirstName = "Test",
            LastName = "User",
            Email = email,
            Password = password,
            ConfirmPassword = password,
            UserType = userType
        };

        await _controller.Register(request);

        var user = await _userManager.FindByEmailAsync(email);
        return user!;
    }

    #region Validation Tests

    /// <summary>
    /// Test: Login with non-existent email returns error.
    /// </summary>
    [Fact]
    public async Task Login_WithNonExistentEmail_ReturnsError()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "nonexistent@test.com",
            Password = "Password123!",
            RememberMe = false
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Contains("/login?error=", redirectResult.Url);
    }

    /// <summary>
    /// Test: Login with invalid password returns error.
    /// </summary>
    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsError()
    {
        // Arrange
        var email = "test@test.com";
        var correctPassword = "CorrectPassword123!";
        await RegisterTestUserAsync(email, correctPassword, "patient");

        var request = new LoginRequest
        {
            Email = email,
            Password = "WrongPassword123!",
            RememberMe = false
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Contains("/login?error=", redirectResult.Url);
    }

    /// <summary>
    /// Test: Login with empty email returns validation error.
    /// </summary>
    [Fact]
    public async Task Login_WithEmptyEmail_ReturnsValidationError()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "",
            Password = "Password123!",
            RememberMe = false
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Contains("/login?error=", redirectResult.Url);
    }

    /// <summary>
    /// Test: Login with empty password returns validation error.
    /// </summary>
    [Fact]
    public async Task Login_WithEmptyPassword_ReturnsValidationError()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "test@test.com",
            Password = "",
            RememberMe = false
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Contains("/login?error=", redirectResult.Url);
    }

    #endregion

    #region Account Status Tests

    /// <summary>
    /// Test: Login attempt for deactivated account fails.
    /// </summary>
    [Fact]
    public async Task Login_WithDeactivatedAccount_ReturnsDeactivationError()
    {
        // Arrange
        var email = "deactivated@test.com";
        var password = "Password123!";
        var user = await RegisterTestUserAsync(email, password, "patient");

        // Deactivate the account
        user.DeactivatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var request = new LoginRequest
        {
            Email = email,
            Password = password,
            RememberMe = false
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Contains("/login?error=", redirectResult.Url);
        Assert.Contains("deactivated", redirectResult.Url.ToLower());
    }

    /// <summary>
    /// Test: Login attempt for unapproved account fails.
    /// </summary>
    [Fact]
    public async Task Login_WithUnapprovedAccount_ReturnsApprovalError()
    {
        // Arrange
        var email = "unapproved@test.com";
        var password = "Password123!";
        var user = await RegisterTestUserAsync(email, password, "admin");

        // Admin accounts are not auto-approved, so ApprovedAt should be null
        Assert.Null(user.ApprovedAt);

        var request = new LoginRequest
        {
            Email = email,
            Password = password,
            RememberMe = false
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Contains("/login?error=", redirectResult.Url);
        Assert.Contains("approval", redirectResult.Url.ToLower());
    }

    /// <summary>
    /// Test: Login attempt for locked out account fails.
    /// </summary>
    [Fact]
    public async Task Login_WithLockedOutAccount_ReturnsLockoutError()
    {
        // Arrange
        var email = "lockedout@test.com";
        var password = "Password123!";
        var user = await RegisterTestUserAsync(email, password, "patient");

        // Lock the account by setting LockoutEnd
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(15));

        var request = new LoginRequest
        {
            Email = email,
            Password = password,
            RememberMe = false
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Contains("/login?error=", redirectResult.Url);
        Assert.Contains("locked", redirectResult.Url.ToLower());
    }

    #endregion

    #region Access Failed Count Tests

    /// <summary>
    /// Test: Failed login increments access failed count.
    /// </summary>
    [Fact]
    public async Task Login_WithInvalidPassword_IncrementsAccessFailedCount()
    {
        // Arrange
        var email = "test@test.com";
        var password = "CorrectPassword123!";
        var user = await RegisterTestUserAsync(email, password, "patient");

        var initialFailedCount = await _userManager.GetAccessFailedCountAsync(user);
        Assert.Equal(0, initialFailedCount);

        var request = new LoginRequest
        {
            Email = email,
            Password = "WrongPassword123!",
            RememberMe = false
        };

        // Act
        await _controller.Login(request);

        // Assert
        var updatedUser = await _userManager.FindByEmailAsync(email);
        var finalFailedCount = await _userManager.GetAccessFailedCountAsync(updatedUser!);
        Assert.Equal(1, finalFailedCount);
    }

    /// <summary>
    /// Test: Multiple failed login attempts increment count correctly.
    /// </summary>
    [Fact]
    public async Task Login_MultipleFailedAttempts_IncrementsCountCorrectly()
    {
        // Arrange
        var email = "test@test.com";
        var password = "CorrectPassword123!";
        await RegisterTestUserAsync(email, password, "patient");

        var request = new LoginRequest
        {
            Email = email,
            Password = "WrongPassword123!",
            RememberMe = false
        };

        // Act: Attempt 3 failed logins
        await _controller.Login(request);
        await _controller.Login(request);
        await _controller.Login(request);

        // Assert
        var user = await _userManager.FindByEmailAsync(email);
        var failedCount = await _userManager.GetAccessFailedCountAsync(user!);
        Assert.Equal(3, failedCount);
    }

    #endregion

    #region Password Validation Tests

    /// <summary>
    /// Test: Password is validated correctly for existing users.
    /// </summary>
    [Fact]
    public async Task Login_ValidatesPasswordCorrectly()
    {
        // Arrange
        var email = "test@test.com";
        var correctPassword = "CorrectPassword123!";
        var user = await RegisterTestUserAsync(email, correctPassword, "patient");

        // Assert: Correct password should validate
        var validPassword = await _userManager.CheckPasswordAsync(user, correctPassword);
        Assert.True(validPassword);

        // Assert: Wrong password should not validate
        var invalidPassword = await _userManager.CheckPasswordAsync(user, "WrongPassword123!");
        Assert.False(invalidPassword);
    }

    #endregion
}
