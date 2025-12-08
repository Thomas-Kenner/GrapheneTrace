using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GrapheneTrace.Web.Components.Pages.Clinician;

/// <summary>
/// Clinician Patient List Page Code-Behind
/// Author: 2402513
///
/// Purpose:
/// Manages the data loading and filtering logic for the Patient List page.
/// Calculates the "Last Day Peak Pressure Breached" for each patient by
/// finding the most recent date where peak pressure exceeded the patient's
/// high threshold from PatientSettings.
/// </summary>
public partial class PatientList
{
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private ApplicationDbContext DbContext { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private bool isLoading = true;
    private Guid clinicianId = Guid.Empty;
    private List<PatientInfo> allPatients = new();
    private List<PatientInfo> filteredPatients = new();
    private string searchQuery = "";

    /// <summary>
    /// Patient information with calculated breach date.
    /// Author: 2402513
    /// </summary>
    private class PatientInfo
    {
        public PatientClinician? PatientClinician { get; set; }
        public ApplicationUser? Patient { get; set; }
        public DateTime? LastBreachDate { get; set; }
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadClinicianId();
        await LoadPatientData();
        isLoading = false;
    }

    /// <summary>
    /// Loads the clinician ID from the authenticated user.
    /// Author: 2402513
    /// </summary>
    private async Task LoadClinicianId()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        // Try multiple methods to get clinician ID
        var clinicianIdClaim = user.FindFirst("sub")?.Value;
        if (Guid.TryParse(clinicianIdClaim, out var id))
        {
            clinicianId = id;
            return;
        }

        var username = user.Identity?.Name;
        if (!string.IsNullOrEmpty(username))
        {
            var clinician = await DbContext.Users
                .FirstOrDefaultAsync(u => u.UserName == username && u.UserType == "clinician");
            if (clinician != null)
            {
                clinicianId = clinician.Id;
                return;
            }
        }

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

    /// <summary>
    /// Loads all assigned patients and calculates their last threshold breach dates.
    /// Author: 2402513
    /// </summary>
    /// <remarks>
    /// For each patient, this method:
    /// 1. Gets the patient's HighPressureThreshold from PatientSettings
    /// 2. Queries PatientSessionData and PatientSnapshotData to find sessions
    ///    where PeakSnapshotPressure exceeded the threshold
    /// 3. Finds the most recent date of such a breach
    /// </remarks>
    private async Task LoadPatientData()
    {
        if (clinicianId == Guid.Empty)
        {
            allPatients = new List<PatientInfo>();
            filteredPatients = new List<PatientInfo>();
            return;
        }

        // Load all assigned patients
        var patientClinicians = await DbContext.PatientClinicians
            .Where(pc => pc.ClinicianId == clinicianId && pc.UnassignedAt == null)
            .Include(pc => pc.Patient)
            .OrderBy(pc => pc.Patient!.LastName)
            .ThenBy(pc => pc.Patient!.FirstName)
            .ToListAsync();

        allPatients = new List<PatientInfo>();

        // Calculate last breach date for each patient
        foreach (var pc in patientClinicians)
        {
            if (pc.Patient == null || pc.PatientId == Guid.Empty)
                continue;

            var patientInfo = new PatientInfo
            {
                PatientClinician = pc,
                Patient = pc.Patient
            };

            // Get patient's high threshold
            var patientSettings = await DbContext.PatientSettings
                .FirstOrDefaultAsync(ps => ps.UserId == pc.PatientId);

            var threshold = patientSettings?.HighPressureThreshold ?? 200; // Default to 200 if no settings

            // Find most recent session where any snapshot exceeded threshold
            var lastBreachSession = await DbContext.PatientSessionDatas
                .Where(s => s.PatientId == pc.PatientId)
                .Join(
                    DbContext.PatientSnapshotDatas,
                    s => s.SessionId,
                    sn => sn.SessionId,
                    (s, sn) => new { Session = s, Snapshot = sn })
                .Where(x => x.Snapshot.PeakSnapshotPressure.HasValue && 
                           x.Snapshot.PeakSnapshotPressure.Value > threshold)
                .OrderByDescending(x => x.Session.Start)
                .Select(x => x.Session)
                .FirstOrDefaultAsync();

            if (lastBreachSession != null)
            {
                patientInfo.LastBreachDate = lastBreachSession.Start.Date;
            }

            allPatients.Add(patientInfo);
        }

        // Apply initial filter
        FilterPatients();
    }

    /// <summary>
    /// Filters the patient list based on the search query.
    /// Author: 2402513
    /// </summary>
    /// <remarks>
    /// Performs real-time filtering as the user types in the search box.
    /// Searches by patient first name, last name, and email.
    /// </remarks>
    private void FilterPatients()
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            filteredPatients = allPatients.ToList();
            return;
        }

        var query = searchQuery.ToLowerInvariant();
        filteredPatients = allPatients
            .Where(p => 
                (p.Patient?.FirstName?.ToLowerInvariant().Contains(query) ?? false) ||
                (p.Patient?.LastName?.ToLowerInvariant().Contains(query) ?? false) ||
                (p.Patient?.Email?.ToLowerInvariant().Contains(query) ?? false))
            .ToList();
    }

    /// <summary>
    /// Handles search input changes and filters the patient list in real-time.
    /// Author: 2402513
    /// </summary>
    private void OnSearchInput(ChangeEventArgs e)
    {
        searchQuery = e.Value?.ToString() ?? "";
        FilterPatients();
    }

    /// <summary>
    /// Navigates to the Patient Details page for the selected patient.
    /// Author: 2402513
    /// </summary>
    /// <param name="patientId">The ID of the patient to view.</param>
    private void ViewPatientDetails(Guid patientId)
    {
        if (patientId != Guid.Empty)
        {
            Navigation.NavigateTo($"/clinician/patient-details/{patientId}");
        }
    }
}

