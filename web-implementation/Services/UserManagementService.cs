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

    /// <summary>
    /// Initializes the UserManagementService.
    /// Author: SID:2402513
    /// </summary>
    public UserManagementService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
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
    public async Task<(bool Success, string Message)> UpdateUserAsync(ApplicationUser user)
    {
        try
        {
            var result = await _userManager.UpdateAsync(user);

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
}
