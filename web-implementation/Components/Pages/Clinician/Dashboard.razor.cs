using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using GrapheneTrace.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GrapheneTrace.Web.Components.Pages.Clinician;

/// <summary>
/// Clinician Dashboard Page
/// Author: SID:2402513
/// Route: /clinician/dashboard
///
/// Purpose: Main dashboard for clinician users to view patients and check their status.
/// </summary>
public partial class Dashboard
{
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private ApplicationDbContext DbContext { get; set; } = default!;
    [Inject] private UserManagementService UserManagementService { get; set; } = default!;

    private bool isPopupVisible = false;
    private bool isLoading = true;
    private string userName = "";
    private Guid clinicianId = Guid.Empty;
    private List<PatientClinician> approvedPatients = new();
    private List<PatientClinicianRequest> pendingRequests = new();
    private Guid? selectedPatientId = null;
    
    // Updated: 2402513 - Request patient modal state
    private bool showRequestModal = false;
    private string patientSearchQuery = "";
    private List<ApplicationUser> searchResults = new();
    private bool isSearching = false;
    private string requestMessage = "";
    private bool requestSuccess = false;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        userName = user.Identity?.Name ?? "Unknown";

        // Get clinician ID from multiple sources
        // Try 1: "sub" claim (standard claim for user ID in many systems)
        var clinicianIdClaim = user.FindFirst("sub")?.Value;
        if (Guid.TryParse(clinicianIdClaim, out var id))
        {
            clinicianId = id;
        }
        else
        {
            // Try 2: Look up by username (from Identity.Name)
            var username = user.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                var clinician = await DbContext.Users
                    .FirstOrDefaultAsync(u => u.UserName == username && u.UserType == "clinician");
                if (clinician != null)
                {
                    clinicianId = clinician.Id;
                }
            }
        }

        // Try 3: If still not found, look up by email
        if (clinicianId == Guid.Empty)
        {
            var email = user.FindFirst("email")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            if (!string.IsNullOrEmpty(email))
            {
                var clinician = await DbContext.Users
                    .FirstOrDefaultAsync(u => u.Email == email && u.UserType == "clinician");
                if (clinician != null)
                {
                    clinicianId = clinician.Id;
                }
            }
        }

        await LoadPatientData();
        isLoading = false;
    }

    private async Task LoadPatientData()
    {
        if (clinicianId == Guid.Empty)
            return;

        // Load approved patients
        approvedPatients = await DbContext.PatientClinicians
            .Where(pc => pc.ClinicianId == clinicianId && pc.UnassignedAt == null)
            .Include(pc => pc.Patient)
            .OrderBy(pc => pc.Patient!.FirstName)
            .ToListAsync();

        // Load pending requests
        pendingRequests = await DbContext.PatientClinicianRequests
            .Where(pcr => pcr.ClinicianId == clinicianId && pcr.Status == "pending")
            .Include(pcr => pcr.Patient)
            .OrderByDescending(pcr => pcr.RequestedAt)
            .ToListAsync();
    }

    private async Task ApproveRequest(Guid requestId)
    {
        var request = await DbContext.PatientClinicianRequests
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
            return;

        // Update request status
        request.Status = "approved";
        request.RespondedAt = DateTime.UtcNow;

        // Create PatientClinician relationship
        var patientClinician = new PatientClinician
        {
            Id = Guid.NewGuid(),
            PatientId = request.PatientId,
            ClinicianId = request.ClinicianId,
            AssignedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        DbContext.PatientClinicianRequests.Update(request);
        DbContext.PatientClinicians.Add(patientClinician);
        await DbContext.SaveChangesAsync();

        // Reload data
        await LoadPatientData();
    }

    private async Task RejectRequest(Guid requestId)
    {
        var request = await DbContext.PatientClinicianRequests
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
            return;

        request.Status = "rejected";
        request.RespondedAt = DateTime.UtcNow;
        request.ResponseReason = "Request declined by clinician";

        DbContext.PatientClinicianRequests.Update(request);
        await DbContext.SaveChangesAsync();

        // Reload data
        await LoadPatientData();
    }

    private void ShowCheckPopup(Guid patientId)
    {
        selectedPatientId = patientId;
        isPopupVisible = true;
    }

    private void ClosePopup()
    {
        isPopupVisible = false;
        selectedPatientId = null;
    }

    /// <summary>
    /// Shows the request patient modal.
    /// Author: 2402513
    /// </summary>
    private void ShowRequestPatientModal()
    {
        showRequestModal = true;
        patientSearchQuery = "";
        searchResults.Clear();
        requestMessage = "";
    }

    /// <summary>
    /// Closes the request patient modal.
    /// Author: 2402513
    /// </summary>
    private void CloseRequestModal()
    {
        showRequestModal = false;
        patientSearchQuery = "";
        searchResults.Clear();
        requestMessage = "";
    }

    /// <summary>
    /// Searches for patients by name or email.
    /// Author: 2402513
    /// </summary>
    private async Task SearchPatients()
    {
        if (string.IsNullOrWhiteSpace(patientSearchQuery) || patientSearchQuery.Length < 2)
        {
            searchResults.Clear();
            return;
        }

        isSearching = true;
        StateHasChanged();

        try
        {
            var query = patientSearchQuery.ToLower();
            searchResults = await DbContext.Users
                .Where(u => u.UserType == "patient"
                    && (u.FirstName.ToLower().Contains(query)
                        || u.LastName.ToLower().Contains(query)
                        || (u.Email != null && u.Email.ToLower().Contains(query))))
                .Take(10)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            requestMessage = $"Error searching: {ex.Message}";
            requestSuccess = false;
        }
        finally
        {
            isSearching = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Selects a patient and creates a request.
    /// Author: 2402513
    /// </summary>
    private async Task SelectPatient(Guid patientId)
    {
        requestMessage = "";
        requestSuccess = false;

        var result = await UserManagementService.CreatePatientClinicianRequestAsync(patientId, clinicianId);
        
        requestMessage = result.Message;
        requestSuccess = result.Success;

        if (result.Success)
        {
            // Reload data and close modal after a short delay
            await LoadPatientData();
            await Task.Delay(1500);
            CloseRequestModal();
        }

        StateHasChanged();
    }
}
