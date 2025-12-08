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
    /// Author: SID:2412494
    /// Updated: 2402513 - Fixed error handling to use redirects instead of JSON responses
    /// </summary>
    /// <remarks>
    /// Error Handling Fix (Updated: 2402513):
    /// Changed all error responses from JSON (Unauthorized/StatusCode) to HTTP redirects
    /// with query parameters. This fixes the login flow after the login page route was
    /// changed from /login to /. The Login.razor component expects redirects with error
    /// messages in the query string (e.g., /?error=message) to display to users.
    ///
    /// Why Redirect Instead of JSON:
    /// - Login form uses traditional HTML POST which expects HTTP redirects
    /// - Browser handles redirects and preserves query parameters
    /// - Login component reads error from URL query parameters
    /// - Provides better UX with proper error display on the login page
    /// </remarks>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] LoginRequest request)
    {
        try
        {
            // Validate input
            // Updated: 2402513 - Changed from BadRequest JSON to redirect with error query parameter
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Redirect("/?error=" + Uri.EscapeDataString("Please enter both email and password"));
            }

            // Find user
            // Updated: 2402513 - Changed from Unauthorized JSON to redirect with error query parameter
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                _logger.LogWarning("Login attempt for non-existent user: {Email}", request.Email);
                return Redirect("/?error=" + Uri.EscapeDataString("Invalid email or password"));
            }

            // Check if deactivated
            // Updated: 2402513 - Changed from Unauthorized JSON to redirect with error query parameter
            if (user.DeactivatedAt != null)
            {
                _logger.LogWarning("Login attempt for deactivated user: {UserId}", user.Id);
                return Redirect("/?error=" + Uri.EscapeDataString("This account has been deactivated. Please contact support."));
            }

            // TODO: Add approval check (ApprovedAt) - block unapproved admin/clinician accounts
            // Updated: 2402513 - Added TODO for approval check implementation
            // Only enforce approval for non-patient accounts (patients are auto-approved on registration)
            // if (user.ApprovedAt == null && (user.UserType == "admin" || user.UserType == "clinician"))
            // {
            //     _logger.LogWarning("Login attempt for unapproved user: {UserId} (Type: {UserType})", user.Id, user.UserType);
            //     return Redirect("/?error=" + Uri.EscapeDataString("Your account is pending administrator approval. You will receive notification when approved."));
            // }

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

                // TODO: Add role assignment - ensure user has Identity role matching their UserType
                // Updated: 2402513 - Added TODO for role assignment implementation
                // This is needed because pages use [Authorize(Roles = "Clinician")] but roles aren't assigned
                // Pages require Identity roles (Admin, Clinician, Patient) but users only have UserType property
                // This TODO should assign the appropriate Identity role based on UserType to enable authorization
                // Example:
                // var roleName = user.UserType switch { "admin" => "Admin", "clinician" => "Clinician", "patient" => "Patient", _ => null };
                // if (!string.IsNullOrEmpty(roleName) && !await _userManager.IsInRoleAsync(user, roleName))
                // {
                //     await _userManager.AddToRoleAsync(user, roleName);
                // }

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

            // Updated: 2402513 - Changed all error responses from JSON to redirects with error query parameters
            // This ensures errors are displayed on the login page (/ route) instead of as JSON responses
            
            if (result.IsLockedOut)
            {
                _logger.LogWarning("User {UserId} account locked out", user.Id);
                return Redirect("/?error=" + Uri.EscapeDataString("Account locked due to multiple failed login attempts. Please try again in 15 minutes."));
            }

            if (result.IsNotAllowed)
            {
                _logger.LogWarning("User {UserId} login not allowed", user.Id);
                return Redirect("/?error=" + Uri.EscapeDataString("Login not allowed. Please confirm your email address."));
            }

            _logger.LogWarning("Failed login attempt for user: {Email}", request.Email);
            return Redirect("/?error=" + Uri.EscapeDataString("Invalid email or password"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
            // Updated: 2402513 - Changed from StatusCode(500) JSON to redirect with error query parameter
            return Redirect("/?error=" + Uri.EscapeDataString("An error occurred during login. Please try again."));
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
                EmailConfirmed = true,  // Skip email confirmation for now
                // Auto-approve patient accounts - only admin/clinician accounts require manual approval
                ApprovedAt = request.UserType == "patient" ? DateTime.UtcNow : null
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

                // Traditional form POST - handle sign-in based on approval status
                if (user.ApprovedAt == null)
                {
                    // Admin/Clinician accounts require approval - redirect to login with success message
                    _logger.LogInformation("Account created but requires approval: {UserId}", user.Id);
                    var message = "Account created successfully! Your account is pending administrator approval. You will be notified when you can log in.";
                    return Redirect("/login?success=" + Uri.EscapeDataString(message));
                }

                // Patient accounts are auto-approved - sign in immediately
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
    /// Author: SID:2412494
    /// Updated: 2402513 - Changed redirect URL from /login to / to match updated login page route
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out successfully");
            // Updated: 2402513 - Changed redirect URL from /login to / to match updated login page route
            return Ok(new { redirectUrl = "/" });
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
