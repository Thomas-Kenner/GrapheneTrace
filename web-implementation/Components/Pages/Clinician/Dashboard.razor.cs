using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
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

    private bool isPopupVisible = false;
    private bool isLoading = true;
    private string userName = "";
    private Guid clinicianId = Guid.Empty;
    private List<PatientClinician> approvedPatients = new();
    private List<PatientClinicianRequest> pendingRequests = new();
    private Guid? selectedPatientId = null;

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
}
