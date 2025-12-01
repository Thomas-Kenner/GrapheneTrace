using GrapheneTrace.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GrapheneTrace.Web.Hubs;

/// <summary>
/// SignalR Hub for real-time alert notifications between users.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Purpose: Enables real-time push notifications for medical alerts across different users.
/// Clinicians receive alerts when their assigned patients exceed pressure thresholds or
/// experience equipment faults. Patients receive direct messages from clinicians.
///
/// Related User Stories:
/// - Story #7: Clinician notifications for peak pressure index alerts
/// - Story #18: Equipment fault notifications
/// - Story #28: Direct clinician-to-patient notifications
///
/// Group Structure:
/// - patient-{patientId}: Patient joins this group to receive alerts about themselves
/// - clinician-patients-{clinicianId}: Group of all patient IDs a clinician monitors
/// - patient-alerts-{patientId}: Clinicians join this to receive alerts for specific patients
///
/// Security: All connections require authentication.
/// </remarks>
[Authorize]
public class AlertHub : Hub
{
    private readonly AlertService _alertService;
    private readonly ILogger<AlertHub> _logger;

    public AlertHub(AlertService alertService, ILogger<AlertHub> logger)
    {
        _alertService = alertService;
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects. Automatically joins relevant alert groups.
    /// Author: SID:2412494
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var userType = GetUserType();

        if (userId == Guid.Empty)
        {
            _logger.LogWarning("AlertHub connection attempted with invalid user ID");
            await base.OnConnectedAsync();
            return;
        }

        if (userType == "patient")
        {
            // Patient joins their own alert group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"patient-{userId}");
            _logger.LogInformation("Patient {UserId} joined alert group", userId);
        }
        else if (userType == "clinician")
        {
            // Clinician joins alert groups for all their assigned patients
            var assignedPatients = await _alertService.GetAssignedPatientsAsync(userId);

            foreach (var patientId in assignedPatients)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"patient-alerts-{patientId}");
            }

            _logger.LogInformation(
                "Clinician {UserId} joined alert groups for {Count} patients",
                userId, assignedPatients.Count);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Sends a pressure alert to a patient and their assigned clinicians.
    /// Author: SID:2412494
    /// </summary>
    /// <param name="patientId">The patient experiencing the alert</param>
    /// <param name="alertData">Alert details</param>
    public async Task SendPressureAlert(Guid patientId, PressureAlertData alertData)
    {
        // Send to patient
        await Clients.Group($"patient-{patientId}").SendAsync("ReceivePressureAlert", alertData);

        // Send to assigned clinicians
        await Clients.Group($"patient-alerts-{patientId}").SendAsync("ReceivePatientAlert", new
        {
            PatientId = patientId,
            AlertType = "pressure",
            alertData.PeakPressure,
            alertData.Threshold,
            alertData.Severity,
            alertData.Message,
            Timestamp = DateTime.UtcNow
        });

        _logger.LogInformation(
            "Pressure alert sent for patient {PatientId}: {PeakPressure} exceeds {Threshold}",
            patientId, alertData.PeakPressure, alertData.Threshold);
    }

    /// <summary>
    /// Sends an equipment fault alert to a patient and their assigned clinicians.
    /// Author: SID:2412494
    /// </summary>
    /// <param name="patientId">The patient with the equipment fault</param>
    /// <param name="faultData">Fault details</param>
    public async Task SendEquipmentFaultAlert(Guid patientId, EquipmentFaultData faultData)
    {
        // Send to patient
        await Clients.Group($"patient-{patientId}").SendAsync("ReceiveEquipmentFault", faultData);

        // Send to assigned clinicians
        await Clients.Group($"patient-alerts-{patientId}").SendAsync("ReceivePatientAlert", new
        {
            PatientId = patientId,
            AlertType = "equipment",
            faultData.FaultType,
            faultData.Description,
            faultData.IsCritical,
            Timestamp = DateTime.UtcNow
        });

        _logger.LogWarning(
            "Equipment fault alert sent for patient {PatientId}: {FaultType}",
            patientId, faultData.FaultType);
    }

    /// <summary>
    /// Sends a direct notification from clinician to patient.
    /// Author: SID:2412494
    /// </summary>
    /// <param name="patientId">Target patient</param>
    /// <param name="message">Notification message</param>
    public async Task SendClinicianNotification(Guid patientId, string message)
    {
        var clinicianId = GetUserId();
        var userType = GetUserType();

        if (userType != "clinician")
        {
            _logger.LogWarning(
                "Non-clinician {UserId} attempted to send clinician notification",
                clinicianId);
            return;
        }

        await Clients.Group($"patient-{patientId}").SendAsync("ReceiveClinicianNotification", new
        {
            ClinicianId = clinicianId,
            Message = message,
            Timestamp = DateTime.UtcNow
        });

        _logger.LogInformation(
            "Clinician {ClinicianId} sent notification to patient {PatientId}",
            clinicianId, patientId);
    }

    /// <summary>
    /// Allows a clinician to subscribe to alerts for a specific patient.
    /// Author: SID:2412494
    /// </summary>
    public async Task SubscribeToPatientAlerts(Guid patientId)
    {
        var userType = GetUserType();

        if (userType != "clinician")
        {
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"patient-alerts-{patientId}");
        _logger.LogInformation(
            "Clinician {UserId} subscribed to alerts for patient {PatientId}",
            GetUserId(), patientId);
    }

    /// <summary>
    /// Allows a clinician to unsubscribe from alerts for a specific patient.
    /// Author: SID:2412494
    /// </summary>
    public async Task UnsubscribeFromPatientAlerts(Guid patientId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"patient-alerts-{patientId}");
        _logger.LogInformation(
            "User {UserId} unsubscribed from alerts for patient {PatientId}",
            GetUserId(), patientId);
    }

    /// <summary>
    /// Acknowledges an alert, clearing it from display.
    /// Author: SID:2412494
    /// </summary>
    public void AcknowledgeAlert(Guid patientId, string alertType)
    {
        var userId = GetUserId();
        _alertService.AcknowledgeAlert(patientId, alertType);

        _logger.LogInformation(
            "Alert acknowledged by {UserId} for patient {PatientId}: {AlertType}",
            userId, patientId, alertType);
    }

    #region Private Helpers

    private Guid GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return Guid.Empty;
    }

    private string GetUserType()
    {
        if (Context.User?.IsInRole("Patient") ?? false)
            return "patient";
        if (Context.User?.IsInRole("Clinician") ?? false)
            return "clinician";
        if (Context.User?.IsInRole("Admin") ?? false)
            return "admin";
        return "unknown";
    }

    #endregion
}

/// <summary>
/// Data transfer object for pressure alerts.
/// Author: SID:2412494
/// </summary>
public class PressureAlertData
{
    public int PeakPressure { get; set; }
    public int Threshold { get; set; }
    public double Severity { get; set; }
    public string Message { get; set; } = "";
    public bool RequireInteraction { get; set; }
}

/// <summary>
/// Data transfer object for equipment fault alerts.
/// Author: SID:2412494
/// </summary>
public class EquipmentFaultData
{
    public string FaultType { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsCritical { get; set; }
}
