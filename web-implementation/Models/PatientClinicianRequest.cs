namespace GrapheneTrace.Web.Models;

/// <summary>
/// Represents a request for clinician assignment.
/// Author: 2402513
/// </summary>
/// <remarks>
/// Design Pattern: Request approval workflow
///
/// Status Flow:
/// 1. pending - Request created, awaiting clinician response
/// 2. approved - Clinician accepted, PatientClinician record created
/// 3. rejected - Clinician declined, request closed
///
/// Workflow:
/// - Patient initiates request to assign a clinician
/// - Clinician receives notification
/// - Clinician approves or rejects
/// - If approved, create corresponding PatientClinician record
/// - If rejected, RespondedAt and ResponseReason are populated
/// </remarks>
public class PatientClinicianRequest
{
    /// <summary>
    /// Unique identifier for the request.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the patient requesting clinician assignment.
    /// </summary>
    public Guid PatientId { get; set; }

    /// <summary>
    /// Navigation property to Patient user.
    /// </summary>
    public ApplicationUser? Patient { get; set; }

    /// <summary>
    /// ID of the clinician being requested.
    /// </summary>
    public Guid ClinicianId { get; set; }

    /// <summary>
    /// Navigation property to Clinician user.
    /// </summary>
    public ApplicationUser? Clinician { get; set; }

    /// <summary>
    /// Current status of the request.
    /// Valid values: pending, approved, rejected
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// When the request was created.
    /// </summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the clinician responded (approved or rejected).
    /// Null while pending.
    /// </summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>
    /// Reason for rejection (if rejected).
    /// Null for pending and approved requests.
    /// </summary>
    public string? ResponseReason { get; set; }

    /// <summary>
    /// Record creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Record last update timestamp.
    /// Updated automatically by database trigger.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Determines if request is still awaiting response.
    /// </summary>
    public bool IsPending => Status == "pending" && RespondedAt == null;

    /// <summary>
    /// Determines if request was approved.
    /// </summary>
    public bool IsApproved => Status == "approved" && RespondedAt != null;

    /// <summary>
    /// Determines if request was rejected.
    /// </summary>
    public bool IsRejected => Status == "rejected" && RespondedAt != null;
}
