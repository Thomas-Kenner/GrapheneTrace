using System.Collections.Concurrent;
using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GrapheneTrace.Web.Services.Mocking;

/// <summary>
/// Singleton manager for mock heatmap devices.
/// Creates one device per patient, provides authorized access to clinicians.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Design Pattern: Factory + Registry pattern
/// - Creates MockHeatmapDevice instances on demand
/// - Maintains registry of active devices keyed by patient ID
/// - Provides shared access to same device instance for patients and their clinicians
///
/// Thread Safety: Uses ConcurrentDictionary and SemaphoreSlim for safe concurrent access
///
/// Lifetime: Singleton - one instance for entire application lifetime
/// </remarks>
public class MockDeviceManager : IAsyncDisposable
{
    private readonly ILogger<MockDeviceManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly PressureThresholdsConfig _thresholdsConfig;
    private readonly ConcurrentDictionary<Guid, MockHeatmapDevice> _patientDevices = new();
    private readonly SemaphoreSlim _createLock = new(1, 1);
    private bool _disposed;

    public MockDeviceManager(
        ILogger<MockDeviceManager> logger,
        ILoggerFactory loggerFactory,
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        PressureThresholdsConfig thresholdsConfig)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _dbContextFactory = dbContextFactory;
        _thresholdsConfig = thresholdsConfig;

        _logger.LogInformation("MockDeviceManager initialized");
    }

    /// <summary>
    /// Get or create a mock device for a patient.
    /// Creates device if not exists and auto-starts it.
    /// </summary>
    /// <param name="patientId">Patient user ID</param>
    /// <returns>The patient's mock device (started automatically)</returns>
    public async Task<MockHeatmapDevice> GetOrCreateDeviceForPatientAsync(Guid patientId)
    {
        ThrowIfDisposed();

        // Fast path: device already exists
        if (_patientDevices.TryGetValue(patientId, out var existingDevice))
        {
            if (existingDevice.Status != DeviceStatus.Disposed)
            {
                return existingDevice;
            }
            // Device was disposed, remove it and create new one
            _patientDevices.TryRemove(patientId, out _);
        }

        // Slow path: need to create device
        await _createLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_patientDevices.TryGetValue(patientId, out existingDevice))
            {
                if (existingDevice.Status != DeviceStatus.Disposed)
                {
                    return existingDevice;
                }
                _patientDevices.TryRemove(patientId, out _);
            }

            // Verify patient exists
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var patient = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == patientId && u.UserType == "patient");

            if (patient == null)
            {
                throw new InvalidOperationException($"Patient with ID {patientId} not found");
            }

            // Create new device
            var deviceLogger = _loggerFactory.CreateLogger<MockHeatmapDevice>();
            var device = new MockHeatmapDevice(patientId, _thresholdsConfig, deviceLogger);

            if (_patientDevices.TryAdd(patientId, device))
            {
                device.Start();
                _logger.LogInformation(
                    "Created and started mock device {DeviceId} for patient {PatientId} ({PatientName})",
                    device.DeviceId, patientId, $"{patient.FirstName} {patient.LastName}");
                return device;
            }
            else
            {
                // Another thread created the device first, dispose our new one
                await device.DisposeAsync();
                return _patientDevices[patientId];
            }
        }
        finally
        {
            _createLock.Release();
        }
    }

    /// <summary>
    /// Get all active mock devices the clinician is authorized to view based on patient assignments.
    /// </summary>
    /// <param name="clinicianId">Clinician user ID</param>
    /// <returns>Collection of authorized mock devices (only running, non-disposed devices)</returns>
    public async Task<IReadOnlyList<MockHeatmapDevice>> GetDevicesForClinicianAsync(Guid clinicianId)
    {
        ThrowIfDisposed();

        var authorizedPatientIds = await GetAuthorizedPatientIdsForClinicianAsync(clinicianId);

        return _patientDevices
            .Where(kvp => authorizedPatientIds.Contains(kvp.Key) &&
                          kvp.Value.Status != DeviceStatus.Disposed)
            .Select(kvp => kvp.Value)
            .ToList();
    }

    /// <summary>
    /// Get a specific device by patient ID if clinician is authorized.
    /// </summary>
    /// <param name="clinicianId">Clinician user ID</param>
    /// <param name="patientId">Target patient ID</param>
    /// <returns>Device if authorized, null otherwise</returns>
    public async Task<MockHeatmapDevice?> GetDeviceIfAuthorizedAsync(Guid clinicianId, Guid patientId)
    {
        ThrowIfDisposed();

        var authorizedPatientIds = await GetAuthorizedPatientIdsForClinicianAsync(clinicianId);

        if (!authorizedPatientIds.Contains(patientId))
        {
            _logger.LogWarning(
                "Clinician {ClinicianId} unauthorized to access patient {PatientId} device",
                clinicianId, patientId);
            return null;
        }

        return TryGetDevice(patientId);
    }

    // Author: SID:2412494
    // Added method to retrieve all assigned patients with their active device status for clinician dashboard.
    /// <summary>
    /// Get all assigned patients with their device status for clinician dashboard display.
    /// Returns all patients assigned to the clinician, whether or not they have an active device.
    /// </summary>
    /// <param name="clinicianId">Clinician user ID</param>
    /// <returns>List of patient device info records, empty if clinician is not approved</returns>
    public async Task<IReadOnlyList<ClinicianPatientDeviceInfo>> GetPatientDeviceInfoForClinicianAsync(Guid clinicianId)
    {
        ThrowIfDisposed();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Verify clinician is approved
        var clinician = await dbContext.Users.FindAsync(clinicianId);
        if (clinician?.UserType != "clinician" || clinician.ApprovedAt == null)
        {
            _logger.LogWarning("Clinician {ClinicianId} not found or not approved", clinicianId);
            return Array.Empty<ClinicianPatientDeviceInfo>();
        }

        // Get all assigned patients with their names
        var assignedPatients = await dbContext.PatientClinicianAssignments
            .Where(a => a.ClinicianId == clinicianId)
            .Join(dbContext.Users,
                assignment => assignment.PatientId,
                user => user.Id,
                (assignment, user) => new
                {
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    user.DeactivatedAt
                })
            .Where(x => x.DeactivatedAt == null)
            .ToListAsync();

        // Build result list with device status
        var result = new List<ClinicianPatientDeviceInfo>(assignedPatients.Count);

        foreach (var patient in assignedPatients)
        {
            var device = TryGetDevice(patient.Id);
            var hasActiveDevice = device != null && device.Status == DeviceStatus.Running;
            var currentFrame = device?.GetCurrentFrame();
            var alerts = currentFrame?.Alerts ?? MedicalAlert.None;

            result.Add(new ClinicianPatientDeviceInfo(
                PatientId: patient.Id,
                PatientName: $"{patient.FirstName} {patient.LastName}".Trim(),
                HasActiveDevice: hasActiveDevice,
                Device: device,
                DeviceStatus: device?.Status,
                ActiveAlerts: alerts
            ));
        }

        _logger.LogDebug(
            "Retrieved {Count} patient device info records for clinician {ClinicianId}, {ActiveCount} with active devices",
            result.Count, clinicianId, result.Count(r => r.HasActiveDevice));

        return result;
    }

    /// <summary>
    /// Check if a device exists for a patient.
    /// </summary>
    public bool HasDevice(Guid patientId)
    {
        return _patientDevices.TryGetValue(patientId, out var device) &&
               device.Status != DeviceStatus.Disposed;
    }

    /// <summary>
    /// Get device by patient ID (no auth check - for internal use).
    /// </summary>
    public MockHeatmapDevice? TryGetDevice(Guid patientId)
    {
        if (_patientDevices.TryGetValue(patientId, out var device) &&
            device.Status != DeviceStatus.Disposed)
        {
            return device;
        }
        return null;
    }

    /// <summary>
    /// Stop and remove a specific patient's device.
    /// </summary>
    public async Task DisposeDeviceAsync(Guid patientId)
    {
        if (_patientDevices.TryRemove(patientId, out var device))
        {
            await device.DisposeAsync();
            _logger.LogInformation("Disposed mock device for patient {PatientId}", patientId);
        }
    }

    /// <summary>
    /// Get count of active devices.
    /// </summary>
    public int ActiveDeviceCount => _patientDevices.Count(kvp => kvp.Value.Status != DeviceStatus.Disposed);

    /// <summary>
    /// Get all active device summaries (for admin/monitoring purposes).
    /// </summary>
    public IReadOnlyList<(Guid PatientId, string DeviceId, DeviceStatus Status)> GetActiveDeviceSummaries()
    {
        return _patientDevices
            .Where(kvp => kvp.Value.Status != DeviceStatus.Disposed)
            .Select(kvp => (kvp.Key, kvp.Value.DeviceId, kvp.Value.Status))
            .ToList();
    }

    // Author: SID:2412494
    // Implemented actual PatientClinicianAssignments query to replace temporary all-patients implementation.
    /// <summary>
    /// Gets authorized patient IDs for a clinician based on PatientClinicianAssignments.
    /// </summary>
    /// <returns>List of patient IDs assigned to the clinician, empty if clinician is not approved.</returns>
    private async Task<IReadOnlyList<Guid>> GetAuthorizedPatientIdsForClinicianAsync(Guid clinicianId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Verify clinician is approved
        var clinician = await dbContext.Users.FindAsync(clinicianId);
        if (clinician?.UserType != "clinician" || clinician.ApprovedAt == null)
        {
            return Array.Empty<Guid>();
        }

        // Query PatientClinicianAssignments to get assigned patient IDs
        // Only include patients who are not deactivated
        return await dbContext.PatientClinicianAssignments
            .Where(a => a.ClinicianId == clinicianId)
            .Join(dbContext.Users,
                assignment => assignment.PatientId,
                user => user.Id,
                (assignment, user) => new { assignment.PatientId, user.DeactivatedAt })
            .Where(x => x.DeactivatedAt == null)
            .Select(x => x.PatientId)
            .ToListAsync();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MockDeviceManager));
        }
    }

    /// <summary>
    /// Dispose all managed devices.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _logger.LogInformation("Disposing MockDeviceManager with {Count} active devices", _patientDevices.Count);

        var disposeTasks = _patientDevices.Values.Select(d => d.DisposeAsync().AsTask());
        await Task.WhenAll(disposeTasks);

        _patientDevices.Clear();
        _createLock.Dispose();

        GC.SuppressFinalize(this);
    }
}
