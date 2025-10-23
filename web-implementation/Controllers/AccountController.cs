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

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
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
                return BadRequest(new { error = "Please enter both email and password" });
            }

            // Find user
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                _logger.LogWarning("Login attempt for non-existent user: {Email}", request.Email);
                return Unauthorized(new { error = "Invalid email or password" });
            }

            // Check if deactivated
            if (user.DeactivatedAt != null)
            {
                _logger.LogWarning("Login attempt for deactivated user: {UserId}", user.Id);
                return Unauthorized(new { error = "This account has been deactivated. Please contact support." });
            }

            // Attempt sign in
            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                request.Password,
                isPersistent: request.RememberMe,
                lockoutOnFailure: true
            );

            if (result.Succeeded)
            {
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

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User {UserId} account locked out", user.Id);
                return Unauthorized(new { error = "Account locked due to multiple failed login attempts. Please try again in 15 minutes." });
            }

            if (result.IsNotAllowed)
            {
                _logger.LogWarning("User {UserId} login not allowed", user.Id);
                return Unauthorized(new { error = "Login not allowed. Please confirm your email address." });
            }

            _logger.LogWarning("Failed login attempt for user: {Email}", request.Email);
            return Unauthorized(new { error = "Invalid email or password" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
            return StatusCode(500, new { error = "An error occurred during login. Please try again." });
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

                // Sign in the user
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
            return Ok(new { redirectUrl = "/login" });
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
