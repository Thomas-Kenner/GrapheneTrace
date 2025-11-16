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
/// Unit tests for AccountController account approval workflow.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Test Coverage:
/// 1. Patient accounts are auto-approved on registration (ApprovedAt set immediately)
/// 2. Admin/Clinician accounts are NOT auto-approved (ApprovedAt = null)
/// 3. Patient auto-approval has ApprovedBy = null (no admin approver)
/// 4. ApprovedAt timestamp is set during patient registration
/// 5. All user types preserve correct approval behavior
///
/// Testing Strategy:
/// - Uses in-memory database for fast, isolated tests
/// - Real UserManager (not mocked) for authentic Identity behavior
/// - Each test is independent with its own database context
/// - Focuses on registration logic (easier to test than login flow)
/// - Login blocking logic is tested via direct user property inspection
/// </remarks>
public class AccountControllerApprovalTests : IDisposable
{
    private ApplicationDbContext _context;
    private UserManager<ApplicationUser> _userManager;
    private RoleManager<IdentityRole<Guid>> _roleManager;
    private Mock<ILogger<AccountController>> _mockLogger;
    private AccountController _controller;

    public AccountControllerApprovalTests()
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

        // Seed roles (required for registration to assign roles)
        _roleManager.CreateAsync(new IdentityRole<Guid>("Admin")).Wait();
        _roleManager.CreateAsync(new IdentityRole<Guid>("Clinician")).Wait();
        _roleManager.CreateAsync(new IdentityRole<Guid>("Patient")).Wait();

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

        var signInManager = new SignInManager<ApplicationUser>(
            _userManager,
            contextAccessor.Object,
            mockClaimsPrincipalFactory.Object,
            null!,
            new Mock<ILogger<SignInManager<ApplicationUser>>>().Object,
            null!,
            null!);

        // Setup controller
        _mockLogger = new Mock<ILogger<AccountController>>();
        _controller = new AccountController(_userManager, signInManager, _roleManager, _mockLogger.Object);

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
    /// Test: Patient accounts should be auto-approved on registration.
    /// </summary>
    [Fact]
    public async Task Register_AutoApprovesPatientAccounts()
    {
        // Arrange
        var request = new RegisterRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "patient@test.com",
            Password = "Patient123!",
            ConfirmPassword = "Patient123!",
            UserType = "patient"
        };

        var beforeRegistration = DateTime.UtcNow;

        // Act
        await _controller.Register(request);

        var afterRegistration = DateTime.UtcNow;

        // Assert
        var user = await _userManager.FindByEmailAsync(request.Email);
        Assert.NotNull(user);
        Assert.NotNull(user.ApprovedAt);

        // ApprovedAt should be set to current time (within 5 second tolerance)
        Assert.True(user.ApprovedAt >= beforeRegistration.AddSeconds(-5));
        Assert.True(user.ApprovedAt <= afterRegistration.AddSeconds(5));
    }

    /// <summary>
    /// Test: Patient auto-approval should have null ApprovedBy (no admin approver).
    /// </summary>
    [Fact]
    public async Task Register_PatientAutoApproval_HasNullApprovedBy()
    {
        // Arrange
        var request = new RegisterRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "patient@test.com",
            Password = "Patient123!",
            ConfirmPassword = "Patient123!",
            UserType = "patient"
        };

        // Act
        await _controller.Register(request);

        // Assert
        var user = await _userManager.FindByEmailAsync(request.Email);
        Assert.NotNull(user);
        Assert.NotNull(user.ApprovedAt); // Auto-approved
        Assert.Null(user.ApprovedBy); // No admin approver for auto-approval
    }

    /// <summary>
    /// Test: Admin accounts should NOT be auto-approved on registration.
    /// </summary>
    [Fact]
    public async Task Register_DoesNotAutoApproveAdminAccounts()
    {
        // Arrange
        var request = new RegisterRequest
        {
            FirstName = "Admin",
            LastName = "User",
            Email = "admin@test.com",
            Password = "Admin123!",
            ConfirmPassword = "Admin123!",
            UserType = "admin"
        };

        // Act
        await _controller.Register(request);

        // Assert
        var user = await _userManager.FindByEmailAsync(request.Email);
        Assert.NotNull(user);
        Assert.Null(user.ApprovedAt); // NOT auto-approved
        Assert.Null(user.ApprovedBy);
    }

    /// <summary>
    /// Test: Clinician accounts should NOT be auto-approved on registration.
    /// </summary>
    [Fact]
    public async Task Register_DoesNotAutoApproveClinicianAccounts()
    {
        // Arrange
        var request = new RegisterRequest
        {
            FirstName = "Clinician",
            LastName = "User",
            Email = "clinician@test.com",
            Password = "Clinician123!",
            ConfirmPassword = "Clinician123!",
            UserType = "clinician"
        };

        // Act
        await _controller.Register(request);

        // Assert
        var user = await _userManager.FindByEmailAsync(request.Email);
        Assert.NotNull(user);
        Assert.Null(user.ApprovedAt); // NOT auto-approved
        Assert.Null(user.ApprovedBy);
    }

    /// <summary>
    /// Test: Registration flow - all three user types have correct approval behavior.
    /// </summary>
    [Theory]
    [InlineData("patient", true)]   // Patients are auto-approved
    [InlineData("clinician", false)] // Clinicians require manual approval
    [InlineData("admin", false)]     // Admins require manual approval
    public async Task Register_SetsCorrectApprovalStatus_ForAllUserTypes(string userType, bool shouldBeAutoApproved)
    {
        // Arrange
        var request = new RegisterRequest
        {
            FirstName = "Test",
            LastName = "User",
            Email = $"{userType}@test.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            UserType = userType
        };

        // Act
        await _controller.Register(request);

        // Assert
        var user = await _userManager.FindByEmailAsync(request.Email);
        Assert.NotNull(user);

        if (shouldBeAutoApproved)
        {
            Assert.NotNull(user.ApprovedAt);
            Assert.Null(user.ApprovedBy);
        }
        else
        {
            Assert.Null(user.ApprovedAt);
            Assert.Null(user.ApprovedBy);
        }
    }

    /// <summary>
    /// Test: Multiple patients can be auto-approved independently.
    /// </summary>
    [Fact]
    public async Task Register_AutoApprovesMultiplePatients()
    {
        // Arrange & Act: Register three patient accounts
        for (int i = 1; i <= 3; i++)
        {
            var request = new RegisterRequest
            {
                FirstName = $"Patient{i}",
                LastName = "Test",
                Email = $"patient{i}@test.com",
                Password = "Password123!",
                ConfirmPassword = "Password123!",
                UserType = "patient"
            };
            await _controller.Register(request);
        }

        // Assert: All three patients should be auto-approved
        for (int i = 1; i <= 3; i++)
        {
            var user = await _userManager.FindByEmailAsync($"patient{i}@test.com");
            Assert.NotNull(user);
            Assert.NotNull(user.ApprovedAt);
            Assert.Null(user.ApprovedBy);
        }
    }

    /// <summary>
    /// Test: Unapproved users would be blocked from login based on ApprovedAt field.
    /// </summary>
    /// <remarks>
    /// This test verifies the database state that the login check relies on.
    /// The actual login blocking is tested manually because SignInManager is complex to mock.
    /// </remarks>
    [Fact]
    public async Task UnapprovedUsers_HaveNullApprovedAt_WouldBeBlockedFromLogin()
    {
        // Arrange: Create admin and clinician accounts via registration
        var adminRequest = new RegisterRequest
        {
            FirstName = "Admin",
            LastName = "Test",
            Email = "admin@test.com",
            Password = "Admin123!",
            ConfirmPassword = "Admin123!",
            UserType = "admin"
        };

        var clinicianRequest = new RegisterRequest
        {
            FirstName = "Clinician",
            LastName = "Test",
            Email = "clinician@test.com",
            Password = "Clinician123!",
            ConfirmPassword = "Clinician123!",
            UserType = "clinician"
        };

        // Act
        await _controller.Register(adminRequest);
        await _controller.Register(clinicianRequest);

        // Assert: Both should have null ApprovedAt
        var admin = await _userManager.FindByEmailAsync("admin@test.com");
        var clinician = await _userManager.FindByEmailAsync("clinician@test.com");

        Assert.NotNull(admin);
        Assert.NotNull(clinician);
        Assert.Null(admin.ApprovedAt); // Would be blocked by login check
        Assert.Null(clinician.ApprovedAt); // Would be blocked by login check
    }

    /// <summary>
    /// Test: System account (if created directly) should be approved and have correct fields.
    /// </summary>
    /// <remarks>
    /// This test verifies that a System account created like the DatabaseSeeder does
    /// has the correct approval status.
    /// </remarks>
    [Fact]
    public async Task SystemAccount_WhenCreatedDirectly_HasCorrectApprovalFields()
    {
        // Arrange & Act: Create System account (simulating DatabaseSeeder)
        var systemUser = new ApplicationUser
        {
            UserName = "system@graphenetrace.local",
            Email = "system@graphenetrace.local",
            EmailConfirmed = true,
            FirstName = "System",
            LastName = "Administrator",
            UserType = "admin",
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow, // System account is auto-approved
            ApprovedBy = null // System approves itself (no approver)
        };
        await _userManager.CreateAsync(systemUser, "System@Admin123");

        // Assert
        var user = await _userManager.FindByEmailAsync("system@graphenetrace.local");
        Assert.NotNull(user);
        Assert.NotNull(user.ApprovedAt); // System account is approved
        Assert.Null(user.ApprovedBy); // No approver (system self-approved)
        Assert.Equal("admin", user.UserType);
    }

    /// <summary>
    /// Test: Registration preserves other user properties when setting approval fields.
    /// </summary>
    [Fact]
    public async Task Register_PreservesUserProperties_WhenSettingApprovalFields()
    {
        // Arrange
        var request = new RegisterRequest
        {
            FirstName = "John",
            LastName = "Smith",
            Email = "john.smith@test.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            UserType = "patient"
        };

        // Act
        await _controller.Register(request);

        // Assert
        var user = await _userManager.FindByEmailAsync(request.Email);
        Assert.NotNull(user);
        Assert.Equal("John", user.FirstName);
        Assert.Equal("Smith", user.LastName);
        Assert.Equal("john.smith@test.com", user.Email);
        Assert.Equal("patient", user.UserType);
        Assert.True(user.EmailConfirmed);
        Assert.Null(user.DeactivatedAt);
        Assert.NotNull(user.ApprovedAt); // Patient auto-approval
    }
}
