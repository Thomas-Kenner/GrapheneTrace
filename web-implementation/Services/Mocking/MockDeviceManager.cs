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
    /// Get all devices the clinician is authorized to view.
    /// </summary>
    /// <param name="clinicianId">Clinician user ID</param>
    /// <returns>Collection of authorized mock devices</returns>
    /// <remarks>
    /// TODO: When PatientClinicianAssignments table is implemented, update this method
    /// to only return devices for assigned patients.
    /// Currently returns all active devices for any approved clinician.
    /// </remarks>
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

    /// <summary>
    /// Gets authorized patient IDs for a clinician.
    /// </summary>
    /// <remarks>
    /// TODO: Update this when PatientClinicianAssignments table is implemented.
    /// Current implementation: All approved clinicians can access all patients.
    /// </remarks>
    private async Task<IReadOnlyList<Guid>> GetAuthorizedPatientIdsForClinicianAsync(Guid clinicianId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Verify clinician is approved
        var clinician = await dbContext.Users.FindAsync(clinicianId);
        if (clinician?.UserType != "clinician" || clinician.ApprovedAt == null)
        {
            return Array.Empty<Guid>();
        }

        // TODO: Replace with PatientClinicianAssignments query when table exists
        // return await dbContext.PatientClinicianAssignments
        //     .Where(a => a.ClinicianId == clinicianId)
        //     .Select(a => a.PatientId)
        //     .ToListAsync();

        // Temporary: Return all active patient IDs
        return await dbContext.Users
            .Where(u => u.UserType == "patient" && u.DeactivatedAt == null)
            .Select(u => u.Id)
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
