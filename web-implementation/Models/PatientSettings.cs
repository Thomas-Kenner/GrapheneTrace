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
/// Pressure Values:
/// - Sensor range: 1-255 (1=no pressure, 255=saturation)
/// - Default low threshold: 50 (early warning)
/// - Default high threshold: 200 (urgent alert)
///
/// Validation Rules:
/// - LowPressureThreshold must be between 1-254
/// - HighPressureThreshold must be between 2-255
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
    /// Default: 50
    /// </summary>
    /// <remarks>
    /// When pressure readings exceed this value, patients receive an early warning
    /// to adjust their position or take preventive action.
    /// </remarks>
    [Required]
    [Range(1, 254, ErrorMessage = "Low threshold must be between 1 and 254")]
    public int LowPressureThreshold { get; set; } = 50;

    /// <summary>
    /// High pressure threshold for urgent alerts.
    /// Default: 200
    /// </summary>
    /// <remarks>
    /// When pressure readings exceed this value, patients receive an urgent alert
    /// requiring immediate action. Clinicians are also notified (Story #7).
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
