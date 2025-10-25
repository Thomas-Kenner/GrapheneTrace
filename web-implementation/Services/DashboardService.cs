using GrapheneTrace.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace GrapheneTrace.Web.Services;

/// <summary>
/// Service for retrieving admin dashboard statistics and data.
/// Author: SID:2402513
/// </summary>
/// <remarks>
/// Purpose: Aggregates user statistics for display on the admin dashboard.
///
/// Dependencies:
/// - UserManager<ApplicationUser>: ASP.NET Core Identity for querying users
///
/// Design Pattern: Service layer encapsulates business logic for dashboard data retrieval.
/// This allows for reuse across multiple components and testability.
///
/// Methods:
/// - GetDashboardStatsAsync(): Returns aggregated user statistics
///   - Counts total active users (where DeactivatedAt is null)
///   - Counts clinicians (where UserType == "clinician")
///   - Counts patients (where UserType == "patient")
///   - Returns pending requests (currently hardcoded as 0, can be expanded)
/// </remarks>
public class DashboardService
{
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>
    /// Initializes the DashboardService with UserManager dependency.
    /// Author: SID:2402513
    /// </summary>
    public DashboardService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    /// <summary>
    /// Retrieves aggregated dashboard statistics for admin dashboard display.
    /// Author: SID:2402513
    /// </summary>
    /// <returns>DashboardStats containing user counts by role and type</returns>
    /// <remarks>
    /// Logic:
    /// 1. Query all users from UserManager
    /// 2. Filter active users (where DeactivatedAt == null)
    /// 3. Count total users
    /// 4. Count by UserType: "clinician" and "patient"
    /// 5. PendingRequests is currently static (0) - can be enhanced with request tracking table
    ///
    /// Performance: Executes single query to UserManager with in-memory filtering
    /// </remarks>
    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        // Get all users from the database
        var allUsers = await Task.FromResult(_userManager.Users.ToList());

        // Filter active users (not deactivated)
        var activeUsers = allUsers.Where(u => u.DeactivatedAt == null).ToList();

        // Count users by type
        var clinicians = activeUsers.Count(u => u.UserType == "clinician");
        var patients = activeUsers.Count(u => u.UserType == "patient");

        return new DashboardStats
        {
            TotalUsers = activeUsers.Count,
            Clinicians = clinicians,
            Patients = patients,
            PendingRequests = 0 // Can be expanded with future request tracking
        };
    }
}
