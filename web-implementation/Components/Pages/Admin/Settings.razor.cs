using GrapheneTrace.Web.Models;
using GrapheneTrace.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace GrapheneTrace.Web.Components.Pages.Admin;

/// <summary>
/// Admin Settings Page
/// Author: 2402513
///
/// Purpose:
/// Provides interface for administrators to manage their personal account settings
/// including profile information and password changes.
/// </summary>
public partial class Settings
{
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private UserManagementService UserManagementService { get; set; } = default!;

    private ApplicationUser adminUser = new();
    private string currentPassword = "";
    private string newPassword = "";
    private string confirmPassword = "";
    private string profileMessage = "";
    private string passwordMessage = "";
    private bool profileSuccess = false;
    private bool passwordSuccess = false;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var guidUserId))
        {
            var user = await UserManagementService.GetUserByIdAsync(guidUserId);
            if (user != null)
            {
                adminUser = new ApplicationUser
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    UserName = user.UserName,
                    UserType = user.UserType,
                    DeactivatedAt = user.DeactivatedAt,
                    SecurityStamp = user.SecurityStamp,
                    ConcurrencyStamp = user.ConcurrencyStamp
                };
            }
        }
    }

    private async Task SaveProfileSettings()
    {
        var (success, message) = await UserManagementService.UpdateUserAsync(adminUser);
        profileSuccess = success;
        profileMessage = message;
    }

    private async Task ChangePassword()
    {
        if (newPassword != confirmPassword)
        {
            passwordSuccess = false;
            passwordMessage = "Passwords do not match";
            return;
        }

        var guidUserId = adminUser.Id;
        var (success, message) = await UserManagementService.ChangePasswordAsync(
            guidUserId,
            currentPassword,
            newPassword);

        passwordSuccess = success;
        passwordMessage = message;

        if (success)
        {
            currentPassword = "";
            newPassword = "";
            confirmPassword = "";
            await Task.Delay(2000);
            passwordMessage = "";
        }
    }
}
