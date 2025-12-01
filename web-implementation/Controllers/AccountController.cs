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

<<<<<<< HEAD
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole<Guid>> roleManager,
=======
    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
>>>>>>> 87be01e ( Implemented ASPNET Core Identity based login. Using HTML form POST for login page, account creation page needs to be converted from HttpClient. Still needs more thorough testing)
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
<<<<<<< HEAD
        _roleManager = roleManager;
=======
>>>>>>> 87be01e ( Implemented ASPNET Core Identity based login. Using HTML form POST for login page, account creation page needs to be converted from HttpClient. Still needs more thorough testing)
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
<<<<<<< HEAD
                return Redirect("/login?error=" + Uri.EscapeDataString("Please enter both email and password"));
=======
                return BadRequest(new { error = "Please enter both email and password" });
>>>>>>> 87be01e ( Implemented ASPNET Core Identity based login. Using HTML form POST for login page, account creation page needs to be converted from HttpClient. Still needs more thorough testing)
            }

            // Find user
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                _logger.LogWarning("Login attempt for non-existent user: {Email}", request.Email);
<<<<<<< HEAD
                return Redirect("/login?error=" + Uri.EscapeDataString("Invalid email or password"));
=======
                return Unauthorized(new { error = "Invalid email or password" });
>>>>>>> 87be01e ( Implemented ASPNET Core Identity based login. Using HTML form POST for login page, account creation page needs to be converted from HttpClient. Still needs more thorough testing)
            }

            // Check if deactivated
            if (user.DeactivatedAt != null)
            {
                _logger.LogWarning("Login attempt for deactivated user: {UserId}", user.Id);
<<<<<<< HEAD
                return Redirect("/login?error=" + Uri.EscapeDataString("This account has been deactivated. Please contact support."));
            }

            // Check if account is approved (only applies to admin/clinician accounts)
            if (user.ApprovedAt == null)
            {
                _logger.LogWarning("Login attempt for unapproved user: {UserId}", user.Id);
                return Redirect("/login?error=" + Uri.EscapeDataString("Your account is pending approval. Please contact an administrator."));
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
=======
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
>>>>>>> 87be01e ( Implemented ASPNET Core Identity based login. Using HTML form POST for login page, account creation page needs to be converted from HttpClient. Still needs more thorough testing)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
<<<<<<< HEAD
            return Redirect("/login?error=" + Uri.EscapeDataString("An error occurred during login. Please try again."));
=======
            return StatusCode(500, new { error = "An error occurred during login. Please try again." });
>>>>>>> 87be01e ( Implemented ASPNET Core Identity based login. Using HTML form POST for login page, account creation page needs to be converted from HttpClient. Still needs more thorough testing)
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
<<<<<<< HEAD
                EmailConfirmed = true,  // Skip email confirmation for now
                // Auto-approve patient accounts - only admin/clinician accounts require manual approval
                ApprovedAt = request.UserType == "patient" ? DateTime.UtcNow : null
=======
                EmailConfirmed = true  // Skip email confirmation for now
>>>>>>> 87be01e ( Implemented ASPNET Core Identity based login. Using HTML form POST for login page, account creation page needs to be converted from HttpClient. Still needs more thorough testing)
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {UserId} created successfully", user.Id);

<<<<<<< HEAD
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
=======
                // Sign in the user
>>>>>>> 87be01e ( Implemented ASPNET Core Identity based login. Using HTML form POST for login page, account creation page needs to be converted from HttpClient. Still needs more thorough testing)
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
<<<<<<< HEAD
            // Redirect via HTTP 302 so browser clears authentication cookie
            return Redirect("/login");
=======
            return Ok(new { redirectUrl = "/login" });
>>>>>>> 87be01e ( Implemented ASPNET Core Identity based login. Using HTML form POST for login page, account creation page needs to be converted from HttpClient. Still needs more thorough testing)
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
