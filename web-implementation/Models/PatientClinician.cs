namespace GrapheneTrace.Web.Models;

/// <summary>
/// Represents an established relationship between a patient and a clinician.
/// Author: 2402513
/// </summary>
/// <remarks>
/// Design Pattern: Many-to-many junction table with audit trail
///
/// Purpose:
/// - Tracks which clinicians are assigned to which patients
/// - Supports soft deletion (UnassignedAt) for audit compliance
/// - Allows patients to have multiple clinicians
/// - Allows clinicians to manage multiple patients
///
/// Lifecycle:
/// 1. Created when PatientClinicianRequest is approved
/// 2. Active while UnassignedAt is null
/// 3. Soft deleted by setting UnassignedAt when relationship ends
/// 4. Historical record preserved for HIPAA compliance
/// </remarks>
public class PatientClinician
{
    /// <summary>
    /// Unique identifier for the relationship.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the patient in this relationship.
    /// </summary>
    public Guid PatientId { get; set; }

    /// <summary>
    /// Navigation property to Patient user.
    /// </summary>
    public ApplicationUser? Patient { get; set; }

    /// <summary>
    /// ID of the assigned clinician.
    /// </summary>
    public Guid ClinicianId { get; set; }

    /// <summary>
    /// Navigation property to Clinician user.
    /// </summary>
    public ApplicationUser? Clinician { get; set; }

    /// <summary>
    /// When the clinician was assigned to the patient.
    /// </summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the clinician was unassigned from the patient.
    /// Null indicates active relationship.
    /// </summary>
    public DateTime? UnassignedAt { get; set; }

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
    /// Determines if this is an active relationship.
    /// </summary>
    public bool IsActive => UnassignedAt == null;

    /// <summary>
    /// Duration the clinician has been assigned (for active relationships).
    /// </summary>
    public TimeSpan? ActiveDuration => IsActive ? DateTime.UtcNow - AssignedAt : null;
}
