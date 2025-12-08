using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GrapheneTrace.Web.Services;

/// <summary>
/// Service for managing patient pressure threshold settings.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Implements Story #9: Patient pressure threshold configuration.
///
/// Purpose: Provides server-side data access for patient settings.
/// This service is used by Blazor Server components to avoid HttpClient
/// authentication issues.
///
/// Design Rationale:
/// - Blazor Server components run on the server and should access data directly
/// - Using HttpClient in Blazor Server requires complex cookie forwarding
/// - Direct database access via service is simpler, faster, and more secure
/// - Follows the same pattern as UserManagementService and DashboardService
///
/// Operations:
/// - GetSettingsAsync(userId): Retrieve settings, auto-create defaults if missing
/// - UpdateSettingsAsync(userId, low, high): Update thresholds with validation
/// </remarks>
public class PatientSettingsService
{
    private readonly ApplicationDbContext _context;
    private readonly PressureThresholdsConfig _thresholdsConfig;
    private readonly ILogger<PatientSettingsService> _logger;

    public PatientSettingsService(
        ApplicationDbContext context,
        PressureThresholdsConfig thresholdsConfig,
        ILogger<PatientSettingsService> logger)
    {
        _context = context;
        _thresholdsConfig = thresholdsConfig;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves patient settings for the specified user.
    /// Creates default settings if none exist.
    /// </summary>
    /// <param name="userId">The patient's user ID</param>
    /// <returns>Patient settings with thresholds and last update time</returns>
    public async Task<PatientSettings> GetSettingsAsync(Guid userId)
    {
        var settings = await _context.PatientSettings
            .FirstOrDefaultAsync(ps => ps.UserId == userId);

        // Auto-create default settings if not found (uses config values)
        if (settings == null)
        {
            settings = new PatientSettings
            {
                PatientSettingsId = Guid.NewGuid(),
                UserId = userId,
                LowPressureThreshold = _thresholdsConfig.DefaultLowThreshold,
                HighPressureThreshold = _thresholdsConfig.DefaultHighThreshold,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.PatientSettings.Add(settings);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Created default settings for patient {UserId}: Low={Low}, High={High}",
                userId, _thresholdsConfig.DefaultLowThreshold, _thresholdsConfig.DefaultHighThreshold);
        }

        return settings;
    }

    /// <summary>
    /// Updates patient pressure threshold settings.
    /// Validates thresholds against configured ranges.
    /// </summary>
    /// <param name="userId">The patient's user ID</param>
    /// <param name="lowThreshold">New low pressure threshold</param>
    /// <param name="highThreshold">New high pressure threshold</param>
    /// <returns>Tuple with success flag, message, and updated settings</returns>
    public async Task<(bool Success, string Message, PatientSettings? Settings)> UpdateSettingsAsync(
        Guid userId,
        int lowThreshold,
        int highThreshold)
    {
        // Validate request using configured ranges (from appsettings.json)
        if (lowThreshold < _thresholdsConfig.LowThresholdMin ||
            lowThreshold > _thresholdsConfig.LowThresholdMax)
        {
            return (false,
                $"Low threshold must be between {_thresholdsConfig.LowThresholdMin} and {_thresholdsConfig.LowThresholdMax}",
                null);
        }

        if (highThreshold < _thresholdsConfig.HighThresholdMin ||
            highThreshold > _thresholdsConfig.HighThresholdMax)
        {
            return (false,
                $"High threshold must be between {_thresholdsConfig.HighThresholdMin} and {_thresholdsConfig.HighThresholdMax}",
                null);
        }

        if (lowThreshold >= highThreshold)
        {
            return (false, "Low threshold must be less than high threshold", null);
        }

        var settings = await _context.PatientSettings
            .FirstOrDefaultAsync(ps => ps.UserId == userId);

        if (settings == null)
        {
            // Create new settings if not found
            settings = new PatientSettings
            {
                PatientSettingsId = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            _context.PatientSettings.Add(settings);
        }

        // Update thresholds
        settings.LowPressureThreshold = lowThreshold;
        settings.HighPressureThreshold = highThreshold;
        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Patient {UserId} updated thresholds: low={Low}, high={High}",
            userId, lowThreshold, highThreshold);

        return (true, "Settings updated successfully", settings);
    }
}