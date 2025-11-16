using System.ComponentModel.DataAnnotations;

namespace GrapheneTrace.Web.Models;

/// <summary>
/// Patient-specific settings for pressure monitoring alerts.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Implements Story #9: Patient threshold configuration
///
/// Design Pattern: Domain entity with validation attributes
///
/// ⚙️ CONFIGURATION NOTE:
/// Pressure value ranges and defaults are configurable in appsettings.json
/// under the "PressureThresholds" section. The hardcoded values in [Range]
/// attributes below are for basic model validation, but runtime validation
/// in SettingsController uses values from appsettings.json.
///
/// To modify sensor ranges or defaults:
/// 1. Edit appsettings.json > PressureThresholds section
/// 2. Restart the application (validation occurs at startup)
/// 3. See PressureThresholdsConfig.cs for configuration options
///
/// Current Default Configuration (from appsettings.json):
/// - Sensor range: 1-255 (1=no pressure, 255=saturation)
/// - Default low threshold: 50 (early warning)
/// - Default high threshold: 200 (urgent alert)
/// - LowPressureThreshold valid range: 1-254
/// - HighPressureThreshold valid range: 2-255
///
/// Validation Rules:
/// - LowPressureThreshold must be within configured min/max range
/// - HighPressureThreshold must be within configured min/max range
/// - LowPressureThreshold must be less than HighPressureThreshold
/// </remarks>
public class PatientSettings
{
    /// <summary>
    /// Primary key for patient settings.
    /// </summary>
    [Key]
    public Guid PatientSettingsId { get; set; }

    /// <summary>
    /// Foreign key to patient user account (AspNetUsers table).
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Low pressure threshold for early warning alerts.
    /// Default value is configured in appsettings.json (DefaultLowThreshold, currently 50).
    /// </summary>
    /// <remarks>
    /// When pressure readings exceed this value, patients receive an early warning
    /// to adjust their position or take preventive action.
    ///
    /// NOTE: Default value (50) is hardcoded here for database entity initialization.
    /// Runtime validation in SettingsController uses values from appsettings.json.
    /// To change defaults, edit appsettings.json > PressureThresholds > DefaultLowThreshold.
    /// </remarks>
    [Required]
    [Range(1, 254, ErrorMessage = "Low threshold must be between 1 and 254")]
    public int LowPressureThreshold { get; set; } = 50;

    /// <summary>
    /// High pressure threshold for urgent alerts.
    /// Default value is configured in appsettings.json (DefaultHighThreshold, currently 200).
    /// </summary>
    /// <remarks>
    /// When pressure readings exceed this value, patients receive an urgent alert
    /// requiring immediate action. Clinicians are also notified (Story #7).
    ///
    /// NOTE: Default value (200) is hardcoded here for database entity initialization.
    /// Runtime validation in SettingsController uses values from appsettings.json.
    /// To change defaults, edit appsettings.json > PressureThresholds > DefaultHighThreshold.
    /// </remarks>
    [Required]
    [Range(2, 255, ErrorMessage = "High threshold must be between 2 and 255")]
    public int HighPressureThreshold { get; set; } = 200;

    /// <summary>
    /// Timestamp when settings were created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when settings were last modified.
    /// </summary>
    /// <remarks>
    /// Updated on every save. Used for audit trail and "last updated" UI display.
    /// </remarks>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to patient user.
    /// </summary>
    public virtual ApplicationUser User { get; set; } = null!;
}
