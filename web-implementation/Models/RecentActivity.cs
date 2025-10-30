namespace GrapheneTrace.Web.Models;

/// <summary>
/// Represents a recent activity entry for display in the activity feed.
/// Author: 2402513
/// </summary>
/// <remarks>
/// Purpose: Data transfer object for displaying recent user account creations
/// in the admin dashboard activity feed.
///
/// Properties:
/// - UserName: Full name of the user who created the account (FirstName + LastName)
/// - CreatedAt: DateTime when the account was created
/// - Email: User's email address for additional context
///
/// Why as a separate model:
/// - Separates presentation data from domain entities
/// - Only includes fields needed for activity display
/// - Allows future expansion without modifying ApplicationUser
/// </remarks>
public class RecentActivity
{
    /// <summary>
    /// Full name of the user (FirstName + LastName).
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Email address of the user.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the user account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Calculates a relative time string for display.
    /// Returns "X hours ago" if created within 24 hours,
    /// otherwise returns formatted date "MMM dd, yyyy".
    /// </summary>
    /// <remarks>
    /// Purpose: Provides user-friendly time display for activity feed.
    ///
    /// Logic:
    /// 1. Calculate time difference from CreatedAt to current UTC time
    /// 2. If less than 24 hours: return "X hours ago"
    /// 3. If 24 hours or more: return formatted date "MMM dd, yyyy"
    /// 4. Special case: if less than 1 hour, show "just now"
    /// </remarks>
    public string GetRelativeTime()
    {
        var now = DateTime.UtcNow;
        var timeSpan = now - CreatedAt;

        if (timeSpan.TotalMinutes < 1)
            return "just now";

        if (timeSpan.TotalHours < 1)
            return $"{(int)timeSpan.TotalMinutes}m ago";

        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours}h ago";

        return CreatedAt.ToString("MMM dd, yyyy");
    }
}
