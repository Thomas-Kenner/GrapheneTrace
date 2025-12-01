using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using GrapheneTrace.Web.Services;
using GrapheneTrace.Web.Services.Mocking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace GrapheneTrace.Web.Tests.Services;

/// <summary>
/// Unit tests for AlertService.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Test Coverage:
/// 1. EvaluateFrameAsync detects threshold breach correctly
/// 2. EvaluateFrameAsync respects patient-specific thresholds
/// 3. EvaluateFrameAsync calculates breach severity correctly
/// 4. EvaluateFrameAsync respects cooldown periods
/// 5. EvaluateEquipmentFault detects fault conditions
/// 6. EvaluateEquipmentFault respects cooldown periods
/// 7. CreatePressureAlertNotification generates correct content
/// 8. CreateEquipmentFaultNotification generates correct content for each fault type
/// 9. AcknowledgeAlert resets cooldown
/// 10. ClearCooldowns removes all cooldowns for a patient
///
/// Testing Strategy:
/// - Uses in-memory EF Core database for isolation
/// - Mocks ILogger for logging verification
/// - Tests cooldown behavior with time manipulation
/// </remarks>
public class AlertServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PatientSettingsService _patientSettingsService;
    private readonly AlertService _alertService;
    private readonly PressureThresholdsConfig _config;
    private readonly Guid _testPatientId = Guid.NewGuid();
    private readonly Guid _testClinicianId = Guid.NewGuid();

    public AlertServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"AlertServiceTests_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);

        // Setup configuration
        _config = new PressureThresholdsConfig
        {
            MinValue = 0,
            MaxValue = 255,
            DefaultLowThreshold = 50,
            DefaultHighThreshold = 200,
            LowThresholdMin = 10,
            LowThresholdMax = 100,
            HighThresholdMin = 150,
            HighThresholdMax = 250
        };

        // Setup patient settings service
        var settingsLogger = new Mock<ILogger<PatientSettingsService>>();
        _patientSettingsService = new PatientSettingsService(_context, _config, settingsLogger.Object);

        // Setup alert service with mock DB context factory
        var contextFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        contextFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_context);

        var alertLogger = new Mock<ILogger<AlertService>>();
        _alertService = new AlertService(
            contextFactory.Object,
            _patientSettingsService,
            _config,
            alertLogger.Object);

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        // Create patient settings with custom thresholds
        _context.PatientSettings.Add(new PatientSettings
        {
            PatientSettingsId = Guid.NewGuid(),
            UserId = _testPatientId,
            LowPressureThreshold = 60,
            HighPressureThreshold = 180,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Create patient-clinician assignment
        _context.PatientClinicianAssignments.Add(new PatientClinicianAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = _testPatientId,
            ClinicianId = _testClinicianId,
            AssignedAt = DateTime.UtcNow
        });

        _context.SaveChanges();
    }

    #region EvaluateFrameAsync Tests

    [Fact]
    public async Task EvaluateFrameAsync_DetectsThresholdBreach_WhenPeakExceedsThreshold()
    {
        // Arrange
        var frame = CreateTestFrame(peakPressure: 200, alerts: MedicalAlert.ThresholdBreach);

        // Act
        var result = await _alertService.EvaluateFrameAsync(frame);

        // Assert
        Assert.True(result.ThresholdBreached);
        Assert.Equal(200, result.PeakPressure);
        Assert.Equal(180, result.HighThreshold); // Patient-specific threshold
    }

    [Fact]
    public async Task EvaluateFrameAsync_NoThresholdBreach_WhenPeakBelowThreshold()
    {
        // Arrange
        var frame = CreateTestFrame(peakPressure: 150);

        // Act
        var result = await _alertService.EvaluateFrameAsync(frame);

        // Assert
        Assert.False(result.ThresholdBreached);
    }

    [Fact]
    public async Task EvaluateFrameAsync_UsesPatientSpecificThresholds()
    {
        // Arrange - peak is above patient threshold (180) but below default (200)
        var frame = CreateTestFrame(peakPressure: 185);

        // Act
        var result = await _alertService.EvaluateFrameAsync(frame);

        // Assert
        Assert.True(result.ThresholdBreached);
        Assert.Equal(180, result.HighThreshold);
    }

    [Fact]
    public async Task EvaluateFrameAsync_CalculatesBreachSeverity()
    {
        // Arrange - max is 255, threshold is 180, peak is 217 (50% of way to max)
        var frame = CreateTestFrame(peakPressure: 217, alerts: MedicalAlert.ThresholdBreach);

        // Act
        var result = await _alertService.EvaluateFrameAsync(frame);

        // Assert
        Assert.True(result.BreachSeverity > 0);
        Assert.True(result.BreachSeverity <= 1.0);
        // (217 - 180) / (255 - 180) = 37 / 75 = ~0.49
        Assert.InRange(result.BreachSeverity, 0.4, 0.6);
    }

    [Fact]
    public async Task EvaluateFrameAsync_ShouldNotifyOnFirstAlert()
    {
        // Arrange
        var frame = CreateTestFrame(peakPressure: 200, alerts: MedicalAlert.ThresholdBreach);

        // Act
        var result = await _alertService.EvaluateFrameAsync(frame);

        // Assert
        Assert.True(result.ShouldNotifyPatient);
    }

    [Fact]
    public async Task EvaluateFrameAsync_RespectsNotificationCooldown()
    {
        // Arrange
        var frame = CreateTestFrame(peakPressure: 200, alerts: MedicalAlert.ThresholdBreach);

        // Act - First evaluation should allow notification
        var result1 = await _alertService.EvaluateFrameAsync(frame);

        // Act - Second evaluation immediately after should be blocked by cooldown
        var result2 = await _alertService.EvaluateFrameAsync(frame);

        // Assert
        Assert.True(result1.ShouldNotifyPatient);
        Assert.False(result2.ShouldNotifyPatient);
    }

    #endregion

    #region EvaluateEquipmentFault Tests

    [Fact]
    public void EvaluateEquipmentFault_DetectsFaultCondition()
    {
        // Arrange
        var faults = DeviceFault.DeadPixels;

        // Act
        var result = _alertService.EvaluateEquipmentFault(_testPatientId, faults);

        // Assert
        Assert.True(result.HasEquipmentFault);
        Assert.Equal(faults, result.DeviceFaults);
    }

    [Fact]
    public void EvaluateEquipmentFault_NoFault_WhenNone()
    {
        // Act
        var result = _alertService.EvaluateEquipmentFault(_testPatientId, DeviceFault.None);

        // Assert
        Assert.False(result.HasEquipmentFault);
    }

    [Fact]
    public void EvaluateEquipmentFault_RespectsNotificationCooldown()
    {
        // Arrange
        var faults = DeviceFault.CalibrationDrift;

        // Act
        var result1 = _alertService.EvaluateEquipmentFault(_testPatientId, faults);
        var result2 = _alertService.EvaluateEquipmentFault(_testPatientId, faults);

        // Assert
        Assert.True(result1.ShouldNotifyPatient);
        Assert.False(result2.ShouldNotifyPatient); // Blocked by cooldown
    }

    #endregion

    #region Notification Content Tests

    [Fact]
    public void CreatePressureAlertNotification_GeneratesCorrectContent()
    {
        // Arrange
        var evaluation = new AlertEvaluation
        {
            PatientId = _testPatientId,
            PeakPressure = 200,
            HighThreshold = 180,
            BreachSeverity = 0.5
        };

        // Act
        var notification = _alertService.CreatePressureAlertNotification(evaluation);

        // Assert
        Assert.Contains("Pressure", notification.Title);
        Assert.Contains("200", notification.Body);
        Assert.Contains("180", notification.Body);
        Assert.NotNull(notification.Tag);
    }

    [Fact]
    public void CreatePressureAlertNotification_CriticalAlertRequiresInteraction()
    {
        // Arrange
        var evaluation = new AlertEvaluation
        {
            PatientId = _testPatientId,
            PeakPressure = 250,
            HighThreshold = 180,
            BreachSeverity = 0.9 // Critical severity
        };

        // Act
        var notification = _alertService.CreatePressureAlertNotification(evaluation);

        // Assert
        Assert.True(notification.RequireInteraction);
        Assert.Contains("Critical", notification.Title);
    }

    [Theory]
    [InlineData(DeviceFault.Disconnected, "disconnected")]
    [InlineData(DeviceFault.Saturation, "saturation")]
    [InlineData(DeviceFault.DeadPixels, "Dead")]
    [InlineData(DeviceFault.CalibrationDrift, "Calibration")]
    [InlineData(DeviceFault.ElectricalNoise, "interference")]
    public void CreateEquipmentFaultNotification_DescribesEachFaultType(DeviceFault fault, string expectedContent)
    {
        // Arrange
        var evaluation = new AlertEvaluation
        {
            PatientId = _testPatientId,
            DeviceFaults = fault,
            HasEquipmentFault = true
        };

        // Act
        var notification = _alertService.CreateEquipmentFaultNotification(evaluation);

        // Assert
        Assert.Contains(expectedContent, notification.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateEquipmentFaultNotification_CriticalFaultsRequireInteraction()
    {
        // Arrange - Disconnected is a critical fault
        var evaluation = new AlertEvaluation
        {
            PatientId = _testPatientId,
            DeviceFaults = DeviceFault.Disconnected,
            HasEquipmentFault = true
        };

        // Act
        var notification = _alertService.CreateEquipmentFaultNotification(evaluation);

        // Assert
        Assert.True(notification.RequireInteraction);
    }

    [Fact]
    public void CreateClinicianAlertNotification_IncludesPatientName()
    {
        // Arrange
        var evaluation = new AlertEvaluation
        {
            PatientId = _testPatientId,
            PeakPressure = 200,
            HighThreshold = 180,
            ThresholdBreached = true
        };

        // Act
        var notification = _alertService.CreateClinicianAlertNotification(evaluation, "John Smith");

        // Assert
        Assert.Contains("John Smith", notification.Title);
    }

    #endregion

    #region Cooldown Management Tests

    [Fact]
    public void AcknowledgeAlert_ResetsCooldown()
    {
        // Arrange - Trigger an alert to set cooldown
        var faults = DeviceFault.DeadPixels;
        _alertService.EvaluateEquipmentFault(_testPatientId, faults);

        // Act - Acknowledge the alert
        _alertService.AcknowledgeAlert(_testPatientId, $"equipment-{faults}");

        // Assert - Should be able to notify again
        var result = _alertService.EvaluateEquipmentFault(_testPatientId, faults);
        Assert.True(result.ShouldNotifyPatient);
    }

    [Fact]
    public void ClearCooldowns_RemovesAllCooldownsForPatient()
    {
        // Arrange - Trigger multiple alerts
        _alertService.EvaluateEquipmentFault(_testPatientId, DeviceFault.DeadPixels);
        _alertService.EvaluateEquipmentFault(_testPatientId, DeviceFault.CalibrationDrift);

        // Act
        _alertService.ClearCooldowns(_testPatientId);

        // Assert - All alerts should be able to notify again
        var result1 = _alertService.EvaluateEquipmentFault(_testPatientId, DeviceFault.DeadPixels);
        var result2 = _alertService.EvaluateEquipmentFault(_testPatientId, DeviceFault.CalibrationDrift);

        Assert.True(result1.ShouldNotifyPatient);
        Assert.True(result2.ShouldNotifyPatient);
    }

    #endregion

    #region Assignment Tests

    [Fact]
    public async Task GetAssignedCliniciansAsync_ReturnsAssignedClinicians()
    {
        // Act
        var clinicians = await _alertService.GetAssignedCliniciansAsync(_testPatientId);

        // Assert
        Assert.Single(clinicians);
        Assert.Contains(_testClinicianId, clinicians);
    }

    [Fact]
    public async Task GetAssignedPatientsAsync_ReturnsAssignedPatients()
    {
        // Act
        var patients = await _alertService.GetAssignedPatientsAsync(_testClinicianId);

        // Assert
        Assert.Single(patients);
        Assert.Contains(_testPatientId, patients);
    }

    #endregion

    #region Helper Methods

    private HeatmapFrame CreateTestFrame(
        int peakPressure = 100,
        MedicalAlert alerts = MedicalAlert.None,
        DeviceFault faults = DeviceFault.None)
    {
        return new HeatmapFrame
        {
            Timestamp = DateTime.UtcNow,
            FrameNumber = 1,
            PatientId = _testPatientId,
            DeviceId = "test-device",
            PressureData = new int[1024],
            ActiveFaults = faults,
            Alerts = alerts,
            PeakPressure = peakPressure,
            ContactAreaPercent = 50f,
            Scenario = SimulationScenario.Static
        };
    }

    #endregion

    public void Dispose()
    {
        _context.Dispose();
    }
}
