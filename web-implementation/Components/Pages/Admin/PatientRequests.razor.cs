using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GrapheneTrace.Web.Components.Pages.Admin;

/// <summary>
/// Admin page for approving/denying patient-clinician requests.
/// Author: 2402513
/// </summary>
public partial class PatientRequests
{
    [Inject] private ApplicationDbContext DbContext { get; set; } = default!;

    private bool isLoading = true;
    private List<PatientClinicianRequest> requests = new();
    private string statusFilter = "pending";
    private string message = "";
    private bool isSuccess = false;

    // Reject modal state
    private bool showRejectModal = false;
    private Guid? requestToReject = null;
    private string rejectReason = "";

    protected override async Task OnInitializedAsync()
    {
        await LoadRequests();
        isLoading = false;
    }

    /// <summary>
    /// Loads patient-clinician requests based on current filter.
    /// Author: 2402513
    /// </summary>
    private async Task LoadRequests()
    {
        try
        {
            var query = DbContext.PatientClinicianRequests
                .Include(r => r.Patient)
                .Include(r => r.Clinician)
                .AsQueryable();

            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(r => r.Status == statusFilter);
            }

            requests = await query
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            message = $"Error loading requests: {ex.Message}";
            isSuccess = false;
        }
    }

    /// <summary>
    /// Approves a patient-clinician request.
    /// Author: 2402513
    /// </summary>
    private async Task ApproveRequest(Guid requestId)
    {
        try
        {
            var request = await DbContext.PatientClinicianRequests
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
            {
                message = "Request not found";
                isSuccess = false;
                return;
            }

            if (request.Status != "pending")
            {
                message = "Request is no longer pending";
                isSuccess = false;
                return;
            }

            // Update request status
            request.Status = "approved";
            request.RespondedAt = DateTime.UtcNow;

            // Check if assignment already exists
            var existingAssignment = await DbContext.PatientClinicians
                .FirstOrDefaultAsync(pc => pc.PatientId == request.PatientId
                    && pc.ClinicianId == request.ClinicianId
                    && pc.UnassignedAt == null);

            if (existingAssignment == null)
            {
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

                DbContext.PatientClinicians.Add(patientClinician);
            }

            DbContext.PatientClinicianRequests.Update(request);
            await DbContext.SaveChangesAsync();

            message = "Request approved successfully";
            isSuccess = true;
            await LoadRequests();
            
            // Clear message after 3 seconds
            await Task.Delay(3000);
            message = "";
            StateHasChanged();
        }
        catch (Exception ex)
        {
            message = $"Error approving request: {ex.Message}";
            isSuccess = false;
        }
    }

    /// <summary>
    /// Shows the reject modal for a request.
    /// Author: 2402513
    /// </summary>
    private void ShowRejectModal(Guid requestId)
    {
        requestToReject = requestId;
        rejectReason = "";
        showRejectModal = true;
    }

    /// <summary>
    /// Closes the reject modal.
    /// Author: 2402513
    /// </summary>
    private void CloseRejectModal()
    {
        showRejectModal = false;
        requestToReject = null;
        rejectReason = "";
    }

    /// <summary>
    /// Rejects a patient-clinician request with a reason.
    /// Author: 2402513
    /// </summary>
    private async Task RejectRequest()
    {
        if (!requestToReject.HasValue)
            return;

        try
        {
            var request = await DbContext.PatientClinicianRequests
                .FirstOrDefaultAsync(r => r.Id == requestToReject.Value);

            if (request == null)
            {
                message = "Request not found";
                isSuccess = false;
                CloseRejectModal();
                return;
            }

            if (request.Status != "pending")
            {
                message = "Request is no longer pending";
                isSuccess = false;
                CloseRejectModal();
                return;
            }

            request.Status = "rejected";
            request.RespondedAt = DateTime.UtcNow;
            request.ResponseReason = string.IsNullOrWhiteSpace(rejectReason) 
                ? "Request rejected by administrator" 
                : rejectReason;

            DbContext.PatientClinicianRequests.Update(request);
            await DbContext.SaveChangesAsync();

            message = "Request rejected successfully";
            isSuccess = true;
            CloseRejectModal();
            await LoadRequests();
            
            // Clear message after 3 seconds
            await Task.Delay(3000);
            message = "";
            StateHasChanged();
        }
        catch (Exception ex)
        {
            message = $"Error rejecting request: {ex.Message}";
            isSuccess = false;
        }
    }
}

