using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GrapheneTrace.Web.Components.Pages.Clinician;

/// <summary>
/// Clinician Settings Page
/// Author: SID:2402513
/// Updated: 2402513 - Load user data from database and use actual email for placeholder
/// Route: /clinician/settings
///
/// Purpose: Allow clinicians to view and update their profile settings.
/// </summary>
public partial class Settings
{
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private ApplicationDbContext DbContext { get; set; } = default!;

    private string firstName = "";
    private string lastName = "";
    private string email = "";
    private string emailPlaceholder = "email@example.com";
    private string specialization = "";
    private bool showSuccessMessage = false;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity?.IsAuthenticated ?? false)
        {
            // Get user ID from claims
            var userIdClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            Guid userId = Guid.Empty;
            
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out userId))
            {
                // Load user from database
                var dbUser = await DbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == userId && u.UserType == "clinician");

                if (dbUser != null)
                {
                    firstName = dbUser.FirstName ?? "";
                    lastName = dbUser.LastName ?? "";
                    email = dbUser.Email ?? "";
                    emailPlaceholder = dbUser.Email ?? "email@example.com";
                    // Specialization is not stored in ApplicationUser model, so we'll keep it empty
                }
            }
            else
            {
                // Fallback: try to get from email claim
                var emailClaim = user.FindFirst("email") ?? user.FindFirst(System.Security.Claims.ClaimTypes.Email);
                if (emailClaim != null)
                {
                    var dbUser = await DbContext.Users
                        .FirstOrDefaultAsync(u => u.Email == emailClaim.Value && u.UserType == "clinician");
                    
                    if (dbUser != null)
                    {
                        firstName = dbUser.FirstName ?? "";
                        lastName = dbUser.LastName ?? "";
                        email = dbUser.Email ?? "";
                        emailPlaceholder = dbUser.Email ?? "email@example.com";
                    }
                }
            }
        }
    }

    private void SaveSettings()
    {
        // In a real app, we would call a service to update the user's profile
        showSuccessMessage = true;

        // Hide message after 3 seconds
        var timer = new System.Timers.Timer(3000);
        timer.Elapsed += (sender, e) =>
        {
            showSuccessMessage = false;
            InvokeAsync(StateHasChanged);
            timer.Dispose();
        };
        timer.Start();
    }
}
