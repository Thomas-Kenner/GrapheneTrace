using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace GrapheneTrace.Web.Components.Pages.Clinician;

/// <summary>
/// Clinician Settings Page
/// Author: SID:2402513
/// Route: /clinician/settings
///
/// Purpose: Allow clinicians to view and update their profile settings.
/// </summary>
public partial class Settings
{
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private string firstName = "John";
    private string lastName = "Doe";
    private string email = "clinician@graphene.com";
    private string specialization = "General Practice";
    private bool showSuccessMessage = false;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity?.IsAuthenticated ?? false)
        {
            // In a real app, we would fetch user details from a service
            // For now, we'll just use the name from the claim if available
            var name = user.Identity.Name;
            if (!string.IsNullOrEmpty(name))
            {
                // Simple split for demo purposes
                var parts = name.Split(' ');
                if (parts.Length > 0) firstName = parts[0];
                if (parts.Length > 1) lastName = parts[1];
            }

            var emailClaim = user.FindFirst("email");
            if (emailClaim != null) email = emailClaim.Value;
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
