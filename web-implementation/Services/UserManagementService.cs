using GrapheneTrace.Web.Models;
using GrapheneTrace.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GrapheneTrace.Web.Services;

/// <summary>
/// Service for managing users in the admin dashboard.
/// Author: SID:2402513
/// </summary>
/// <remarks>
/// Purpose: Provides CRUD operations for user management.
///
/// Operations:
/// - GetAllUsersAsync(): Retrieve all users
/// - GetUserByIdAsync(userId): Get single user by ID
/// - CreateUserAsync(user, password): Create new user
/// - UpdateUserAsync(user): Update user details
/// - DeleteUserAsync(userId): Delete/deactivate user
/// - ChangePasswordAsync(userId, oldPassword, newPassword): Change user password
/// </remarks>
public class UserManagementService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserManagementService> _logger;

    /// <summary>
    /// Initializes the UserManagementService.
    /// Author: SID:2402513
    /// Updated: 2402513 - Added ApplicationDbContext injection for PatientClinician operations
    /// </summary>
    /// <remarks>
    /// Database Context Addition (Updated: 2402513):
    /// Added ApplicationDbContext injection to enable direct database access for
    /// patient-clinician relationship management. This is needed because UserManager
    /// doesn't provide direct access to PatientClinician entities.
    /// </remarks>
    public UserManagementService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        ILogger<UserManagementService> logger)
    {
        _userManager = userManager;
        _context = context;  // Updated: 2402513 - Added for PatientClinician operations
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all users from the system.
    /// Author: SID:2402513
    /// </summary>
    public async Task<List<ApplicationUser>> GetAllUsersAsync()
    {
        return await Task.FromResult(_userManager.Users.ToList());
    }

    /// <summary>
    /// Retrieves a single user by ID.
    /// Author: SID:2402513
    /// </summary>
    public async Task<ApplicationUser?> GetUserByIdAsync(Guid userId)
    {
        return await _userManager.FindByIdAsync(userId.ToString());
    }

    /// <summary>
    /// Creates a new user with the provided details.
    /// Author: SID:2402513
    /// Updated: SID:2412494 - Auto-approve admin-created users (they don't need to go through approval workflow)
    /// </summary>
    /// <remarks>
    /// Admin-Created User Auto-Approval (Updated: SID:2412494):
    /// Users created by an admin through the admin dashboard are automatically approved.
    /// This differs from self-registration where clinician/admin accounts require manual approval.
    /// Since this method is only called from the admin Users page, we can safely auto-approve.
    /// </remarks>
    public async Task<(bool Success, string Message)> CreateUserAsync(
        ApplicationUser user,
        string password,
        string userType)
    {
        try
        {
            user.UserType = userType;
            user.EmailConfirmed = true;
            // Author: SID:2412494 - Auto-approve admin-created users
            user.ApprovedAt = DateTime.UtcNow;

            var result = await _userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, $"Failed to create user: {errors}");
            }

            _logger.LogInformation("Admin created and auto-approved user {UserId} ({Email}) with type {UserType}",
                user.Id, user.Email, userType);

            return (true, "User created successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Error creating user: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates user details (name, email).
    /// Author: SID:2402513
    /// </summary>
    /// <remarks>
    /// Implementation Pattern: Fetch-then-update to avoid EF Core tracking conflicts.
    ///
    /// Why this approach:
    /// - Fetches the user from database first (ensures it's tracked by current context)
    /// - Updates only the editable properties (FirstName, LastName, Email)
    /// - Avoids detached entity conflicts when user was loaded in another context
    /// - Preserves Identity-managed fields (SecurityStamp, ConcurrencyStamp, etc.)
    /// </remarks>
    public async Task<(bool Success, string Message)> UpdateUserAsync(ApplicationUser user)
    {
        try
        {
            // Fetch the user from database to ensure it's tracked by the current context
            var existingUser = await _userManager.FindByIdAsync(user.Id.ToString());
            if (existingUser == null)
            {
                return (false, "User not found");
            }

            // Update only the editable properties
            existingUser.FirstName = user.FirstName;
            existingUser.LastName = user.LastName;
            existingUser.Email = user.Email;
            existingUser.UserName = user.Email; // Keep username in sync with email
            // Updated: 2402513 - Include address fields in user updates
            existingUser.Phone = user.Phone;
            existingUser.Address = user.Address;
            existingUser.City = user.City;
            existingUser.Postcode = user.Postcode;
            existingUser.Country = user.Country;

            var result = await _userManager.UpdateAsync(existingUser);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, $"Failed to update user: {errors}");
            }

            return (true, "User updated successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Error updating user: {ex.Message}");
        }
    }

    /// <summary>
    /// Soft deletes a user by setting DeactivatedAt timestamp.
    /// Author: SID:2402513
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteUserAsync(Guid userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return (false, "User not found");
            }

            user.DeactivatedAt = DateTime.UtcNow;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, $"Failed to delete user: {errors}");
            }

            return (true, "User deleted successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Error deleting user: {ex.Message}");
        }
    }

    /// <summary>
    /// Changes a user's password.
    /// Author: SID:2402513
    /// </summary>
    public async Task<(bool Success, string Message)> ChangePasswordAsync(
        Guid userId,
        string oldPassword,
        string newPassword)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return (false, "User not found");
            }

            var result = await _userManager.ChangePasswordAsync(user, oldPassword, newPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, $"Failed to change password: {errors}");
            }

            return (true, "Password changed successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Error changing password: {ex.Message}");
        }
    }

    /// <summary>
    /// Changes a user's password as admin (without requiring old password).
    /// Author: SID:2402513
    /// </summary>
    public async Task<(bool Success, string Message)> AdminChangePasswordAsync(
        Guid userId,
        string newPassword)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return (false, "User not found");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, $"Failed to reset password: {errors}");
            }

            return (true, "Password reset successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Error resetting password: {ex.Message}");
        }
    }

    /// <summary>
    /// Approves a pending user account by setting the ApprovedAt timestamp and ApprovedBy foreign key.
    /// Author: SID:2412494
    /// </summary>
    /// <param name="userId">ID of the user to approve</param>
    /// <param name="approvedByAdminId">ID of the admin performing the approval</param>
    /// <returns>Tuple containing success status and message</returns>
    /// <remarks>
    /// Account Approval Workflow:
    /// - Sets ApprovedAt to current UTC timestamp
    /// - Sets ApprovedBy foreign key to track which admin approved the account
    /// - Idempotent operation: if user is already approved, returns error without modification
    /// - Audit logging for compliance tracking (HIPAA requirement)
    ///
    /// Race Condition Protection:
    /// The ApprovedAt != null check makes this method idempotent. If two admins
    /// attempt to approve simultaneously, the second approval will fail gracefully
    /// with an "already approved" message.
    ///
    /// Security:
    /// - Only callable by admin users (enforced by component authorization)
    /// - Validates user exists before updating
    /// - Tracks approval audit trail in database and logs
    /// </remarks>
    public async Task<(bool Success, string Message)> ApproveUserAsync(Guid userId, Guid approvedByAdminId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                _logger.LogWarning("Attempted to approve non-existent user {UserId}", userId);
                return (false, "User not found");
            }

            // Idempotent check - prevents race condition if multiple admins approve simultaneously
            if (user.ApprovedAt != null)
            {
                _logger.LogWarning("Attempted to approve already-approved user {UserId}", userId);
                return (false, "User already approved");
            }

            // Set approval timestamp and approving admin
            user.ApprovedAt = DateTime.UtcNow;
            user.ApprovedBy = approvedByAdminId;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Failed to approve user {UserId}: {Errors}", userId, errors);
                return (false, $"Failed to approve user: {errors}");
            }

            // Audit logging for compliance and tracking
            _logger.LogInformation(
                "User {UserId} ({UserEmail}) approved by admin {AdminId} at {Timestamp}",
                userId, user.Email, approvedByAdminId, user.ApprovedAt);

            return (true, "User approved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving user {UserId}", userId);
            return (false, $"Error approving user: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves all patients assigned to a specific clinician.
    /// Author: SID:2402513
    /// </summary>
    public async Task<List<ApplicationUser>> GetPatientsByClinicianAsync(Guid clinicianId)
    {
        // In a real scenario with EF Core, we would use .Where(u => u.AssignedClinicianId == clinicianId)
        // Since we are using UserManager which doesn't expose IQueryable directly in the same way for custom fields easily without casting,
        // we will fetch all users and filter in memory for this implementation, 
        // or rely on the fact that we can access the context if needed.
        // For simplicity and safety with UserManager abstraction:
        
        var allUsers = _userManager.Users.ToList();
        return allUsers
            .Where(u => u.UserType == "patient" && u.AssignedClinicianId == clinicianId)
            .ToList();
    }

    /// <summary>
    /// Assigns a patient to a clinician (admin direct assignment).
    /// Author: 2402513
    /// </summary>
    /// <param name="patientId">ID of the patient to assign</param>
    /// <param name="clinicianId">ID of the clinician to assign to</param>
    /// <returns>Tuple containing success status and message</returns>
    /// <remarks>
    /// Purpose: Allows administrators to directly assign patients to clinicians without
    /// requiring clinician approval. This bypasses the normal PatientClinicianRequest workflow.
    ///
    /// Workflow:
    /// 1. Validates that both patient and clinician exist and have correct UserType
    /// 2. Checks for existing active assignment (prevents duplicates)
    /// 3. Creates new PatientClinician record with AssignedAt timestamp
    /// 4. Saves to database and logs the action for audit trail
    ///
    /// Design Pattern: Direct database operation using ApplicationDbContext.
    /// Admin assignments are immediate and don't require approval workflow.
    /// </remarks>
    public async Task<(bool Success, string Message)> AssignPatientToClinicianAsync(Guid patientId, Guid clinicianId)
    {
        try
        {
            // Check if patient exists
            var patient = await _userManager.FindByIdAsync(patientId.ToString());
            if (patient == null || patient.UserType != "patient")
            {
                return (false, "Patient not found");
            }

            // Check if clinician exists
            var clinician = await _userManager.FindByIdAsync(clinicianId.ToString());
            if (clinician == null || clinician.UserType != "clinician")
            {
                return (false, "Clinician not found");
            }

            // Check if assignment already exists (active)
            var existingAssignment = await _context.PatientClinicians
                .FirstOrDefaultAsync(pc => pc.PatientId == patientId 
                    && pc.ClinicianId == clinicianId 
                    && pc.UnassignedAt == null);

            if (existingAssignment != null)
            {
                return (false, "Patient is already assigned to this clinician");
            }

            // Create new assignment
            var assignment = new PatientClinician
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                ClinicianId = clinicianId,
                AssignedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.PatientClinicians.Add(assignment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Patient {PatientId} assigned to clinician {ClinicianId} by admin", patientId, clinicianId);
            return (true, "Patient assigned to clinician successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning patient {PatientId} to clinician {ClinicianId}", patientId, clinicianId);
            return (false, $"Error assigning patient: {ex.Message}");
        }
    }

    /// <summary>
    /// Unassigns a patient from a clinician (soft delete).
    /// Author: 2402513
    /// </summary>
    /// <param name="patientId">ID of the patient to unassign</param>
    /// <param name="clinicianId">ID of the clinician to unassign from</param>
    /// <returns>Tuple containing success status and message</returns>
    /// <remarks>
    /// Purpose: Removes the relationship between a patient and clinician using soft delete.
    ///
    /// Soft Delete Pattern:
    /// - Sets UnassignedAt timestamp instead of deleting the record
    /// - Preserves historical data for HIPAA compliance and audit trail
    /// - Active assignments are filtered by UnassignedAt == null
    /// - Allows tracking of assignment history and duration
    ///
    /// Use Cases:
    /// - Patient transfers to different clinician
    /// - Clinician no longer treating patient
    /// - Administrative corrections
    /// </remarks>
    public async Task<(bool Success, string Message)> UnassignPatientFromClinicianAsync(Guid patientId, Guid clinicianId)
    {
        try
        {
            var assignment = await _context.PatientClinicians
                .FirstOrDefaultAsync(pc => pc.PatientId == patientId 
                    && pc.ClinicianId == clinicianId 
                    && pc.UnassignedAt == null);

            if (assignment == null)
            {
                return (false, "Assignment not found");
            }

            assignment.UnassignedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Patient {PatientId} unassigned from clinician {ClinicianId}", patientId, clinicianId);
            return (true, "Patient unassigned successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unassigning patient {PatientId} from clinician {ClinicianId}", patientId, clinicianId);
            return (false, $"Error unassigning patient: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a new patient-clinician request.
    /// Author: 2402513
    /// </summary>
    /// <param name="patientId">ID of the patient</param>
    /// <param name="clinicianId">ID of the clinician</param>
    /// <returns>Tuple containing success status and message</returns>
    /// <remarks>
    /// Purpose: Allows patients or clinicians to request assignment relationships.
    /// 
    /// Workflow:
    /// 1. Validates that both patient and clinician exist and have correct UserType
    /// 2. Checks for existing pending request (prevents duplicates)
    /// 3. Checks for existing active assignment (prevents unnecessary requests)
    /// 4. Creates new PatientClinicianRequest with pending status
    /// 5. Saves to database and logs the action
    ///
    /// Design Pattern: Request-based workflow for patient-clinician relationships.
    /// Requests require admin or clinician approval before assignment is created.
    /// </remarks>
    public async Task<(bool Success, string Message)> CreatePatientClinicianRequestAsync(Guid patientId, Guid clinicianId)
    {
        try
        {
            // Check if patient exists
            var patient = await _userManager.FindByIdAsync(patientId.ToString());
            if (patient == null || patient.UserType != "patient")
            {
                return (false, "Patient not found");
            }

            // Check if clinician exists
            var clinician = await _userManager.FindByIdAsync(clinicianId.ToString());
            if (clinician == null || clinician.UserType != "clinician")
            {
                return (false, "Clinician not found");
            }

            // Check if there's already a pending request
            var existingPendingRequest = await _context.PatientClinicianRequests
                .FirstOrDefaultAsync(pcr => pcr.PatientId == patientId 
                    && pcr.ClinicianId == clinicianId 
                    && pcr.Status == "pending");

            if (existingPendingRequest != null)
            {
                return (false, "A pending request already exists for this patient-clinician pair");
            }

            // Check if there's already an active assignment
            var existingAssignment = await _context.PatientClinicians
                .FirstOrDefaultAsync(pc => pc.PatientId == patientId 
                    && pc.ClinicianId == clinicianId 
                    && pc.UnassignedAt == null);

            if (existingAssignment != null)
            {
                return (false, "Patient is already assigned to this clinician");
            }

            // Create new request
            var request = new PatientClinicianRequest
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                ClinicianId = clinicianId,
                Status = "pending",
                RequestedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.PatientClinicianRequests.Add(request);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Patient-clinician request created: Patient {PatientId} -> Clinician {ClinicianId}", patientId, clinicianId);
            return (true, "Request created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating patient-clinician request for Patient {PatientId} and Clinician {ClinicianId}", patientId, clinicianId);
            return (false, $"Error creating request: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves all patient-clinician assignments (active and inactive).
    /// Author: 2402513
    /// </summary>
    /// <param name="activeOnly">If true, returns only active assignments (UnassignedAt == null)</param>
    /// <returns>List of PatientClinician relationships</returns>
    /// <remarks>
    /// Purpose: Retrieves all patient-clinician assignment records for admin dashboard display.
    ///
    /// Features:
    /// - Includes navigation properties (Patient, Clinician) for display
    /// - Supports filtering by active/inactive status
    /// - Ordered by AssignedAt descending (most recent first)
    /// - Used by admin assignment management page
    ///
    /// Performance: Uses EF Core Include() to eagerly load related entities,
    /// preventing N+1 query issues when displaying patient/clinician names.
    /// </remarks>
    public async Task<List<PatientClinician>> GetAllPatientClinicianAssignmentsAsync(bool activeOnly = false)
    {
        var query = _context.PatientClinicians
            .Include(pc => pc.Patient)
            .Include(pc => pc.Clinician)
            .AsQueryable();

        if (activeOnly)
        {
            query = query.Where(pc => pc.UnassignedAt == null);
        }

        return await query
            .OrderByDescending(pc => pc.AssignedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all active patients in the system.
    /// Author: SID:2412494
    /// </summary>
    /// <returns>List of active patient users ordered by name</returns>
    public async Task<List<ApplicationUser>> GetAllPatientsAsync()
    {
        return await _context.Users
            .Where(u => u.UserType == "patient" && u.DeactivatedAt == null)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all patient IDs assigned to a specific clinician.
    /// Uses PatientClinician model with soft-delete (UnassignedAt == null for active assignments).
    /// Author: SID:2412494
    /// </summary>
    /// <param name="clinicianId">The clinician's user ID</param>
    /// <returns>Set of assigned patient IDs</returns>
    public async Task<HashSet<Guid>> GetAssignedPatientIdsAsync(Guid clinicianId)
    {
        var patientIds = await _context.PatientClinicians
            .Where(a => a.ClinicianId == clinicianId && a.UnassignedAt == null)
            .Select(a => a.PatientId)
            .ToListAsync();
        return patientIds.ToHashSet();
    }
}
