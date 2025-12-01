using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using GrapheneTrace.Web.Services.Mocking;
using Microsoft.EntityFrameworkCore;

namespace GrapheneTrace.Web.Services;

/// <summary>
/// Service for managing pressure alerts and equipment fault notifications.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Purpose: Central orchestrator for alert detection, threshold checking, and notification
/// coordination. Handles both patient-facing alerts (browser notifications) and
/// clinician notifications (via SignalR hub).
///
/// Related User Stories:
/// - Story #7: Clinician notifications for peak pressure index alerts
/// - Story #10: Patient alerts for threshold exceedance
/// - Story #18: Equipment fault/sensor issue notifications
/// - Story #28: Direct clinician-to-patient notifications (coordination)
///
/// Design Pattern:
/// - Uses cooldown tracking to prevent notification spam
/// - Integrates with PatientSettingsService for patient-specific thresholds
/// - Stateless service - alert state is managed per-component
/// </remarks>
public class AlertService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly PatientSettingsService _patientSettingsService;
    private readonly ILogger<AlertService> _logger;
    private readonly PressureThresholdsConfig _config;

    // Cooldown tracking to prevent notification spam
    // Key: (PatientId, AlertType) -> Last notification time
    private readonly Dictionary<(Guid, string), DateTime> _alertCooldowns = new();
    private readonly object _cooldownLock = new();

    // Cooldown periods for different alert types
    private static readonly TimeSpan PressureAlertCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan EquipmentFaultCooldown = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan SustainedPressureCooldown = TimeSpan.FromMinutes(1);

    public AlertService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        PatientSettingsService patientSettingsService,
        PressureThresholdsConfig config,
        ILogger<AlertService> logger)
    {
        _contextFactory = contextFactory;
        _patientSettingsService = patientSettingsService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Evaluates a pressure frame against patient-specific thresholds.
    /// Author: SID:2412494
    /// </summary>
    /// <param name="frame">The heatmap frame to evaluate</param>
    /// <returns>Alert evaluation result with notification recommendations</returns>
    public async Task<AlertEvaluation> EvaluateFrameAsync(HeatmapFrame frame)
    {
        var result = new AlertEvaluation
        {
            PatientId = frame.PatientId,
            Timestamp = frame.Timestamp,
            PeakPressure = frame.PeakPressure,
            DeviceFaults = frame.ActiveFaults,
            MedicalAlerts = frame.Alerts
        };

        // Get patient-specific thresholds
        var settings = await _patientSettingsService.GetSettingsAsync(frame.PatientId);
        result.LowThreshold = settings.LowPressureThreshold;
        result.HighThreshold = settings.HighPressureThreshold;

        // Check against patient's personal thresholds
        if (frame.PeakPressure >= settings.HighPressureThreshold)
        {
            result.ThresholdBreached = true;
            result.BreachSeverity = CalculateBreachSeverity(
                frame.PeakPressure,
                settings.LowPressureThreshold,
                settings.HighPressureThreshold);
        }

        // Determine if notifications should be sent (respecting cooldowns)
        result.ShouldNotifyPatient = ShouldSendNotification(
            frame.PatientId,
            GetAlertKey(result),
            GetCooldownForAlert(result));

        result.ShouldNotifyClinician = result.ThresholdBreached &&
            ShouldSendNotification(
                frame.PatientId,
                $"clinician-{GetAlertKey(result)}",
                PressureAlertCooldown);

        return result;
    }

    /// <summary>
    /// Evaluates equipment faults and determines if notifications should be sent.
    /// Author: SID:2412494
    /// </summary>
    public AlertEvaluation EvaluateEquipmentFault(Guid patientId, DeviceFault faults)
    {
        var result = new AlertEvaluation
        {
            PatientId = patientId,
            Timestamp = DateTime.UtcNow,
            DeviceFaults = faults,
            HasEquipmentFault = faults != DeviceFault.None
        };

        if (result.HasEquipmentFault)
        {
            var alertKey = $"equipment-{faults}";
            result.ShouldNotifyPatient = ShouldSendNotification(
                patientId, alertKey, EquipmentFaultCooldown);

            result.ShouldNotifyClinician = ShouldSendNotification(
                patientId, $"clinician-{alertKey}", EquipmentFaultCooldown);
        }

        return result;
    }

    /// <summary>
    /// Gets the list of clinicians assigned to a patient.
    /// Author: SID:2412494
    /// </summary>
    /// <param name="patientId">The patient's user ID</param>
    /// <returns>List of assigned clinician user IDs</returns>
    public async Task<List<Guid>> GetAssignedCliniciansAsync(Guid patientId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.PatientClinicianAssignments
            .Where(a => a.PatientId == patientId)
            .Select(a => a.ClinicianId)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all patients assigned to a clinician.
    /// Author: SID:2412494
    /// </summary>
    /// <param name="clinicianId">The clinician's user ID</param>
    /// <returns>List of assigned patient user IDs</returns>
    public async Task<List<Guid>> GetAssignedPatientsAsync(Guid clinicianId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.PatientClinicianAssignments
            .Where(a => a.ClinicianId == clinicianId)
            .Select(a => a.PatientId)
            .ToListAsync();
    }

    /// <summary>
    /// Creates notification content for a pressure alert.
    /// Author: SID:2412494
    /// </summary>
    public NotificationContent CreatePressureAlertNotification(AlertEvaluation evaluation)
    {
        var severity = evaluation.BreachSeverity;

        return new NotificationContent
        {
            Title = severity >= 0.8
                ? "Critical Pressure Alert!"
                : "High Pressure Detected",
            Body = $"Peak pressure: {evaluation.PeakPressure} (threshold: {evaluation.HighThreshold}). " +
                   "Consider repositioning to relieve pressure.",
            Icon = "/images/alert-pressure.png",
            Tag = $"pressure-alert-{evaluation.PatientId}",
            RequireInteraction = severity >= 0.8
        };
    }

    /// <summary>
    /// Creates notification content for an equipment fault.
    /// Author: SID:2412494
    /// </summary>
    public NotificationContent CreateEquipmentFaultNotification(AlertEvaluation evaluation)
    {
        var faultDescription = GetFaultDescription(evaluation.DeviceFaults);
        var isCritical = evaluation.DeviceFaults.HasFlag(DeviceFault.Disconnected) ||
                         evaluation.DeviceFaults.HasFlag(DeviceFault.Saturation);

        return new NotificationContent
        {
            Title = isCritical ? "Equipment Fault!" : "Sensor Issue Detected",
            Body = faultDescription,
            Icon = "/images/alert-equipment.png",
            Tag = $"equipment-fault-{evaluation.PatientId}",
            RequireInteraction = isCritical
        };
    }

    /// <summary>
    /// Creates notification content for clinicians about patient alerts.
    /// Author: SID:2412494
    /// </summary>
    public NotificationContent CreateClinicianAlertNotification(
        AlertEvaluation evaluation,
        string patientName)
    {
        if (evaluation.HasEquipmentFault)
        {
            return new NotificationContent
            {
                Title = $"Equipment Issue: {patientName}",
                Body = $"Patient device reporting: {GetFaultDescription(evaluation.DeviceFaults)}",
                Icon = "/images/alert-clinician.png",
                Tag = $"clinician-equipment-{evaluation.PatientId}",
                RequireInteraction = false
            };
        }

        return new NotificationContent
        {
            Title = $"Pressure Alert: {patientName}",
            Body = $"Peak pressure {evaluation.PeakPressure} exceeds threshold {evaluation.HighThreshold}",
            Icon = "/images/alert-clinician.png",
            Tag = $"clinician-pressure-{evaluation.PatientId}",
            RequireInteraction = evaluation.BreachSeverity >= 0.8
        };
    }

    /// <summary>
    /// Marks an alert as acknowledged, resetting cooldown.
    /// Author: SID:2412494
    /// </summary>
    public void AcknowledgeAlert(Guid patientId, string alertKey)
    {
        lock (_cooldownLock)
        {
            _alertCooldowns.Remove((patientId, alertKey));
        }

        _logger.LogInformation(
            "Alert acknowledged for patient {PatientId}: {AlertKey}",
            patientId, alertKey);
    }

    /// <summary>
    /// Clears all cooldowns for a patient (e.g., when device reconnects).
    /// Author: SID:2412494
    /// </summary>
    public void ClearCooldowns(Guid patientId)
    {
        lock (_cooldownLock)
        {
            var keysToRemove = _alertCooldowns.Keys
                .Where(k => k.Item1 == patientId)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _alertCooldowns.Remove(key);
            }
        }
    }

    #region Private Helpers

    private bool ShouldSendNotification(Guid patientId, string alertKey, TimeSpan cooldown)
    {
        lock (_cooldownLock)
        {
            var key = (patientId, alertKey);

            if (_alertCooldowns.TryGetValue(key, out var lastNotification))
            {
                if (DateTime.UtcNow - lastNotification < cooldown)
                {
                    return false;
                }
            }

            // Update cooldown
            _alertCooldowns[key] = DateTime.UtcNow;
            return true;
        }
    }

    private static string GetAlertKey(AlertEvaluation evaluation)
    {
        if (evaluation.MedicalAlerts.HasFlag(MedicalAlert.SustainedPressure))
            return "sustained-pressure";

        if (evaluation.ThresholdBreached)
            return "threshold-breach";

        if (evaluation.MedicalAlerts.HasFlag(MedicalAlert.HighPressure))
            return "high-pressure";

        return "general";
    }

    private static TimeSpan GetCooldownForAlert(AlertEvaluation evaluation)
    {
        if (evaluation.MedicalAlerts.HasFlag(MedicalAlert.SustainedPressure))
            return SustainedPressureCooldown;

        return PressureAlertCooldown;
    }

    private double CalculateBreachSeverity(int peakPressure, int lowThreshold, int highThreshold)
    {
        // Calculate how far above threshold we are (0.0 = at threshold, 1.0 = at max)
        var range = _config.MaxValue - highThreshold;
        if (range <= 0) return 1.0;

        var overage = peakPressure - highThreshold;
        return Math.Clamp((double)overage / range, 0.0, 1.0);
    }

    private static string GetFaultDescription(DeviceFault faults)
    {
        var descriptions = new List<string>();

        if (faults.HasFlag(DeviceFault.Disconnected))
            descriptions.Add("Device disconnected - no data received");

        if (faults.HasFlag(DeviceFault.Saturation))
            descriptions.Add("Sensor saturation - readings at maximum");

        if (faults.HasFlag(DeviceFault.DeadPixels))
            descriptions.Add("Dead sensor pixels detected");

        if (faults.HasFlag(DeviceFault.PartialDataLoss))
            descriptions.Add("Partial data loss - some readings missing");

        if (faults.HasFlag(DeviceFault.CalibrationDrift))
            descriptions.Add("Calibration drift - readings may be inaccurate");

        if (faults.HasFlag(DeviceFault.ElectricalNoise))
            descriptions.Add("Electrical interference detected");

        return descriptions.Count > 0
            ? string.Join("; ", descriptions)
            : "Unknown equipment issue";
    }

    #endregion
}

/// <summary>
/// Result of alert evaluation containing notification recommendations.
/// Author: SID:2412494
/// </summary>
public class AlertEvaluation
{
    public Guid PatientId { get; set; }
    public DateTime Timestamp { get; set; }
    public int PeakPressure { get; set; }
    public int LowThreshold { get; set; }
    public int HighThreshold { get; set; }
    public bool ThresholdBreached { get; set; }
    public double BreachSeverity { get; set; }
    public MedicalAlert MedicalAlerts { get; set; }
    public DeviceFault DeviceFaults { get; set; }
    public bool HasEquipmentFault { get; set; }
    public bool ShouldNotifyPatient { get; set; }
    public bool ShouldNotifyClinician { get; set; }
}

/// <summary>
/// Content for a notification to be displayed.
/// Author: SID:2412494
/// </summary>
public class NotificationContent
{
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string? Icon { get; set; }
    public string? Tag { get; set; }
    public bool RequireInteraction { get; set; }
}
