using GrapheneTrace.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace GrapheneTrace.Web.Services;

/// <summary>
/// Service for seeding essential database records on application startup.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Purpose: Ensures critical system accounts and test accounts exist with correct credentials.
///
/// System Account:
/// - Username: system@graphenetrace.local
/// - Password: System@Admin123
/// - UserType: admin
/// - Auto-approved: ApprovedAt set to build time
/// - Used for: Initial account approvals, system-generated actions, development testing
///
/// Test Patient Account:
/// - Username: patient.test@graphenetrace.local
/// - Password: Patient@Test123
/// - UserType: patient
/// - Auto-approved: ApprovedAt set to build time (patients are auto-approved)
///
/// Approved Clinician Account:
/// - Username: clinician.approved@graphenetrace.local
/// - Password: Clinician@Approved123
/// - UserType: clinician
/// - Approved by: System admin at build time
///
/// Unapproved Clinician Account:
/// - Username: clinician.pending@graphenetrace.local
/// - Password: Clinician@Pending123
/// - UserType: clinician
/// - Approval status: Not approved (ApprovedAt = null)
///
/// Design Pattern: Idempotent seeding
/// - Safe to run multiple times without side effects
/// - Creates accounts if missing
/// - Resets passwords and approval states on every startup (ensures known state)
/// - All timestamps use build time for consistency
/// - Logs all operations for audit trail
///
/// Security Note:
/// This seeder should be DISABLED in production or all passwords should be
/// changed immediately after deployment. The hardcoded passwords are for development
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

    /// <summary>
    /// Test patient account credentials (auto-approved).
    /// </summary>
    private const string TestPatientEmail = "patient.test@graphenetrace.local";
    private const string TestPatientPassword = "Patient@Test123";
    private const string TestPatientFirstName = "Test";
    private const string TestPatientLastName = "Patient";
    private const string TestPatientUserType = "patient";

    /// <summary>
    /// Test approved clinician account credentials.
    /// </summary>
    private const string ApprovedClinicianEmail = "clinician.approved@graphenetrace.local";
    private const string ApprovedClinicianPassword = "Clinician@Approved123";
    private const string ApprovedClinicianFirstName = "Approved";
    private const string ApprovedClinicianLastName = "Clinician";
    private const string ApprovedClinicianUserType = "clinician";

    /// <summary>
    /// Test unapproved clinician account credentials.
    /// </summary>
    private const string UnapprovedClinicianEmail = "clinician.pending@graphenetrace.local";
    private const string UnapprovedClinicianPassword = "Clinician@Pending123";
    private const string UnapprovedClinicianFirstName = "Pending";
    private const string UnapprovedClinicianLastName = "Clinician";
    private const string UnapprovedClinicianUserType = "clinician";

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

            var buildTime = DateTime.UtcNow;
            var systemUser = await EnsureSystemAccountExistsAsync();
            await EnsureTestPatientExistsAsync(buildTime);
            await EnsureApprovedClinicianExistsAsync(systemUser, buildTime);
            await EnsureUnapprovedClinicianExistsAsync(buildTime);

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
    /// <returns>The system user account</returns>
    private async Task<ApplicationUser> EnsureSystemAccountExistsAsync()
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

        return systemUser;
    }

    /// <summary>
    /// Ensures a test patient account exists (auto-approved).
    /// </summary>
    /// <param name="buildTime">The build time to use for approval timestamps</param>
    private async Task EnsureTestPatientExistsAsync(DateTime buildTime)
    {
        var testPatient = await _userManager.FindByEmailAsync(TestPatientEmail);

        if (testPatient == null)
        {
            _logger.LogInformation("Test patient account not found, creating new account...");

            testPatient = new ApplicationUser
            {
                UserName = TestPatientEmail,
                Email = TestPatientEmail,
                EmailConfirmed = true,
                FirstName = TestPatientFirstName,
                LastName = TestPatientLastName,
                UserType = TestPatientUserType,
                CreatedAt = buildTime,
                ApprovedAt = buildTime, // Auto-approve patient account
                ApprovedBy = null // Patients are auto-approved
            };

            var createResult = await _userManager.CreateAsync(testPatient, TestPatientPassword);

            if (createResult.Succeeded)
            {
                _logger.LogInformation(
                    "Test patient account created successfully: {Email} (ID: {UserId})",
                    TestPatientEmail, testPatient.Id);
            }
            else
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create test patient account: {Errors}", errors);
                throw new InvalidOperationException($"Failed to create test patient account: {errors}");
            }
        }
        else
        {
            _logger.LogInformation(
                "Test patient account found (ID: {UserId}), resetting password and approval status...",
                testPatient.Id);

            // Reset password
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(testPatient);
            var resetResult = await _userManager.ResetPasswordAsync(testPatient, resetToken, TestPatientPassword);

            if (!resetResult.Succeeded)
            {
                var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
                _logger.LogWarning("Failed to reset test patient password: {Errors}", errors);
            }

            // Reset approval status
            testPatient.ApprovedAt = buildTime;
            testPatient.ApprovedBy = null;
            await _userManager.UpdateAsync(testPatient);
        }
    }

    /// <summary>
    /// Ensures an approved clinician test account exists.
    /// </summary>
    /// <param name="systemUser">The system admin who approved the account</param>
    /// <param name="buildTime">The build time to use for approval timestamps</param>
    private async Task EnsureApprovedClinicianExistsAsync(ApplicationUser systemUser, DateTime buildTime)
    {
        var approvedClinician = await _userManager.FindByEmailAsync(ApprovedClinicianEmail);

        if (approvedClinician == null)
        {
            _logger.LogInformation("Approved clinician account not found, creating new account...");

            approvedClinician = new ApplicationUser
            {
                UserName = ApprovedClinicianEmail,
                Email = ApprovedClinicianEmail,
                EmailConfirmed = true,
                FirstName = ApprovedClinicianFirstName,
                LastName = ApprovedClinicianLastName,
                UserType = ApprovedClinicianUserType,
                CreatedAt = buildTime,
                ApprovedAt = buildTime, // Approved by system admin
                ApprovedBy = systemUser.Id
            };

            var createResult = await _userManager.CreateAsync(approvedClinician, ApprovedClinicianPassword);

            if (createResult.Succeeded)
            {
                _logger.LogInformation(
                    "Approved clinician account created successfully: {Email} (ID: {UserId})",
                    ApprovedClinicianEmail, approvedClinician.Id);
            }
            else
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create approved clinician account: {Errors}", errors);
                throw new InvalidOperationException($"Failed to create approved clinician account: {errors}");
            }
        }
        else
        {
            _logger.LogInformation(
                "Approved clinician account found (ID: {UserId}), resetting password and approval status...",
                approvedClinician.Id);

            // Reset password
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(approvedClinician);
            var resetResult = await _userManager.ResetPasswordAsync(approvedClinician, resetToken, ApprovedClinicianPassword);

            if (!resetResult.Succeeded)
            {
                var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
                _logger.LogWarning("Failed to reset approved clinician password: {Errors}", errors);
            }

            // Reset approval status
            approvedClinician.ApprovedAt = buildTime;
            approvedClinician.ApprovedBy = systemUser.Id;
            await _userManager.UpdateAsync(approvedClinician);
        }
    }

    /// <summary>
    /// Ensures an unapproved clinician test account exists.
    /// </summary>
    /// <param name="buildTime">The build time to use for creation timestamp</param>
    private async Task EnsureUnapprovedClinicianExistsAsync(DateTime buildTime)
    {
        var unapprovedClinician = await _userManager.FindByEmailAsync(UnapprovedClinicianEmail);

        if (unapprovedClinician == null)
        {
            _logger.LogInformation("Unapproved clinician account not found, creating new account...");

            unapprovedClinician = new ApplicationUser
            {
                UserName = UnapprovedClinicianEmail,
                Email = UnapprovedClinicianEmail,
                EmailConfirmed = true,
                FirstName = UnapprovedClinicianFirstName,
                LastName = UnapprovedClinicianLastName,
                UserType = UnapprovedClinicianUserType,
                CreatedAt = buildTime,
                ApprovedAt = null, // Not approved
                ApprovedBy = null
            };

            var createResult = await _userManager.CreateAsync(unapprovedClinician, UnapprovedClinicianPassword);

            if (createResult.Succeeded)
            {
                _logger.LogInformation(
                    "Unapproved clinician account created successfully: {Email} (ID: {UserId})",
                    UnapprovedClinicianEmail, unapprovedClinician.Id);
            }
            else
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create unapproved clinician account: {Errors}", errors);
                throw new InvalidOperationException($"Failed to create unapproved clinician account: {errors}");
            }
        }
        else
        {
            _logger.LogInformation(
                "Unapproved clinician account found (ID: {UserId}), resetting password and approval status...",
                unapprovedClinician.Id);

            // Reset password
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(unapprovedClinician);
            var resetResult = await _userManager.ResetPasswordAsync(unapprovedClinician, resetToken, UnapprovedClinicianPassword);

            if (!resetResult.Succeeded)
            {
                var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
                _logger.LogWarning("Failed to reset unapproved clinician password: {Errors}", errors);
            }

            // Reset approval status (ensure it remains unapproved)
            unapprovedClinician.ApprovedAt = null;
            unapprovedClinician.ApprovedBy = null;
            await _userManager.UpdateAsync(unapprovedClinician);
        }
    }
}
