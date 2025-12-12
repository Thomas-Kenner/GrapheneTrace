using GrapheneTrace.Web.Models;
using GrapheneTrace.Web.Services;
using GrapheneTrace.Web.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
    [Inject] private ApplicationDbContext DbContext { get; set; } = default!;

    private string firstName = "";
    private string lastName = "";
    private string email = "";
    private string phone = "";
    private string address = "";
    private string city = "";
    private string postcode = "";
    private string country = "";
    private string personalMessage = "";
    private bool personalSuccess = false;
    private bool isSavingPersonal = false;
    private Guid currentUserId;
    
    // Updated: 2402513 - Clinician request state
    private PatientClinician? assignedClinician = null;
    private bool showRequestClinicianModal = false;
    private string clinicianSearchQuery = "";
    private List<ApplicationUser> clinicianSearchResults = new();
    private bool isSearchingClinician = false;
    private string clinicianRequestMessage = "";
    private bool clinicianRequestSuccess = false;

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
                    phone = currentUser.Phone ?? "";
                    address = currentUser.Address ?? "";
                    city = currentUser.City ?? "";
                    postcode = currentUser.Postcode ?? "";
                    country = currentUser.Country ?? "";
                }

                // Updated: 2402513 - Load assigned clinician
                await LoadAssignedClinician();
            }
        }
    }

    /// <summary>
    /// Loads the currently assigned clinician for the patient.
    /// Author: 2402513
    /// </summary>
    private async Task LoadAssignedClinician()
    {
        assignedClinician = await DbContext.PatientClinicians
            .Where(pc => pc.PatientId == currentUserId && pc.UnassignedAt == null)
            .Include(pc => pc.Clinician)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Validates UK-style postcode format.
    /// Author: 2402513
    /// </summary>
    /// <param name="postcode">Postcode to validate</param>
    /// <returns>True if valid UK postcode format, false otherwise</returns>
    private bool IsValidUKPostcode(string postcode)
    {
        if (string.IsNullOrWhiteSpace(postcode))
            return false;

        // UK postcode pattern: AA9A 9AA or A9A 9AA or A9 9AA or A99 9AA or AA9 9AA or AA99 9AA
        // Remove spaces and convert to uppercase for validation
        var cleaned = postcode.Replace(" ", "").ToUpper();
        var pattern = @"^[A-Z]{1,2}[0-9R][0-9A-Z]?\s?[0-9][ABD-HJLNP-UW-Z]{2}$";
        return System.Text.RegularExpressions.Regex.IsMatch(cleaned, pattern);
    }

    /// <summary>
    /// Validates UK phone number format.
    /// Author: 2402513
    /// </summary>
    /// <param name="phone">Phone number to validate</param>
    /// <returns>True if valid UK phone format, false otherwise</returns>
    private bool IsValidUKPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return false;

        // Remove spaces, dashes, and parentheses
        var cleaned = phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        
        // UK phone numbers: +44 or 0 followed by 10 digits, or 11 digits starting with 0
        // Allow common formats: +44..., 0..., or 07... (mobile)
        if (cleaned.StartsWith("+44"))
            cleaned = "0" + cleaned.Substring(3);
        
        // Should be 11 digits starting with 0, or 10 digits (without leading 0)
        var digitsOnly = cleaned.All(char.IsDigit);
        return digitsOnly && (cleaned.Length == 10 || cleaned.Length == 11);
    }

    /// <summary>
    /// Saves personal details including address fields with UK-style validation.
    /// Updated: 2402513 - Added address fields and validation
    /// </summary>
    private async Task SavePersonalDetails()
    {
        isSavingPersonal = true;
        personalMessage = "";

        try
        {
            // Validation
            if (string.IsNullOrWhiteSpace(firstName))
            {
                personalMessage = "First name is required";
                personalSuccess = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(lastName))
            {
                personalMessage = "Last name is required";
                personalSuccess = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                personalMessage = "Email is required";
                personalSuccess = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(phone))
            {
                personalMessage = "Phone number is required";
                personalSuccess = false;
                return;
            }

            if (!IsValidUKPhone(phone))
            {
                personalMessage = "Please enter a valid UK phone number";
                personalSuccess = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                personalMessage = "Address is required";
                personalSuccess = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(city))
            {
                personalMessage = "City is required";
                personalSuccess = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(postcode))
            {
                personalMessage = "Postcode is required";
                personalSuccess = false;
                return;
            }

            if (!IsValidUKPostcode(postcode))
            {
                personalMessage = "Please enter a valid UK postcode (e.g., SW1A 1AA)";
                personalSuccess = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(country))
            {
                personalMessage = "Country is required";
                personalSuccess = false;
                return;
            }

            var user = await UserManager.FindByIdAsync(currentUserId.ToString());
            if (user == null)
            {
                personalMessage = "User not found";
                personalSuccess = false;
                return;
            }

            // Update all fields
            user.FirstName = firstName;
            user.LastName = lastName;
            user.Email = email;
            user.UserName = email;
            user.Phone = phone;
            user.Address = address;
            user.City = city;
            user.Postcode = postcode;
            user.Country = country;

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

    /// <summary>
    /// Shows the request clinician modal.
    /// Author: 2402513
    /// </summary>
    private void ShowRequestClinicianModal()
    {
        showRequestClinicianModal = true;
        clinicianSearchQuery = "";
        clinicianSearchResults.Clear();
        clinicianRequestMessage = "";
    }

    /// <summary>
    /// Closes the request clinician modal.
    /// Author: 2402513
    /// </summary>
    private void CloseRequestClinicianModal()
    {
        showRequestClinicianModal = false;
        clinicianSearchQuery = "";
        clinicianSearchResults.Clear();
        clinicianRequestMessage = "";
    }

    /// <summary>
    /// Searches for clinicians by name or email.
    /// Author: 2402513
    /// </summary>
    private async Task SearchClinicians()
    {
        if (string.IsNullOrWhiteSpace(clinicianSearchQuery) || clinicianSearchQuery.Length < 2)
        {
            clinicianSearchResults.Clear();
            return;
        }

        isSearchingClinician = true;
        StateHasChanged();

        try
        {
            var query = clinicianSearchQuery.ToLower();
            clinicianSearchResults = await DbContext.Users
                .Where(u => u.UserType == "clinician"
                    && (u.FirstName.ToLower().Contains(query)
                        || u.LastName.ToLower().Contains(query)
                        || (u.Email != null && u.Email.ToLower().Contains(query))))
                .Take(10)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            clinicianRequestMessage = $"Error searching: {ex.Message}";
            clinicianRequestSuccess = false;
        }
        finally
        {
            isSearchingClinician = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Selects a clinician and creates a request.
    /// Author: 2402513
    /// </summary>
    private async Task SelectClinician(Guid clinicianId)
    {
        clinicianRequestMessage = "";
        clinicianRequestSuccess = false;

        var result = await UserManagementService.CreatePatientClinicianRequestAsync(currentUserId, clinicianId);
        
        clinicianRequestMessage = result.Message;
        clinicianRequestSuccess = result.Success;

        if (result.Success)
        {
            // Reload assigned clinician and close modal after a short delay
            await LoadAssignedClinician();
            await Task.Delay(1500);
            CloseRequestClinicianModal();
        }

        StateHasChanged();
    }
}
