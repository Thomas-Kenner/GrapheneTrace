using GrapheneTrace.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GrapheneTrace.Web.Controllers;

/// <summary>
/// Handles authentication via traditional HTTP POST endpoints.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Why Controller Instead of Blazor Component:
/// Blazor Server components run over SignalR WebSocket connections. When SignInManager
/// tries to set authentication cookies, the HTTP response has already started streaming,
/// causing "Headers are read-only" exceptions.
///
/// Solution: Traditional MVC controller endpoints that handle authentication via HTTP POST
/// before the response starts, allowing cookies to be set properly.
///
/// This controller provides:
/// - POST /account/login - Traditional login endpoint
/// - POST /account/register - Traditional registration endpoint
/// - POST /account/logout - Logout endpoint
///
/// Blazor components redirect to these endpoints via NavigationManager with forceLoad: true
/// to break out of the SignalR connection and use traditional HTTP.
/// </remarks>
[Route("[controller]")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AccountController> _logger;

    private readonly RoleManager<IdentityRole<Guid>> _roleManager;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    /// <summary>
    /// Handles login via traditional HTTP POST.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] LoginRequest request)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Redirect("/login?error=" + Uri.EscapeDataString("Please enter both email and password"));
            }

            // Find user
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                _logger.LogWarning("Login attempt for non-existent user: {Email}", request.Email);
                return Redirect("/login?error=" + Uri.EscapeDataString("Invalid email or password"));
            }

            // Check if deactivated
            if (user.DeactivatedAt != null)
            {
                _logger.LogWarning("Login attempt for deactivated user: {UserId}", user.Id);
                return Redirect("/login?error=" + Uri.EscapeDataString("This account has been deactivated. Please contact support."));
            }

            // Check if account is locked out
            if (await _userManager.IsLockedOutAsync(user))
            {
                _logger.LogWarning("Login attempt for locked out user: {UserId}", user.Id);
                return Redirect("/login?error=" + Uri.EscapeDataString("Account locked due to multiple failed login attempts. Please try again in 15 minutes."));
            }

            // Validate password first
            var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!passwordValid)
            {
                // Record failed attempt for lockout
                await _userManager.AccessFailedAsync(user);
                _logger.LogWarning("Failed login attempt for user: {Email}", request.Email);
                return Redirect("/login?error=" + Uri.EscapeDataString("Invalid email or password"));
            }

            // Reset access failed count on successful password validation
            await _userManager.ResetAccessFailedCountAsync(user);

            // Ensure user has a role assigned (for existing users created before role system)
            var userRoles = await _userManager.GetRolesAsync(user);
            if (!userRoles.Any())
            {
                // Assign role based on UserType
                var roleName = user.UserType switch
                {
                    "admin" => "Admin",
                    "clinician" => "Clinician",
                    "patient" => "Patient",
                    _ => "Patient"
                };

                // Ensure role exists
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    await _roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                    _logger.LogInformation("Created role: {RoleName}", roleName);
                }

                // Add user to role
                await _userManager.AddToRoleAsync(user, roleName);
                _logger.LogInformation("Assigned role {RoleName} to existing user {UserId}", roleName, user.Id);
            }

            // Sign in the user (this will include role claims in the authentication cookie)
            await _signInManager.SignInAsync(user, isPersistent: request.RememberMe);

            _logger.LogInformation("User {UserId} logged in successfully", user.Id);

            // Determine redirect path based on user type
            var redirectPath = user.UserType switch
            {
                "admin" => "/admin/dashboard",
                "clinician" => "/clinician/dashboard",
                "patient" => "/patient/dashboard",
                _ => "/patient/dashboard"
            };

            // Redirect via HTTP 302 so browser picks up authentication cookie
            return Redirect(redirectPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
            return Redirect("/login?error=" + Uri.EscapeDataString("An error occurred during login. Please try again."));
        }
    }

    /// <summary>
    /// Handles registration via traditional HTTP POST.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromForm] RegisterRequest request)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            {
                return BadRequest(new { error = "First name and last name are required" });
            }

            if (request.Password != request.ConfirmPassword)
            {
                return BadRequest(new { error = "Passwords do not match" });
            }

            // Create user
            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                UserType = request.UserType,
                EmailConfirmed = true  // Skip email confirmation for now
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {UserId} created successfully", user.Id);

                // Assign user to role based on UserType
                var roleName = user.UserType switch
                {
                    "admin" => "Admin",
                    "clinician" => "Clinician",
                    "patient" => "Patient",
                    _ => "Patient"
                };

                // Ensure role exists, create if needed
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    await _roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                    _logger.LogInformation("Created role: {RoleName}", roleName);
                }

                // Add user to role
                await _userManager.AddToRoleAsync(user, roleName);
                _logger.LogInformation("User {UserId} assigned to role {RoleName}", user.Id, roleName);

                // Check if this is a JSON request (from Blazor component via HttpClient)
                // If so, return JSON without signing in - the component will handle sign-in via form POST
                var contentType = Request.ContentType ?? "";
                if (contentType.Contains("multipart/form-data") && Request.Headers.ContainsKey("X-Requested-With"))
                {
                    // Return success JSON for Blazor component to show success overlay
                    // The component will then submit a traditional form POST to /account/login
                    return Ok(new { success = true, message = "Account created successfully" });
                }

                // Traditional form POST - sign in and redirect immediately
                await _signInManager.SignInAsync(user, isPersistent: false);

                // Determine redirect path
                var redirectPath = user.UserType switch
                {
                    "admin" => "/admin/dashboard",
                    "clinician" => "/clinician/dashboard",
                    "patient" => "/patient/dashboard",
                    _ => "/patient/dashboard"
                };

                // Redirect via HTTP 302 so browser picks up authentication cookie
                return Redirect(redirectPath);
            }

            // Return validation errors
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("User registration failed: {Errors}", errors);
            return BadRequest(new { error = errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for email: {Email}", request.Email);
            return StatusCode(500, new { error = "An error occurred while creating your account. Please try again." });
        }
    }

    /// <summary>
    /// Handles logout via traditional HTTP POST.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out successfully");
            // Redirect via HTTP 302 so browser clears authentication cookie
            return Redirect("/login");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { error = "An error occurred during logout" });
        }
    }
}

/// <summary>
/// Login request model for form binding.
/// </summary>
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}

/// <summary>
/// Registration request model for form binding.
/// </summary>
public class RegisterRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string UserType { get; set; } = "patient";
}
