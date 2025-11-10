using GrapheneTrace.Web.Models;
using Microsoft.AspNetCore.Identity;

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
    private readonly ILogger<UserManagementService> _logger;

    /// <summary>
    /// Initializes the UserManagementService.
    /// Author: SID:2402513
    /// </summary>
    public UserManagementService(
        UserManager<ApplicationUser> userManager,
        ILogger<UserManagementService> logger)
    {
        _userManager = userManager;
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
    /// </summary>
    public async Task<(bool Success, string Message)> CreateUserAsync(
        ApplicationUser user,
        string password,
        string userType)
    {
        try
        {
            user.UserType = userType;
            user.EmailConfirmed = true;

            var result = await _userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, $"Failed to create user: {errors}");
            }

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
}
