using GrapheneTrace.Web.Models;
using GrapheneTrace.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GrapheneTrace.Web.Components.Pages.Admin;

/// <summary>
/// Admin Patient-Clinician Assignment Page
/// Author: SID:2402513
///
/// Purpose:
/// Allows administrators to directly assign patients to clinicians and manage
/// existing assignments. Admin assignments bypass the approval workflow.
/// </summary>
public partial class PatientClinicianAssignment
{
    [Inject] private UserManagementService UserManagementService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private List<PatientClinician>? assignments;
    private List<PatientClinician>? filteredAssignments;
    private List<ApplicationUser>? patients;
    private List<ApplicationUser>? clinicians;
    private bool isLoading = true;
    private bool showAssignModal = false;
    private Guid selectedPatientId = Guid.Empty;
    private Guid selectedClinicianId = Guid.Empty;
    private string assignMessage = "";
    private bool assignSuccess = false;
    private bool showActiveOnly = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    /// <summary>
    /// Loads all data needed for the page: assignments, patients, and clinicians.
    /// Author: 2402513
    /// </summary>
    /// <remarks>
    /// Data Loading Strategy:
    /// - Loads all assignments (active and inactive) for complete history
    /// - Filters assignments based on showActiveOnly flag
    /// - Loads all active patients and clinicians for dropdown selection
    /// - Sets loading state to show loading indicator during async operations
    /// </remarks>
    private async Task LoadData()
    {
        isLoading = true;
        
        // Author: 2402513
        // Load all assignments (active and inactive) for admin view
        assignments = await UserManagementService.GetAllPatientClinicianAssignmentsAsync(activeOnly: false);
        FilterAssignments();

        // Author: 2402513
        // Load patients and clinicians for assignment dropdowns
        // Only show active (non-deactivated) users
        var allUsers = await UserManagementService.GetAllUsersAsync();
        patients = allUsers.Where(u => u.UserType == "patient" && u.DeactivatedAt == null).ToList();
        clinicians = allUsers.Where(u => u.UserType == "clinician" && u.DeactivatedAt == null).ToList();

        isLoading = false;
    }

    /// <summary>
    /// Filters assignments based on active/inactive status.
    /// Author: 2402513
    /// </summary>
    /// <remarks>
    /// Filtering Logic:
    /// - If showActiveOnly is true, only shows assignments where UnassignedAt == null
    /// - If showActiveOnly is false, shows all assignments (active and inactive)
    /// - Used to toggle between viewing current assignments vs. full history
    /// </remarks>
    private void FilterAssignments()
    {
        if (assignments == null)
        {
            filteredAssignments = new List<PatientClinician>();
            return;
        }

        if (showActiveOnly)
        {
            filteredAssignments = assignments.Where(a => a.UnassignedAt == null).ToList();
        }
        else
        {
            filteredAssignments = assignments.ToList();
        }
    }

    /// <summary>
    /// Toggles between showing active-only vs. all assignments.
    /// Author: 2402513
    /// </summary>
    /// <remarks>
    /// Note: Currently not exposed in UI but available for future enhancement.
    /// Could add a toggle button to switch between active and all assignments view.
    /// </remarks>
    private void ToggleFilter()
    {
        showActiveOnly = !showActiveOnly;
        FilterAssignments();
    }

    /// <summary>
    /// Assigns a patient to a clinician via admin direct assignment.
    /// Author: 2402513
    /// </summary>
    /// <remarks>
    /// Assignment Flow:
    /// 1. Validates that both patient and clinician are selected
    /// 2. Calls UserManagementService to create PatientClinician record
    /// 3. On success: clears form, reloads data, closes modal after 2 seconds
    /// 4. On failure: displays error message in modal
    ///
    /// Admin Direct Assignment:
    /// - Bypasses normal PatientClinicianRequest approval workflow
    /// - Creates assignment immediately without clinician approval
    /// - Used for administrative assignments and corrections
    /// </remarks>
    private async Task AssignPatient()
    {
        if (selectedPatientId == Guid.Empty || selectedClinicianId == Guid.Empty)
        {
            assignMessage = "Please select both a patient and a clinician";
            assignSuccess = false;
            return;
        }

        // Author: 2402513
        // Call service to create PatientClinician assignment record
        var (success, message) = await UserManagementService.AssignPatientToClinicianAsync(
            selectedPatientId, 
            selectedClinicianId);

        assignSuccess = success;
        assignMessage = message;

        if (success)
        {
            // Author: 2402513
            // Clear form, reload data, and close modal after brief delay
            selectedPatientId = Guid.Empty;
            selectedClinicianId = Guid.Empty;
            await LoadData();
            await Task.Delay(2000);  // Show success message briefly
            showAssignModal = false;
            assignMessage = "";
        }
    }

    /// <summary>
    /// Unassigns a patient from a clinician (soft delete).
    /// Author: 2402513
    /// </summary>
    /// <param name="patientId">ID of the patient to unassign</param>
    /// <param name="clinicianId">ID of the clinician to unassign from</param>
    /// <remarks>
    /// Unassignment Flow:
    /// 1. Shows JavaScript confirmation dialog to prevent accidental unassignment
    /// 2. Calls UserManagementService to soft-delete assignment (sets UnassignedAt)
    /// 3. On success: reloads data to reflect changes
    /// 4. On failure: displays error message
    ///
    /// Soft Delete:
    /// - Sets UnassignedAt timestamp instead of deleting record
    /// - Preserves historical data for audit trail and HIPAA compliance
    /// - Assignment becomes inactive but remains in database
    /// </remarks>
    private async Task UnassignPatient(Guid patientId, Guid clinicianId)
    {
        // Author: 2402513
        // Confirm action with user before proceeding
        var confirmed = await JS.InvokeAsync<bool>("confirm", "Are you sure you want to unassign this patient from the clinician?");
        if (!confirmed)
            return;

        // Author: 2402513
        // Call service to soft-delete assignment
        var (success, message) = await UserManagementService.UnassignPatientFromClinicianAsync(
            patientId, 
            clinicianId);

        if (success)
        {
            await LoadData();
        }
        else
        {
            assignMessage = message;
            assignSuccess = false;
        }
    }
}

