using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
    private readonly ApplicationDbContext _context;

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
        IConfiguration configuration,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _logger = logger;
        _configuration = configuration;
        _context = context;
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
            var patient = await EnsureTestPatientExistsAsync(buildTime);
            var clinician = await EnsureApprovedClinicianExistsAsync(systemUser, buildTime);
            await EnsureUnapprovedClinicianExistsAsync(buildTime);
            await SeedPressureDataAsync();

            await EnsureAssignmentsAndMessagesAsync(patient, clinician);

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
    private async Task<ApplicationUser> EnsureTestPatientExistsAsync(DateTime buildTime)
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

        return testPatient;
    }

    /// <summary>
    /// Ensures an approved clinician test account exists.
    /// </summary>
    /// <param name="systemUser">The system admin who approved the account</param>
    /// <param name="buildTime">The build time to use for approval timestamps</param>
    private async Task<ApplicationUser> EnsureApprovedClinicianExistsAsync(ApplicationUser systemUser, DateTime buildTime)
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

        return approvedClinician;
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


    private async Task EnsureAssignmentsAndMessagesAsync(ApplicationUser patient, ApplicationUser clinician)
    {
        // Check if assignment exists
        var assignment = await _context.PatientClinicianAssignments
            .FirstOrDefaultAsync(a => a.PatientId == patient.Id && a.ClinicianId == clinician.Id);

        if (assignment == null)
        {
            _logger.LogInformation("Creating assignment between {Patient} and {Clinician}", patient.Email, clinician.Email);
            assignment = new PatientClinicianAssignment
            {
                PatientId = patient.Id,
                ClinicianId = clinician.Id,
                AssignedAt = DateTime.UtcNow
            };
            _context.PatientClinicianAssignments.Add(assignment);
            await _context.SaveChangesAsync();
        }

        // Check if messages exist
        var messagesExist = await _context.ChatMessages
            .AnyAsync(m => (m.SenderId == patient.Id && m.ReceiverId == clinician.Id) ||
                           (m.SenderId == clinician.Id && m.ReceiverId == patient.Id));

        if (!messagesExist)
        {
            _logger.LogInformation("Seeding sample chat messages");
            var now = DateTime.UtcNow;

            var messages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    SenderId = clinician.Id,
                    ReceiverId = patient.Id,
                    Content = "Hello! How are you feeling today?",
                    SentAt = now.AddMinutes(-30),
                    IsRead = true
                },
                new ChatMessage
                {
                    SenderId = patient.Id,
                    ReceiverId = clinician.Id,
                    Content = "I'm feeling a bit better, thanks.",
                    SentAt = now.AddMinutes(-25),
                    IsRead = true
                },
                new ChatMessage
                {
                    SenderId = clinician.Id,
                    ReceiverId = patient.Id,
                    Content = "That's good to hear. Have you been monitoring your pressure?",
                    SentAt = now.AddMinutes(-20),
                    IsRead = false
                }
            };

            _context.ChatMessages.AddRange(messages);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Seeds pressure data from CSV files in the resources directory.
    /// </summary>
    // Author: SID:2412494
    // Moved seeding logic here to run on startup and support resources directory.
    private async Task SeedPressureDataAsync()
    {
        try
        {
            // Path to resources directory relative to the web app execution path
            // Assuming web app runs from web-implementation/ or bin/Debug/net8.0/
            // We need to find the repo root.
            // Author: SID:2412494
            // Updated to use capitalized Resources directory.
            string resourcePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Resources", "GTLB-Data");
            
            // If running from bin/Debug/..., we might need to go up more levels. 
            // A safer bet for dev environment is to look for the resources folder.
            if (!Directory.Exists(resourcePath))
            {
                 // Try finding it relative to project root if running in dev
                 resourcePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "GTLB-Data");
                 if (!Directory.Exists(resourcePath))
                 {
                     // Fallback for when running inside web-implementation folder
                     resourcePath = Path.Combine("..", "Resources", "GTLB-Data");
                 }
            }

            if (!Directory.Exists(resourcePath))
            {
                _logger.LogWarning("Pressure data resources directory not found at: {Path}", resourcePath);
                return;
            }

            string[] files = Directory.GetFiles(resourcePath, "*.csv");
            _logger.LogInformation("Found {Count} pressure data files to process.", files.Length);

            foreach (string fileName in files)
            {
                // Split file path into separate bits of info
                // Expect csv files with paths in the format deviceId_date.csv
                string simpleFileName = Path.GetFileName(fileName);
                char[] delimiterChar = ['_', '.'];
                string[] fileNameSegments = simpleFileName.Split(delimiterChar);

                // Expect deviceId_date.csv, so 3 segments (deviceId, date, csv)
                if (fileNameSegments.Length != 3) 
                {
                    _logger.LogWarning("Skipping file with invalid format: {FileName}", simpleFileName);
                    continue;
                }

                string deviceId = fileNameSegments[0];
                string dateString = fileNameSegments[1];

                if (!DateTime.TryParseExact(dateString, "yyyyMMdd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out DateTime date)) 
                {
                    _logger.LogWarning("Skipping file with invalid date format: {FileName}", simpleFileName);
                    continue;
                }
                
                var parsedDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

                // Check if session already exists
                var existingSession = await _context.PatientSessionDatas
                    .FirstOrDefaultAsync(s => s.DeviceId == deviceId && s.Start == parsedDate);

                if (existingSession != null)
                {
                    _logger.LogDebug("Session already exists for {DeviceId} on {Date}. Skipping.", deviceId, parsedDate.ToShortDateString());
                    continue;
                }

                _logger.LogInformation("Seeding pressure data for {DeviceId} on {Date}...", deviceId, parsedDate.ToShortDateString());

                string fileContents = await File.ReadAllTextAsync(fileName);

                // Create Session
                var sessionData = new PatientSessionData
                {
                    DeviceId = deviceId,
                    Start = parsedDate,
                };

                _context.PatientSessionDatas.Add(sessionData);
                await _context.SaveChangesAsync();

                // Process Snapshots using helper methods from PressureDataService
                // Note: We need to make sure PressureDataService helpers are accessible or duplicate them.
                // The plan said to keep them public in PressureDataService.
                
                List<string> sessionSnapshots = PressureDataService.SplitIntoSnapshots(fileContents, 32); // 32 rows
                
                // 15 snapshots per second
                double millisec = 1000.0 / 15.0;
                TimeSpan interval = TimeSpan.FromMilliseconds(millisec);
                var snapshotTime = parsedDate;

                var snapshotsToAdd = new List<PatientSnapshotData>(sessionSnapshots.Count);

                foreach (string snapshot in sessionSnapshots)
                {
                    var snapshotInts = PressureDataService.ConvertSnapshotToInt(snapshot, 32); // 32 columns

                    var snapshotData = new PatientSnapshotData
                    {
                        SessionId = sessionData.SessionId,
                        SnapshotData = snapshot,
                        ContactAreaPercent = PressureDataService.SensorsOverLimitInSession(snapshotInts, 0) * (100.0f / 1024.0f),
                        SnapshotTime = snapshotTime,
                    };

                    snapshotsToAdd.Add(snapshotData);
                    snapshotTime = snapshotTime.Add(interval);
                }

                _context.PatientSnapshotDatas.AddRange(snapshotsToAdd);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding pressure data.");
            // Don't throw, just log error so other seeding can continue or app can start
        }
    }
}
