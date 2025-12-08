using GrapheneTrace.Web.Models;
using GrapheneTrace.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Text;

namespace GrapheneTrace.Web.Components.Pages.Admin;

/// <summary>
/// Admin Dashboard Page
/// Author: 2402513
///
/// Purpose:
/// Main administrative dashboard providing system-wide overview, user statistics,
/// activity monitoring, and navigation to other admin features.
/// </summary>
public partial class Dashboard
{
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private DashboardService DashboardService { get; set; } = default!;

    // Component state variables
    private string userName = "";
    private DashboardStats stats = new();
    private List<ChartDataPoint> chartData = new();
    private List<RecentActivity> recentActivities = new();
    private int chartDays = 30;

    // Tooltip state
    private bool showTooltip = false;
    private double tooltipPixelX = 0;
    private double tooltipPixelY = 0;
    private string tooltipDate = "";
    private int tooltipCount = 0;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        userName = authState.User.Identity?.Name ?? "Unknown";

        // Load dashboard statistics, chart data, and recent activities in parallel
        stats = await DashboardService.GetDashboardStatsAsync();
        chartData = await DashboardService.GetUserSignupDataAsync(chartDays);
        recentActivities = await DashboardService.GetRecentActivitiesAsync(5);
    }

    private async Task OnTimeRangeChanged(ChangeEventArgs e)
    {
        if (e.Value != null && int.TryParse(e.Value.ToString(), out int days))
        {
            chartDays = days;
            chartData = await DashboardService.GetUserSignupDataAsync(chartDays);
            StateHasChanged();
        }
    }

    private string GetChartSvgMarkup()
    {
        var sb = new StringBuilder();

        var svgWidth = 800;
        var svgHeight = 350;
        var marginLeft = 60;
        var marginRight = 40;
        var marginTop = 30;
        var marginBottom = 50;
        var chartWidth = svgWidth - marginLeft - marginRight;
        var chartHeight = svgHeight - marginTop - marginBottom;

        sb.AppendLine($"<svg viewBox=\"0 0 {svgWidth} {svgHeight}\" class=\"chart-svg\" style=\"width: 100%; height: auto; max-height: 400px;\">");
        sb.AppendLine($"  <defs>");
        sb.AppendLine($"    <linearGradient id=\"chartGradient\" x1=\"0%\" y1=\"0%\" x2=\"0%\" y2=\"100%\">");
        sb.AppendLine($"      <stop offset=\"0%\" style=\"stop-color:#3b82f6;stop-opacity:0.3\" />");
        sb.AppendLine($"      <stop offset=\"100%\" style=\"stop-color:#3b82f6;stop-opacity:0.05\" />");
        sb.AppendLine($"    </linearGradient>");
        sb.AppendLine($"  </defs>");

        sb.AppendLine($"  <g class=\"grid-lines\" stroke=\"#e5e7eb\" stroke-width=\"0.5\" opacity=\"0.5\">");
        for (int i = 0; i <= 5; i++)
        {
            var y = marginTop + (i * chartHeight / 5);
            sb.AppendLine($"    <line x1=\"{marginLeft}\" y1=\"{y}\" x2=\"{svgWidth - marginRight}\" y2=\"{y}\" />");
        }
        sb.AppendLine($"  </g>");

        sb.AppendLine($"  <g class=\"y-axis-labels\" font-size=\"11\" fill=\"#6b7280\" text-anchor=\"end\">");
        var yLabels = GetYAxisLabels();
        foreach (var yLabel in yLabels)
        {
            var y = marginTop + yLabel.Y;
            sb.AppendLine($"    <text x=\"{marginLeft - 10}\" y=\"{y + 4}\">{yLabel.Value}</text>");
        }
        sb.AppendLine($"  </g>");

        sb.AppendLine($"  <polygon points=\"{GetChartAreaPointsScaled(marginLeft, marginTop, chartWidth, chartHeight)}\" fill=\"url(#chartGradient)\" />");
        sb.AppendLine($"  <polyline points=\"{GetChartLinePointsScaled(marginLeft, marginTop, chartWidth, chartHeight)}\" fill=\"none\" stroke=\"#3b82f6\" stroke-width=\"2\" />");

        sb.AppendLine($"  <g class=\"data-points\">");
        for (int i = 0; i < chartData.Count; i++)
        {
            var point = GetChartPointCoordinatesScaled(i, marginLeft, marginTop, chartWidth, chartHeight);
            var dataPoint = chartData[i];
            var dateStr = dataPoint.Date.ToString("MMM dd, yyyy");
            var countStr = $"{dataPoint.Count} user{(dataPoint.Count == 1 ? "" : "s")}";

            sb.AppendLine($"    <g class=\"data-point-group\" data-tooltip=\"{dateStr} - {countStr}\">");
            sb.AppendLine($"      <rect x=\"{point.X - 8}\" y=\"{point.Y - 8}\" width=\"16\" height=\"16\" fill=\"transparent\" style=\"cursor: pointer;\" />");
            sb.AppendLine($"      <circle cx=\"{point.X:F1}\" cy=\"{point.Y:F1}\" r=\"5\" fill=\"#3b82f6\" stroke=\"white\" stroke-width=\"2\" class=\"chart-data-point\" />");
            sb.AppendLine($"    </g>");
        }
        sb.AppendLine($"  </g>");

        sb.AppendLine($"  <g class=\"x-axis-labels\" font-size=\"10\" fill=\"#6b7280\" text-anchor=\"middle\">");
        var xLabels = GetXAxisLabels();
        foreach (var xLabel in xLabels)
        {
            var scaledX = marginLeft + (xLabel.X * chartWidth / 460.0);
            sb.AppendLine($"    <text x=\"{scaledX:F1}\" y=\"{svgHeight - marginBottom + 20}\">{xLabel.Date}</text>");
        }
        sb.AppendLine($"  </g>");

        sb.AppendLine($"  <line x1=\"{marginLeft}\" y1=\"{marginTop}\" x2=\"{marginLeft}\" y2=\"{svgHeight - marginBottom}\" stroke=\"#374151\" stroke-width=\"1.5\" />");
        sb.AppendLine($"  <line x1=\"{marginLeft}\" y1=\"{svgHeight - marginBottom}\" x2=\"{svgWidth - marginRight}\" y2=\"{svgHeight - marginBottom}\" stroke=\"#374151\" stroke-width=\"1.5\" />");

        sb.AppendLine($"</svg>");

        return sb.ToString();
    }

    private List<(double Y, int Value)> GetYAxisLabels()
    {
        var labels = new List<(double Y, int Value)>();

        if (chartData == null || !chartData.Any()) return labels;

        var maxValue = chartData.Max(d => d.Count);
        if (maxValue == 0) maxValue = 1;

        for (int i = 0; i <= 5; i++)
        {
            var value = maxValue - (i * maxValue / 5);
            var y = 30 + (i * 30) + 3;
            labels.Add((y, value));
        }

        return labels;
    }

    private List<(double X, string Date)> GetXAxisLabels()
    {
        var labels = new List<(double X, string Date)>();

        if (chartData == null || !chartData.Any()) return labels;

        var interval = Math.Max(chartData.Count / 6, 1);

        for (int i = 0; i < chartData.Count; i += interval)
        {
            var x = 60 + (i * 460.0 / (chartData.Count - 1));
            var dateLabel = chartData[i].Date.ToString("MMM dd");
            labels.Add((x, dateLabel));
        }

        return labels;
    }

    private string GetChartLinePoints()
    {
        if (chartData == null || !chartData.Any()) return "";

        var maxValue = chartData.Max(d => d.Count);
        if (maxValue == 0) maxValue = 1;

        var points = new List<string>();
        var width = 550.0;
        var height = 250.0;
        var padding = 60.0;
        var paddingTop = 30.0;
        var paddingBottom = 60.0;

        for (int i = 0; i < chartData.Count; i++)
        {
            var x = padding + (i * (width - padding - 30) / (chartData.Count - 1));
            var y = paddingTop + ((maxValue - chartData[i].Count) / (double)maxValue) * (height - paddingTop - paddingBottom);
            points.Add($"{x:F1},{y:F1}");
        }

        return string.Join(" ", points);
    }

    private string GetChartAreaPoints()
    {
        var linePoints = GetChartLinePoints();
        if (string.IsNullOrEmpty(linePoints)) return "";

        var width = 550.0;
        var height = 250.0;
        var padding = 60.0;
        var paddingBottom = 60.0;

        var bottomRight = $"{width - padding + 60},{height - paddingBottom}";
        var bottomLeft = $"{padding},{height - paddingBottom}";

        return $"{linePoints} {bottomRight} {bottomLeft}";
    }

    private (double X, double Y) GetChartPointCoordinates(int index)
    {
        if (chartData == null || !chartData.Any() || index >= chartData.Count)
            return (0, 0);

        var maxValue = chartData.Max(d => d.Count);
        if (maxValue == 0) maxValue = 1;

        var width = 550.0;
        var height = 250.0;
        var padding = 60.0;
        var paddingTop = 30.0;
        var paddingBottom = 60.0;

        var x = padding + (index * (width - padding - 30) / (chartData.Count - 1));
        var y = paddingTop + ((maxValue - chartData[index].Count) / (double)maxValue) * (height - paddingTop - paddingBottom);

        return (x, y);
    }

    private string GetChartLinePointsScaled(double marginLeft, double marginTop, double chartWidth, double chartHeight)
    {
        if (chartData == null || !chartData.Any()) return "";

        var maxValue = chartData.Max(d => d.Count);
        if (maxValue == 0) maxValue = 1;

        var points = new List<string>();

        for (int i = 0; i < chartData.Count; i++)
        {
            var x = marginLeft + (i * chartWidth / (chartData.Count - 1));
            var y = marginTop + ((maxValue - chartData[i].Count) / (double)maxValue) * chartHeight;
            points.Add($"{x:F1},{y:F1}");
        }

        return string.Join(" ", points);
    }

    private string GetChartAreaPointsScaled(double marginLeft, double marginTop, double chartWidth, double chartHeight)
    {
        var linePoints = GetChartLinePointsScaled(marginLeft, marginTop, chartWidth, chartHeight);
        if (string.IsNullOrEmpty(linePoints)) return "";

        var bottomRight = $"{marginLeft + chartWidth},{marginTop + chartHeight}";
        var bottomLeft = $"{marginLeft},{marginTop + chartHeight}";

        return $"{linePoints} {bottomRight} {bottomLeft}";
    }

    private (double X, double Y) GetChartPointCoordinatesScaled(int index, double marginLeft, double marginTop, double chartWidth, double chartHeight)
    {
        if (chartData == null || !chartData.Any() || index >= chartData.Count)
            return (0, 0);

        var maxValue = chartData.Max(d => d.Count);
        if (maxValue == 0) maxValue = 1;

        var x = marginLeft + (index * chartWidth / (chartData.Count - 1));
        var y = marginTop + ((maxValue - chartData[index].Count) / (double)maxValue) * chartHeight;

        return (x, y);
    }

    private string GetChartPolylinePoints()
    {
        if (chartData == null || !chartData.Any()) return "";

        var maxValue = chartData.Max(d => d.Count);
        if (maxValue == 0) maxValue = 1;

        var points = new List<string>();

        for (int i = 0; i < chartData.Count; i++)
        {
            var x = 5 + (i * 90.0 / (chartData.Count - 1));
            var y = 85 - ((chartData[i].Count / (double)maxValue) * 70);
            points.Add($"{x:F1},{y:F1}");
        }

        return string.Join(" ", points);
    }

    private string GetChartPolygonPoints()
    {
        var linePoints = GetChartPolylinePoints();
        if (string.IsNullOrEmpty(linePoints)) return "";

        return $"{linePoints} 95,85 5,85";
    }

    private (double X, double Y) GetChartPointPercent(int index)
    {
        if (chartData == null || !chartData.Any() || index >= chartData.Count)
            return (0, 0);

        var maxValue = chartData.Max(d => d.Count);
        if (maxValue == 0) maxValue = 1;

        var x = 5 + (index * 90.0 / (chartData.Count - 1));
        var y = 85 - ((chartData[index].Count / (double)maxValue) * 70);

        return (x, y);
    }

    private void ShowTooltip(int dataIndex)
    {
        if (dataIndex >= chartData.Count) return;

        var dataPoint = chartData[dataIndex];
        var (svgX, svgY) = GetChartPointCoordinatesModern(dataIndex);

        tooltipDate = dataPoint.Date.ToString("MMM dd, yyyy");
        tooltipCount = dataPoint.Count;

        tooltipPixelX = svgX;
        tooltipPixelY = svgY - 40;

        showTooltip = true;
        StateHasChanged();
    }

    private void HideTooltip()
    {
        showTooltip = false;
        StateHasChanged();
    }

    private string GetChartPolylinePointsModern()
    {
        if (chartData == null || !chartData.Any()) return "";

        var maxValue = chartData.Max(d => d.Count);
        if (maxValue == 0) maxValue = 1;

        var points = new List<string>();
        var startX = 80;
        var endX = 950;
        var startY = 40;
        var endY = 340;
        var chartWidth = endX - startX;
        var chartHeight = endY - startY;

        for (int i = 0; i < chartData.Count; i++)
        {
            var x = startX + (i * chartWidth / (chartData.Count - 1));
            var y = endY - ((chartData[i].Count / (double)maxValue) * chartHeight);
            points.Add($"{x:F1},{y:F1}");
        }

        return string.Join(" ", points);
    }

    private string GetChartPolygonPointsModern()
    {
        var linePoints = GetChartPolylinePointsModern();
        if (string.IsNullOrEmpty(linePoints)) return "";

        return $"{linePoints} 950,340 80,340";
    }

    private (double X, double Y) GetChartPointCoordinatesModern(int index)
    {
        if (chartData == null || !chartData.Any() || index >= chartData.Count)
            return (0, 0);

        var maxValue = chartData.Max(d => d.Count);
        if (maxValue == 0) maxValue = 1;

        var startX = 80;
        var endX = 950;
        var startY = 40;
        var endY = 340;
        var chartWidth = endX - startX;
        var chartHeight = endY - startY;

        var x = startX + (index * chartWidth / (chartData.Count - 1));
        var y = endY - ((chartData[index].Count / (double)maxValue) * chartHeight);

        return (x, y);
    }

    private string GetChartAxisLabels()
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!-- Y-axis label values -->");
        sb.AppendLine("<g font-size=\"11\" fill=\"#94a3b8\" text-anchor=\"end\" font-weight=\"500\">");

        if (chartData != null && chartData.Any())
        {
            var maxVal = chartData.Max(d => d.Count);
            if (maxVal == 0) maxVal = 1;

            for (int i = 0; i <= 5; i++)
            {
                var val = maxVal - (i * maxVal / 5);
                var yPos = 60 + i * 56 + 4;
                sb.AppendLine($"  <text x=\"70\" y=\"{yPos}\">{val}</text>");
            }
        }

        sb.AppendLine("</g>");

        sb.AppendLine("<!-- X-axis labels (dates) -->");
        sb.AppendLine("<g font-size=\"11\" fill=\"#94a3b8\" text-anchor=\"middle\" font-weight=\"500\">");

        if (chartData != null && chartData.Any())
        {
            var interval = Math.Max(chartData.Count / 6, 1);
            for (int i = 0; i < chartData.Count; i += interval)
            {
                if (i < chartData.Count)
                {
                    var xPos = 80 + (i * 870.0 / (chartData.Count - 1));
                    var dateStr = chartData[i].Date.ToString("MMM dd");
                    sb.AppendLine($"  <text x=\"{xPos:F1}\" y=\"365\">{dateStr}</text>");
                }
            }
        }

        sb.AppendLine("</g>");

        return sb.ToString();
    }
}
