using GrapheneTrace.Web.Models;
using GrapheneTrace.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GrapheneTrace.Web.Components.Pages.Admin;

/// <summary>
/// Admin Users Management Page
/// Author: 2402513
///
/// Purpose:
/// Comprehensive user management interface allowing administrators to view,
/// create, edit, and delete system users.
/// </summary>
public partial class Users
{
    [Inject] private UserManagementService UserManagementService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private List<ApplicationUser>? users;
    private List<ApplicationUser>? filteredUsers;
    private ApplicationUser? editingUser;
    private ApplicationUser newUser = new();
    private string newUserPassword = "";
    private string searchQuery = "";
    private bool isLoading = true;
    private bool showCreateModal = false;
    private bool showEditModal = false;
    private string createMessage = "";
    private string editMessage = "";
    private bool createSuccess = false;
    private bool editSuccess = false;

    protected override async Task OnInitializedAsync()
    {
        await LoadUsers();
    }

    private async Task LoadUsers()
    {
        isLoading = true;
        users = await UserManagementService.GetAllUsersAsync();
        filteredUsers = users.Where(u => u.DeactivatedAt == null).ToList();
        isLoading = false;
    }

    private void OnSearchChange(ChangeEventArgs e)
    {
        searchQuery = e.Value?.ToString() ?? "";
        FilterUsers();
    }

    private void FilterUsers()
    {
        if (users == null) return;

        filteredUsers = users
            .Where(u => u.DeactivatedAt == null &&
                       (u.FirstName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                        u.LastName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                        (u.Email ?? "").Contains(searchQuery, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private void EditUser(ApplicationUser user)
    {
        editingUser = new ApplicationUser
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
        showEditModal = true;
    }

    private async Task SaveUser()
    {
        if (editingUser == null) return;

        var (success, message) = await UserManagementService.UpdateUserAsync(editingUser);
        editSuccess = success;
        editMessage = message;

        if (success)
        {
            await LoadUsers();
            await Task.Delay(2000);
            showEditModal = false;
        }
    }

    private async Task CreateUser()
    {
        if (string.IsNullOrEmpty(newUser.Email) || string.IsNullOrEmpty(newUserPassword))
        {
            createMessage = "Email and password are required";
            createSuccess = false;
            return;
        }

        // Author: SID:2412494 - Validate user type is selected to prevent accounts without roles
        if (string.IsNullOrEmpty(newUser.UserType))
        {
            createMessage = "Please select a user type";
            createSuccess = false;
            return;
        }

        newUser.UserName = newUser.Email;

        var (success, message) = await UserManagementService.CreateUserAsync(
            newUser,
            newUserPassword,
            newUser.UserType);

        createSuccess = success;
        createMessage = message;

        if (success)
        {
            newUser = new();
            newUserPassword = "";
            await LoadUsers();
            await Task.Delay(2000);
            showCreateModal = false;
        }
    }

    private async Task DeleteUser(Guid userId)
    {
        var confirmed = await JS.InvokeAsync<bool>("confirm", "Are you sure you want to delete this user?");
        if (!confirmed)
            return;

        var (success, message) = await UserManagementService.DeleteUserAsync(userId);

        if (success)
        {
            await LoadUsers();
        }
        else
        {
            await JS.InvokeVoidAsync("alert", message);
        }
    }

    private string GetUserTypeColor(string userType)
    {
        return userType switch
        {
            "admin" => "#030213",
            "clinician" => "#10b981",
            "patient" => "#3b82f6",
            _ => "#6b7280"
        };
    }
}
