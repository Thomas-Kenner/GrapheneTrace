using System.ComponentModel.DataAnnotations;

namespace GrapheneTrace.Web.Models;

/// <summary>
/// Configuration for pressure threshold validation ranges and defaults.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// This configuration is loaded from appsettings.json under the "PressureThresholds" section.
///
/// Configuration Location: appsettings.json
/// Section: "PressureThresholds"
///
/// To modify threshold ranges or defaults:
/// 1. Edit appsettings.json (production values)
/// 2. Edit appsettings.Development.json (development overrides)
/// 3. Restart the application
///
/// Configuration values are validated at startup. Invalid configurations will prevent
/// application startup with a descriptive error message.
///
/// Purpose:
/// - Defines valid ranges for patient-configured thresholds
/// - Provides default values for new patients
/// - Allows flexibility for different sensor hardware ranges without code changes
///
/// Design Note:
/// Using configuration instead of hardcoded values allows the system to adapt to
/// different sensor hardware specifications. For example, if a new sensor model
/// uses a different range (e.g., 0-1023), only the config file needs updating.
/// </remarks>
public class PressureThresholdsConfig
{
    /// <summary>
    /// Section name in appsettings.json.
    /// </summary>
    public const string SectionName = "PressureThresholds";

    /// <summary>
    /// Absolute minimum pressure value the sensor can report.
    /// Default: 1 (sensor baseline, no pressure)
    /// </summary>
    [Required]
    [Range(0, int.MaxValue)]
    public int MinValue { get; set; } = 1;

    /// <summary>
    /// Absolute maximum pressure value the sensor can report.
    /// Default: 255 (sensor saturation)
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int MaxValue { get; set; } = 255;

    /// <summary>
    /// Minimum allowed value for patient's low threshold setting.
    /// Must be >= MinValue.
    /// Default: 1
    /// </summary>
    [Required]
    [Range(0, int.MaxValue)]
    public int LowThresholdMin { get; set; } = 1;

    /// <summary>
    /// Maximum allowed value for patient's low threshold setting.
    /// Must be less than HighThresholdMin to ensure low < high.
    /// Default: 254
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int LowThresholdMax { get; set; } = 254;

    /// <summary>
    /// Minimum allowed value for patient's high threshold setting.
    /// Must be > LowThresholdMax to ensure low < high.
    /// Default: 2
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int HighThresholdMin { get; set; } = 2;

    /// <summary>
    /// Maximum allowed value for patient's high threshold setting.
    /// Must be <= MaxValue.
    /// Default: 255
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int HighThresholdMax { get; set; } = 255;

    /// <summary>
    /// Default low threshold for new patients.
    /// Must be between LowThresholdMin and LowThresholdMax.
    /// Default: 50 (early warning level)
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int DefaultLowThreshold { get; set; } = 50;

    /// <summary>
    /// Default high threshold for new patients.
    /// Must be between HighThresholdMin and HighThresholdMax.
    /// Must be > DefaultLowThreshold.
    /// Default: 200 (urgent alert level)
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int DefaultHighThreshold { get; set; } = 200;

    /// <summary>
    /// Validates the configuration for logical consistency.
    /// Called at application startup to fail-fast on misconfiguration.
    /// </summary>
    /// <returns>List of validation error messages (empty if valid)</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        // Basic range checks
        if (MinValue < 0)
            errors.Add($"MinValue ({MinValue}) must be >= 0");

        if (MaxValue <= MinValue)
            errors.Add($"MaxValue ({MaxValue}) must be greater than MinValue ({MinValue})");

        // Low threshold range validation
        if (LowThresholdMin < MinValue)
            errors.Add($"LowThresholdMin ({LowThresholdMin}) must be >= MinValue ({MinValue})");

        if (LowThresholdMin > LowThresholdMax)
            errors.Add($"LowThresholdMin ({LowThresholdMin}) must be <= LowThresholdMax ({LowThresholdMax})");

        if (LowThresholdMax > MaxValue)
            errors.Add($"LowThresholdMax ({LowThresholdMax}) must be <= MaxValue ({MaxValue})");

        // High threshold range validation
        if (HighThresholdMin < MinValue)
            errors.Add($"HighThresholdMin ({HighThresholdMin}) must be >= MinValue ({MinValue})");

        if (HighThresholdMin > HighThresholdMax)
            errors.Add($"HighThresholdMin ({HighThresholdMin}) must be <= HighThresholdMax ({HighThresholdMax})");

        if (HighThresholdMax > MaxValue)
            errors.Add($"HighThresholdMax ({HighThresholdMax}) must be <= MaxValue ({MaxValue})");

        // Ensure ranges allow low < high (ranges must have at least 1 value gap)
        if (LowThresholdMin >= HighThresholdMax)
            errors.Add($"LowThresholdMin ({LowThresholdMin}) must be less than HighThresholdMax ({HighThresholdMax}) to allow any low < high combinations");

        // Default values validation
        if (DefaultLowThreshold < LowThresholdMin || DefaultLowThreshold > LowThresholdMax)
            errors.Add($"DefaultLowThreshold ({DefaultLowThreshold}) must be between LowThresholdMin ({LowThresholdMin}) and LowThresholdMax ({LowThresholdMax})");

        if (DefaultHighThreshold < HighThresholdMin || DefaultHighThreshold > HighThresholdMax)
            errors.Add($"DefaultHighThreshold ({DefaultHighThreshold}) must be between HighThresholdMin ({HighThresholdMin}) and HighThresholdMax ({HighThresholdMax})");

        if (DefaultLowThreshold >= DefaultHighThreshold)
            errors.Add($"DefaultLowThreshold ({DefaultLowThreshold}) must be less than DefaultHighThreshold ({DefaultHighThreshold})");

        return errors;
    }
}
