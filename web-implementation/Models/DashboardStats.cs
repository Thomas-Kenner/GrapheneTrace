namespace GrapheneTrace.Web.Models;

/// <summary>
/// Dashboard statistics model for admin dashboard display.
/// Author: SID:2402513
/// </summary>
/// <remarks>
/// Purpose: Contains aggregated user statistics displayed on the admin dashboard.
///
/// Design Pattern: Simple data transfer object (DTO) for passing statistics
/// from service to UI component.
///
/// Fields:
/// - TotalUsers: Count of all active users in the system
/// - Clinicians: Count of users with clinician role
/// - Patients: Count of users with patient role
/// - PendingRequests: Count of pending clinician/patient requests (future use)
/// </remarks>
public class DashboardStats
{
    /// <summary>
    /// Total number of active users in the system.
    /// </summary>
    public int TotalUsers { get; set; }

    /// <summary>
    /// Number of active clinician users.
    /// </summary>
    public int Clinicians { get; set; }

    /// <summary>
    /// Number of active patient users.
    /// </summary>
    public int Patients { get; set; }

    /// <summary>
    /// Number of pending requests awaiting admin approval.
    /// </summary>
    public int PendingRequests { get; set; }
}
