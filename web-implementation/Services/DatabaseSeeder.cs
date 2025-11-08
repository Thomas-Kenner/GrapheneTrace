using GrapheneTrace.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace GrapheneTrace.Web.Services;

/// <summary>
/// Service for seeding essential database records on application startup.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Purpose: Ensures critical system accounts exist and have correct credentials.
///
/// System Account:
/// - Username: system@graphenetrace.local
/// - Password: System@Admin123 (reset on every startup for development consistency)
/// - UserType: admin
/// - Auto-approved: ApprovedAt set to account creation time
/// - Used for: Initial account approvals, system-generated actions, development testing
///
/// Design Pattern: Idempotent seeding
/// - Safe to run multiple times without side effects
/// - Creates System account if missing
/// - Resets System password to 1234 if account exists (ensures known credentials)
/// - Logs all operations for audit trail
///
/// Security Note:
/// This seeder should be DISABLED in production or the System password should be
/// changed immediately after deployment. The hardcoded password is for development
/// convenience only.
/// </remarks>
public class DatabaseSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DatabaseSeeder> _logger;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// System admin account credentials and configuration.
    /// </summary>
    private const string SystemEmail = "system@graphenetrace.local";
    private const string SystemPassword = "System@Admin123";
    private const string SystemFirstName = "System";
    private const string SystemLastName = "Administrator";
    private const string SystemUserType = "admin";

    public DatabaseSeeder(
        UserManager<ApplicationUser> userManager,
        ILogger<DatabaseSeeder> logger,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Seeds the database with essential system accounts.
    /// </summary>
    /// <remarks>
    /// Execution Flow:
    /// 1. Check if seeding is enabled in configuration
    /// 2. Check if System account exists in database
    /// 3. If missing: Create new System account with default credentials
    /// 4. If exists: Reset password to ensure known credentials
    /// 5. Log all operations for audit trail
    ///
    /// Idempotent Design:
    /// - Safe to call on every application startup
    /// - Won't create duplicate accounts (email is unique)
    /// - Password reset ensures consistent state across restarts
    /// </remarks>
    public async Task SeedAsync()
    {
        try
        {
            // Check if seeding is enabled in configuration
            var seedingEnabled = _configuration.GetValue<bool>("DatabaseSeeding:Enabled", true);
            if (!seedingEnabled)
            {
                _logger.LogInformation("Database seeding is disabled in configuration");
                return;
            }

            _logger.LogInformation("Starting database seeding...");

            await EnsureSystemAccountExistsAsync();

            _logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during database seeding");
            throw; // Re-throw to prevent application startup if seeding fails
        }
    }

    /// <summary>
    /// Ensures the System admin account exists with correct credentials.
    /// </summary>
    /// <remarks>
    /// Implementation Strategy:
    /// - Searches for existing account by email (unique constraint)
    /// - Creates new account if missing
    /// - Resets password if account exists (ensures known credentials)
    /// - Sets account as pre-approved (ApprovedAt = CreatedAt)
    ///
    /// Password Hashing:
    /// UserManager automatically hashes passwords using ASP.NET Core Identity's
    /// PBKDF2 algorithm (same as runtime user registration). We don't need to
    /// manually hash with BCrypt - Identity handles this internally.
    ///
    /// Why Reset Password Every Time:
    /// - Development convenience: Always know System credentials
    /// - Testing consistency: Fresh environment on every restart
    /// - Password recovery: If System password changed, restart resets it
    /// </remarks>
    private async Task EnsureSystemAccountExistsAsync()
    {
        var systemUser = await _userManager.FindByEmailAsync(SystemEmail);

        if (systemUser == null)
        {
            // Create new System account
            _logger.LogInformation("System account not found, creating new account...");

            systemUser = new ApplicationUser
            {
                UserName = SystemEmail,
                Email = SystemEmail,
                EmailConfirmed = true,
                FirstName = SystemFirstName,
                LastName = SystemLastName,
                UserType = SystemUserType,
                CreatedAt = DateTime.UtcNow,
                ApprovedAt = DateTime.UtcNow, // Auto-approve system account
                ApprovedBy = null // System approves itself (no approver)
            };

            var createResult = await _userManager.CreateAsync(systemUser, SystemPassword);

            if (createResult.Succeeded)
            {
                _logger.LogInformation(
                    "System account created successfully: {Email} (ID: {UserId})",
                    SystemEmail, systemUser.Id);
            }
            else
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create System account: {Errors}", errors);
                throw new InvalidOperationException($"Failed to create System account: {errors}");
            }
        }
        else
        {
            // System account exists - reset password to ensure known credentials
            _logger.LogInformation(
                "System account found (ID: {UserId}), resetting password to default...",
                systemUser.Id);

            // Generate password reset token and reset password
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(systemUser);
            var resetResult = await _userManager.ResetPasswordAsync(systemUser, resetToken, SystemPassword);

            if (resetResult.Succeeded)
            {
                _logger.LogInformation("System account password reset successfully");
            }
            else
            {
                var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
                _logger.LogWarning("Failed to reset System account password: {Errors}", errors);
                // Don't throw - existing account with different password is acceptable
            }
        }
    }
}
