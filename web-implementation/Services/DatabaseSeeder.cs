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
/// Test Patient Accounts:
/// - 5 patients (Alice, Bob, Carol, David, Emma) each mapped to a device ID
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
    /// Patient account configuration records.
    /// Each patient is mapped to a specific device ID for session assignment.
    /// Author: SID:2412494
    /// </summary>
    private static readonly (string Email, string Password, string FirstName, string LastName, string DeviceId)[] PatientConfigs =
    [
        ("patient.alice@graphenetrace.local", "Patient@Alice123", "Alice", "Thompson", "1c0fd777"),
        ("patient.bob@graphenetrace.local", "Patient@Bob123", "Bob", "Martinez", "543d4676"),
        ("patient.carol@graphenetrace.local", "Patient@Carol123", "Carol", "Johnson", "71e66ab3"),
        ("patient.david@graphenetrace.local", "Patient@David123", "David", "Williams", "d13043b3"),
        ("patient.emma@graphenetrace.local", "Patient@Emma123", "Emma", "Davis", "de0e9b2c")
    ];
    private const string PatientUserType = "patient";

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

    // Author: SID:2412494
    // Added ApplicationDbContext for session assignment operations
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
            var patients = await EnsurePatientsExistAsync(buildTime);
            var clinician = await EnsureApprovedClinicianExistsAsync(systemUser, buildTime);
            await EnsureUnapprovedClinicianExistsAsync(buildTime);
            await SeedPressureDataAsync(patients);

            // Author: SID:2412494
            // Seed patient-clinician assignments and sample chat messages
            await EnsureAssignmentsAndMessagesAsync(patients, clinician);

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
    /// Ensures all patient accounts exist (auto-approved).
    /// Returns a dictionary mapping device IDs to patient users.
    /// Author: SID:2412494
    /// </summary>
    /// <param name="buildTime">The build time to use for approval timestamps</param>
    private async Task<Dictionary<string, ApplicationUser>> EnsurePatientsExistAsync(DateTime buildTime)
    {
        var patients = new Dictionary<string, ApplicationUser>();

        foreach (var config in PatientConfigs)
        {
            var patient = await _userManager.FindByEmailAsync(config.Email);

            if (patient == null)
            {
                _logger.LogInformation("Patient account not found, creating: {Email}", config.Email);

                patient = new ApplicationUser
                {
                    UserName = config.Email,
                    Email = config.Email,
                    EmailConfirmed = true,
                    FirstName = config.FirstName,
                    LastName = config.LastName,
                    UserType = PatientUserType,
                    CreatedAt = buildTime,
                    ApprovedAt = buildTime,
                    ApprovedBy = null
                };

                var createResult = await _userManager.CreateAsync(patient, config.Password);

                if (createResult.Succeeded)
                {
                    _logger.LogInformation(
                        "Patient account created: {Email} (ID: {UserId})",
                        config.Email, patient.Id);
                }
                else
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to create patient account {Email}: {Errors}", config.Email, errors);
                    throw new InvalidOperationException($"Failed to create patient account {config.Email}: {errors}");
                }
            }
            else
            {
                _logger.LogInformation(
                    "Patient account found (ID: {UserId}), resetting password...",
                    patient.Id);

                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(patient);
                var resetResult = await _userManager.ResetPasswordAsync(patient, resetToken, config.Password);

                if (!resetResult.Succeeded)
                {
                    var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
                    _logger.LogWarning("Failed to reset patient password {Email}: {Errors}", config.Email, errors);
                }

                patient.ApprovedAt = buildTime;
                patient.ApprovedBy = null;
                await _userManager.UpdateAsync(patient);
            }

            patients[config.DeviceId] = patient;
        }

        return patients;
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

    /// <summary>
    /// Seeds pressure data from CSV files in the resources directory.
    /// Assigns sessions to patients based on device ID mapping.
    /// Author: SID:2412494
    /// </summary>
    /// <param name="patients">Dictionary mapping device IDs to patient users</param>
    private async Task SeedPressureDataAsync(Dictionary<string, ApplicationUser> patients)
    {
        try
        {
            // Path to resources directory - use the path from PressureDataService
            string resourcePath = "../Resources/GTLB-Data";

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

                // Look up patient by device ID
                Guid? patientId = null;
                patients.TryGetValue(deviceId, out var patient);
                if (patient != null)
                {
                    patientId = patient.Id;
                }

                // If session exists, ensure patient assignment is correct
                if (existingSession != null)
                {
                    if (existingSession.PatientId != patientId)
                    {
                        existingSession.PatientId = patientId;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Updated patient assignment for session {DeviceId} on {Date} (Patient: {PatientName})",
                            deviceId, parsedDate.ToShortDateString(), patient != null ? $"{patient.FirstName} {patient.LastName}" : "none");
                    }
                    continue;
                }

                _logger.LogInformation("Seeding pressure data for {DeviceId} on {Date} (Patient: {PatientName})...",
                    deviceId, parsedDate.ToShortDateString(), patient != null ? $"{patient.FirstName} {patient.LastName}" : "none");

                string fileContents = await File.ReadAllTextAsync(fileName);

                // Create Session
                var sessionData = new PatientSessionData
                {
                    DeviceId = deviceId,
                    Start = parsedDate,
                    PatientId = patientId
                };

                _context.PatientSessionDatas.Add(sessionData);
                await _context.SaveChangesAsync();

                // Process Snapshots using helper methods from PressureDataService
                List<string> sessionSnapshots = PressureDataService.SplitIntoSnapshots(fileContents, 32);

                // 15 snapshots per second
                double millisec = 1000.0 / 15.0;
                TimeSpan interval = TimeSpan.FromMilliseconds(millisec);
                var snapshotTime = parsedDate;

                var snapshotsToAdd = new List<PatientSnapshotData>(sessionSnapshots.Count);

                foreach (string snapshot in sessionSnapshots)
                {
                    var snapshotInts = PressureDataService.ConvertSnapshotToInt(snapshot, 32);

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

    /// <summary>
    /// Ensures patient-clinician assignments and sample chat messages exist.
    /// Uses PatientClinician model (main's assignment workflow with soft-delete support).
    /// Author: SID:2412494
    /// </summary>
    /// <param name="patients">Dictionary mapping device IDs to patient users</param>
    /// <param name="clinician">The approved clinician to assign patients to</param>
    private async Task EnsureAssignmentsAndMessagesAsync(Dictionary<string, ApplicationUser> patients, ApplicationUser clinician)
    {
        // Assign first patient (Alice) to the approved clinician for chat testing
        var firstPatient = patients.Values.FirstOrDefault();
        if (firstPatient == null)
        {
            _logger.LogWarning("No patients found to create assignments");
            return;
        }

        // Check if assignment exists using PatientClinician (main's model with soft-delete)
        var assignment = await _context.PatientClinicians
            .FirstOrDefaultAsync(a => a.PatientId == firstPatient.Id && a.ClinicianId == clinician.Id && a.UnassignedAt == null);

        if (assignment == null)
        {
            _logger.LogInformation("Creating assignment between {Patient} and {Clinician}", firstPatient.Email, clinician.Email);
            assignment = new PatientClinician
            {
                PatientId = firstPatient.Id,
                ClinicianId = clinician.Id,
                AssignedAt = DateTime.UtcNow
            };
            _context.PatientClinicians.Add(assignment);
            await _context.SaveChangesAsync();
        }

        // Check if messages exist
        var messagesExist = await _context.ChatMessages
            .AnyAsync(m => (m.SenderId == firstPatient.Id && m.ReceiverId == clinician.Id) ||
                           (m.SenderId == clinician.Id && m.ReceiverId == firstPatient.Id));

        if (!messagesExist)
        {
            _logger.LogInformation("Seeding sample chat messages");
            var now = DateTime.UtcNow;

            var messages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    SenderId = clinician.Id,
                    ReceiverId = firstPatient.Id,
                    Content = "Hello! How are you feeling today?",
                    SentAt = now.AddMinutes(-30),
                    IsRead = true
                },
                new ChatMessage
                {
                    SenderId = firstPatient.Id,
                    ReceiverId = clinician.Id,
                    Content = "I'm feeling a bit better, thanks.",
                    SentAt = now.AddMinutes(-25),
                    IsRead = true
                },
                new ChatMessage
                {
                    SenderId = clinician.Id,
                    ReceiverId = firstPatient.Id,
                    Content = "That's good to hear. Have you been monitoring your pressure?",
                    SentAt = now.AddMinutes(-20),
                    IsRead = false
                }
            };

            _context.ChatMessages.AddRange(messages);
            await _context.SaveChangesAsync();
        }
    }
}
