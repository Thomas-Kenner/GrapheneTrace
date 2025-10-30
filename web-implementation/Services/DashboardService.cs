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

    /// <summary>
    /// Retrieves user signup data grouped by date for the specified number of days.
    /// Author: 2402513
    /// </summary>
    /// <param name="days">Number of days to look back (default: 30)</param>
    /// <returns>List of ChartDataPoint containing date and signup count</returns>
    /// <remarks>
    /// Purpose: Provides time-series data for the "New Users" chart on admin dashboard.
    ///
    /// Logic:
    /// 1. Calculate start date (today minus specified days)
    /// 2. Query all users created on or after start date
    /// 3. Filter active users only (DeactivatedAt == null)
    /// 4. Group by date (using Date component of CreatedAt timestamp)
    /// 5. Count signups per date
    /// 6. Fill in missing dates with zero counts for continuous graph
    /// 7. Return ordered list from oldest to newest
    ///
    /// Why fill missing dates:
    /// - Creates smooth graph visualization
    /// - Prevents gaps that could mislead viewers
    /// - Shows true timeline even with zero-signup days
    ///
    /// Performance: Single database query with in-memory grouping and date generation
    /// </remarks>
    public async Task<List<ChartDataPoint>> GetUserSignupDataAsync(int days = 30)
    {
        // Calculate the start date
        var startDate = DateTime.UtcNow.Date.AddDays(-days);

        // Get all users created within the time period
        var allUsers = await Task.FromResult(_userManager.Users.ToList());

        // Filter users: created after start date and not deactivated
        var recentUsers = allUsers
            .Where(u => u.CreatedAt >= startDate && u.DeactivatedAt == null)
            .ToList();

        // Group by date and count
        var signupsByDate = recentUsers
            .GroupBy(u => u.CreatedAt.Date)
            .Select(g => new ChartDataPoint
            {
                Date = g.Key,
                Count = g.Count()
            })
            .ToDictionary(dp => dp.Date, dp => dp.Count);

        // Fill in missing dates with zero counts for continuous graph
        var result = new List<ChartDataPoint>();
        for (int i = 0; i <= days; i++)
        {
            var date = startDate.AddDays(i);
            result.Add(new ChartDataPoint
            {
                Date = date,
                Count = signupsByDate.ContainsKey(date) ? signupsByDate[date] : 0
            });
        }

        return result.OrderBy(dp => dp.Date).ToList();
    }

    /// <summary>
    /// Retrieves the most recent user account creations for the activity feed.
    /// Author: 2402513
    /// </summary>
    /// <param name="limit">Maximum number of recent activities to return (default: 10)</param>
    /// <returns>List of recent user account creations ordered by most recent first</returns>
    /// <remarks>
    /// Purpose: Provides activity feed data for the "Recent Activity" section on dashboard.
    ///
    /// Logic:
    /// 1. Query all users from UserManager
    /// 2. Filter active users only (DeactivatedAt == null)
    /// 3. Order by CreatedAt descending (most recent first)
    /// 4. Take top N results
    /// 5. Return with user information for display
    ///
    /// Why filter active users: Shows actual recent account creations, not deactivations
    /// </remarks>
    public async Task<List<RecentActivity>> GetRecentActivitiesAsync(int limit = 10)
    {
        var allUsers = await Task.FromResult(_userManager.Users.ToList());

        var recentActivities = allUsers
            .Where(u => u.DeactivatedAt == null)
            .OrderByDescending(u => u.CreatedAt)
            .Take(limit)
            .Select(u => new RecentActivity
            {
                UserName = u.FullName,
                CreatedAt = u.CreatedAt,
                Email = u.Email ?? ""
            })
            .ToList();

        return recentActivities;
    }
}
