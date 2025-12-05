using GrapheneTrace.Web.Models;
using GrapheneTrace.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;

namespace GrapheneTrace.Web.Components.Pages.Patient;

/// <summary>
/// Patient Settings Page (Placeholder/Visual Design Reference)
/// Author: SID:2402513
///
/// NOTE: This is a design reference file.
/// The actual working settings page is at Settings/Index.razor.
/// </summary>
public partial class SettingsPlaceholder
{
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private UserManagementService UserManagementService { get; set; } = default!;

    private string firstName = "";
    private string lastName = "";
    private string email = "";
    private string personalMessage = "";
    private bool personalSuccess = false;
    private bool isSavingPersonal = false;
    private Guid currentUserId;

    private int pressureThreshold = 80;
    private bool pushNotifications = false;
    private bool emailNotifications = false;
    private bool deviceAlerts = false;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                currentUserId = userId;
                var currentUser = await UserManager.FindByIdAsync(userId.ToString());
                if (currentUser != null)
                {
                    firstName = currentUser.FirstName;
                    lastName = currentUser.LastName;
                    email = currentUser.Email ?? "";
                }
            }
        }
    }

    private async Task SavePersonalDetails()
    {
        isSavingPersonal = true;
        personalMessage = "";

        try
        {
            var user = await UserManager.FindByIdAsync(currentUserId.ToString());
            if (user == null)
            {
                personalMessage = "User not found";
                personalSuccess = false;
                return;
            }

            user.FirstName = firstName;
            user.LastName = lastName;
            user.Email = email;
            user.UserName = email;

            var result = await UserManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                personalMessage = "Details saved successfully!";
                personalSuccess = true;
                await Task.Delay(2000);
                personalMessage = "";
            }
            else
            {
                personalMessage = "Failed to save: " + string.Join(", ", result.Errors.Select(e => e.Description));
                personalSuccess = false;
            }
        }
        catch (Exception ex)
        {
            personalMessage = $"Error: {ex.Message}";
            personalSuccess = false;
        }
        finally
        {
            isSavingPersonal = false;
            StateHasChanged();
        }
    }
}
