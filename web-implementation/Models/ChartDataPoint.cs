namespace GrapheneTrace.Web.Models;

/// <summary>
/// Represents a single data point for chart visualization.
/// Author: 2402513
/// </summary>
/// <remarks>
/// Purpose: Provides date-value pairs for rendering time-series charts
/// on the admin dashboard.
///
/// Design Pattern: Simple data transfer object (DTO) for passing chart data
/// from service to UI component.
///
/// Usage: Used by DashboardService to return user signup data grouped by date,
/// which is then consumed by Dashboard.razor for SVG chart rendering.
///
/// Fields:
/// - Date: The date for this data point (e.g., signup date)
/// - Count: The number of occurrences on this date (e.g., new user signups)
/// </remarks>
public class ChartDataPoint
{
    /// <summary>
    /// The date for this data point.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// The count value for this date (e.g., number of new users).
    /// </summary>
    public int Count { get; set; }
}
