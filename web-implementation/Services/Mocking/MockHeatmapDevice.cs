using GrapheneTrace.Web.Models;
using Microsoft.Extensions.Logging;

namespace GrapheneTrace.Web.Services.Mocking;

/// <summary>
/// Simulates a live pressure sensor device for a specific patient.
/// Generates realistic 32x32 pressure matrices at configurable frame rates.
/// Adapted from CLI-demo MockPressureDataGenerator with added fault injection.
/// Author: SID:2412494
/// </summary>
public class MockHeatmapDevice : IAsyncDisposable
{
    private const int MatrixSize = 32;
    private const int TotalCells = MatrixSize * MatrixSize;

    // Anatomical position constants (matrix coordinates, not pressure values)
    private const int IschialLeftRowStart = 12;
    private const int IschialLeftRowEnd = 20;
    private const int IschialLeftColStart = 8;
    private const int IschialLeftColEnd = 12;

    private const int IschialRightRowStart = 12;
    private const int IschialRightRowEnd = 20;
    private const int IschialRightColStart = 20;
    private const int IschialRightColEnd = 24;

    private const int ThighRowStart = 8;
    private const int ThighRowEnd = 25;
    private const int ThighColStart = 6;
    private const int ThighColEnd = 26;

    // Noise factor (percentage, not absolute)
    private const double NoiseFactor = 0.15;
    private const double PressureBuildupRate = 2.5;

    // Dependencies
    private readonly ILogger<MockHeatmapDevice> _logger;
    private readonly PressureThresholdsConfig _config;
    private readonly Random _random;
    private readonly object _stateLock = new();

    // Identity
    public Guid PatientId { get; }
    public string DeviceId { get; }

    // Configuration-driven pressure values (computed from config)
    private int Range => _config.MaxValue - _config.MinValue;
    private int BasePressure => _config.MinValue + (int)(Range * 0.12);
    private int ThighMaxPressure => _config.MinValue + (int)(Range * 0.31);
    private int IschialMaxPressure => _config.MinValue + (int)(Range * 0.71);
    private int AlertThreshold => _config.DefaultHighThreshold;
    private int LowAlertThreshold => _config.DefaultLowThreshold;

    // State
    private DeviceStatus _status = DeviceStatus.Idle;
    private SimulationScenario _currentScenario = SimulationScenario.NormalSitting;
    private DeviceFault _activeFaults = DeviceFault.None;
    private int _currentPhase;

    // Frame generation
    private PeriodicTimer? _frameTimer;
    private CancellationTokenSource? _cts;
    private Task? _generationTask;
    private double _framesPerSecond = 15.0;
    private long _frameNumber;
    private DateTime _startTime;
    private TimeSpan _scenarioStartTime;
    private HeatmapFrame? _currentFrame;

    // Auto-fault configuration
    private double _autoFaultProbability;
    private TimeSpan _autoFaultDuration = TimeSpan.FromSeconds(5);
    private DateTime? _autoFaultEndTime;
    private DeviceFault _autoInjectedFault = DeviceFault.None;

    // Sustained pressure tracking for alerts
    private int _sustainedHighPressureFrames;
    private const int SustainedPressureThresholdFrames = 45; // ~3 seconds at 15 FPS

    // Calibration drift tracking
    private int _calibrationDriftOffset;

    /// <summary>
    /// Current device status.
    /// </summary>
    public DeviceStatus Status
    {
        get { lock (_stateLock) return _status; }
        private set
        {
            lock (_stateLock)
            {
                if (_status != value)
                {
                    _status = value;
                }
            }
            OnStatusChanged?.Invoke(value);
        }
    }

    /// <summary>
    /// Current simulation scenario.
    /// </summary>
    public SimulationScenario CurrentScenario
    {
        get { lock (_stateLock) return _currentScenario; }
    }

    /// <summary>
    /// Active faults (both manual and auto-injected).
    /// </summary>
    public DeviceFault ActiveFaults
    {
        get { lock (_stateLock) return _activeFaults | _autoInjectedFault; }
    }

    /// <summary>
    /// Current frame rate in frames per second.
    /// </summary>
    public double FramesPerSecond => _framesPerSecond;

    /// <summary>
    /// Total frames generated since start.
    /// </summary>
    public long TotalFrames => _frameNumber;

    /// <summary>
    /// Event raised when a new frame is generated.
    /// </summary>
    public event Action<HeatmapFrame>? OnFrameGenerated;

    /// <summary>
    /// Event raised when device status changes.
    /// </summary>
    public event Action<DeviceStatus>? OnStatusChanged;

    /// <summary>
    /// Event raised when a medical alert is detected.
    /// </summary>
    public event Action<MedicalAlert, HeatmapFrame>? OnAlertDetected;

    /// <summary>
    /// Creates a new mock heatmap device for a patient.
    /// </summary>
    /// <param name="patientId">Patient user ID</param>
    /// <param name="thresholdsConfig">Pressure thresholds configuration</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="deviceId">Optional device ID (if null, generates a new unique ID)</param>
    /// <param name="seed">Optional random seed for reproducible patterns</param>
    /// Author: SID:2412494 - Added deviceId parameter to support persisting device IDs across sessions
    internal MockHeatmapDevice(
        Guid patientId,
        PressureThresholdsConfig thresholdsConfig,
        ILogger<MockHeatmapDevice> logger,
        string? deviceId = null,
        int? seed = null)
    {
        PatientId = patientId;
        _config = thresholdsConfig;
        _logger = logger;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
        DeviceId = deviceId ?? GenerateDeviceId();
        _startTime = DateTime.UtcNow;
        _scenarioStartTime = TimeSpan.Zero;

        _logger.LogInformation(
            "Created mock device {DeviceId} for patient {PatientId} with range {Min}-{Max} (deviceId was {Source})",
            DeviceId, PatientId, _config.MinValue, _config.MaxValue, deviceId != null ? "provided" : "generated");
    }

    // Author: SID:2412494
    // Generates a unique device ID in hex format (e.g., "a1b2c3d4")
    private string GenerateDeviceId()
    {
        var bytes = new byte[4];
        _random.NextBytes(bytes);
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Generates a unique device ID that doesn't exist in the database.
    /// Used by MockDeviceManager when creating devices for patients with no prior sessions.
    /// Author: SID:2412494
    /// </summary>
    public static string GenerateUniqueDeviceId()
    {
        var random = new Random();
        var bytes = new byte[4];
        random.NextBytes(bytes);
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    #region Output Methods

    /// <summary>
    /// Get current frame as flat int array for JS canvas rendering.
    /// Thread-safe polling method.
    /// </summary>
    public int[] GetCurrentFrameAsArray()
    {
        lock (_stateLock)
        {
            return _currentFrame?.GetPressureDataCopy() ?? new int[TotalCells];
        }
    }

    /// <summary>
    /// Get current frame as CSV string for database storage.
    /// Format matches PatientSnapshotData.SnapshotData.
    /// </summary>
    public string GetCurrentFrameAsCsv()
    {
        lock (_stateLock)
        {
            return _currentFrame?.ToCsvString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Get current frame with full metadata.
    /// </summary>
    public HeatmapFrame? GetCurrentFrame()
    {
        lock (_stateLock)
        {
            return _currentFrame;
        }
    }

    #endregion

    #region Control Methods

    /// <summary>
    /// Start streaming frames at configured FPS.
    /// </summary>
    public void Start()
    {
        lock (_stateLock)
        {
            if (_status == DeviceStatus.Running)
            {
                _logger.LogDebug("Device {DeviceId} already running", DeviceId);
                return;
            }

            if (_status == DeviceStatus.Disposed)
            {
                throw new ObjectDisposedException(nameof(MockHeatmapDevice));
            }
        }

        _startTime = DateTime.UtcNow;
        _scenarioStartTime = TimeSpan.Zero;
        _frameNumber = 0;
        _calibrationDriftOffset = 0;

        _cts = new CancellationTokenSource();
        _frameTimer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / _framesPerSecond));
        _generationTask = RunFrameGenerationLoopAsync(_cts.Token);

        Status = DeviceStatus.Running;
        _logger.LogInformation("Device {DeviceId} started at {FPS} FPS", DeviceId, _framesPerSecond);
    }

    /// <summary>
    /// Stop streaming (can be resumed with Start).
    /// </summary>
    public async Task StopAsync()
    {
        if (_cts == null) return;

        _logger.LogInformation("Stopping device {DeviceId}", DeviceId);

        _cts.Cancel();

        if (_generationTask != null)
        {
            try
            {
                await _generationTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _frameTimer?.Dispose();
        _frameTimer = null;
        _cts.Dispose();
        _cts = null;
        _generationTask = null;

        Status = DeviceStatus.Idle;
    }

    /// <summary>
    /// Set the simulation scenario.
    /// </summary>
    public void SetScenario(SimulationScenario scenario)
    {
        lock (_stateLock)
        {
            _currentScenario = scenario;
            _scenarioStartTime = DateTime.UtcNow - _startTime;
            _currentPhase = 0;
        }
        _logger.LogDebug("Device {DeviceId} scenario set to {Scenario}", DeviceId, scenario);
    }

    /// <summary>
    /// Set frame generation rate.
    /// </summary>
    public async Task SetFrameRateAsync(double fps)
    {
        if (fps <= 0 || fps > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(fps), "FPS must be between 0 and 60");
        }

        var wasRunning = Status == DeviceStatus.Running;
        if (wasRunning)
        {
            await StopAsync();
        }

        _framesPerSecond = fps;

        if (wasRunning)
        {
            Start();
        }
    }

    #endregion

    #region Fault Injection

    /// <summary>
    /// Manually inject a fault condition.
    /// </summary>
    /// <param name="fault">Fault type to inject</param>
    /// <param name="duration">How long fault persists (null = permanent until cleared)</param>
    public void InjectFault(DeviceFault fault, TimeSpan? duration = null)
    {
        lock (_stateLock)
        {
            _activeFaults |= fault;
        }

        _logger.LogWarning("Device {DeviceId} fault injected: {Fault}", DeviceId, fault);

        if (duration.HasValue)
        {
            // Schedule fault removal
            _ = Task.Delay(duration.Value).ContinueWith(_ =>
            {
                lock (_stateLock)
                {
                    _activeFaults &= ~fault;
                }
                _logger.LogInformation("Device {DeviceId} fault cleared: {Fault}", DeviceId, fault);
            });
        }
    }

    /// <summary>
    /// Clear all manually injected faults.
    /// </summary>
    public void ClearFaults()
    {
        lock (_stateLock)
        {
            _activeFaults = DeviceFault.None;
            _calibrationDriftOffset = 0;
        }
        _logger.LogInformation("Device {DeviceId} all faults cleared", DeviceId);
    }

    /// <summary>
    /// Configure automatic random fault injection.
    /// </summary>
    /// <param name="probability">Probability (0.0-1.0) of fault occurring per frame</param>
    /// <param name="duration">Duration of auto-injected faults</param>
    public void SetAutoFaultConfig(double probability, TimeSpan duration)
    {
        if (probability < 0 || probability > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(probability), "Must be between 0 and 1");
        }

        _autoFaultProbability = probability;
        _autoFaultDuration = duration;
    }

    #endregion

    #region Frame Generation

    private async Task RunFrameGenerationLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _frameTimer != null)
            {
                if (!await _frameTimer.WaitForNextTickAsync(ct))
                    break;

                CheckAutoFaults();
                var frame = GenerateFrame();

                lock (_stateLock)
                {
                    _currentFrame = frame;
                    _frameNumber++;
                }

                // Raise events outside lock
                OnFrameGenerated?.Invoke(frame);

                if (frame.Alerts != MedicalAlert.None)
                {
                    OnAlertDetected?.Invoke(frame.Alerts, frame);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in frame generation loop for device {DeviceId}", DeviceId);
            Status = DeviceStatus.Error;
        }
    }

    private void CheckAutoFaults()
    {
        // Clear expired auto-fault
        if (_autoFaultEndTime.HasValue && DateTime.UtcNow >= _autoFaultEndTime.Value)
        {
            _autoInjectedFault = DeviceFault.None;
            _autoFaultEndTime = null;
        }

        // Potentially inject new auto-fault
        if (_autoFaultProbability > 0 && _autoInjectedFault == DeviceFault.None)
        {
            if (_random.NextDouble() < _autoFaultProbability)
            {
                var faultTypes = new[]
                {
                    DeviceFault.DeadPixels,
                    DeviceFault.PartialDataLoss,
                    DeviceFault.CalibrationDrift,
                    DeviceFault.ElectricalNoise
                };

                _autoInjectedFault = faultTypes[_random.Next(faultTypes.Length)];
                _autoFaultEndTime = DateTime.UtcNow + _autoFaultDuration;

                _logger.LogWarning("Device {DeviceId} auto-fault triggered: {Fault}", DeviceId, _autoInjectedFault);
            }
        }
    }

    private HeatmapFrame GenerateFrame()
    {
        var elapsedTime = DateTime.UtcNow - _startTime;
        var scenarioTime = elapsedTime - _scenarioStartTime;

        var pressureData = new int[TotalCells];

        // Check for disconnected fault first
        var currentFaults = ActiveFaults;
        if (currentFaults.HasFlag(DeviceFault.Disconnected))
        {
            // Return all zeros
            return CreateFrame(pressureData, currentFaults, MedicalAlert.None);
        }

        // Check for saturation fault
        if (currentFaults.HasFlag(DeviceFault.Saturation))
        {
            Array.Fill(pressureData, _config.MaxValue);
            return CreateFrame(pressureData, currentFaults, MedicalAlert.HighPressure);
        }

        // Generate base anatomical pattern
        GenerateAnatomicalBasePattern(pressureData);

        // Apply scenario-specific modifications
        ApplyScenarioEffects(pressureData, scenarioTime);

        // Add realistic noise and micro-movements
        ApplyNoiseAndVariation(pressureData);

        // Apply fault effects
        ApplyFaultEffects(pressureData, currentFaults);

        // Clamp all values to valid range
        ClampPressureValues(pressureData);

        // Detect alerts
        var alerts = DetectAlerts(pressureData);

        return CreateFrame(pressureData, currentFaults, alerts);
    }

    private HeatmapFrame CreateFrame(int[] pressureData, DeviceFault faults, MedicalAlert alerts)
    {
        var peakPressure = pressureData.Max();
        var contactCells = pressureData.Count(p => p > BasePressure);
        var contactPercent = (float)contactCells / TotalCells * 100f;

        return new HeatmapFrame
        {
            Timestamp = DateTime.UtcNow,
            FrameNumber = _frameNumber,
            PatientId = PatientId,
            DeviceId = DeviceId,
            PressureData = pressureData,
            ActiveFaults = faults,
            Alerts = alerts,
            PeakPressure = peakPressure,
            ContactAreaPercent = contactPercent,
            Scenario = _currentScenario
        };
    }

    #endregion

    #region Anatomical Pattern Generation

    private void GenerateAnatomicalBasePattern(int[] pressureData)
    {
        // Initialize with minimum value (no pressure)
        Array.Fill(pressureData, _config.MinValue);

        // Generate thigh contact area (broader, lower pressure)
        GenerateThighContactArea(pressureData);

        // Generate ischial tuberosity pressure points (concentrated, high pressure)
        GenerateIschialTuberositiesPattern(pressureData);
    }

    private void GenerateThighContactArea(int[] pressureData)
    {
        var centerRow = (ThighRowStart + ThighRowEnd) / 2;
        var centerCol = (ThighColStart + ThighColEnd) / 2;

        for (int row = ThighRowStart; row <= ThighRowEnd; row++)
        {
            for (int col = ThighColStart; col <= ThighColEnd; col++)
            {
                var rowDistance = Math.Abs(row - centerRow);
                var colDistance = Math.Abs(col - centerCol);

                // Elliptical pressure distribution
                var normalizedDistance = Math.Sqrt(
                    Math.Pow(rowDistance / 8.0, 2) + Math.Pow(colDistance / 10.0, 2)
                );

                if (normalizedDistance <= 1.0)
                {
                    var pressure = BasePressure +
                                   (ThighMaxPressure - BasePressure) * (1.0 - normalizedDistance);

                    var idx = row * MatrixSize + col;
                    pressureData[idx] = Math.Max(_config.MinValue, (int)pressure);
                }
            }
        }
    }

    private void GenerateIschialTuberositiesPattern(int[] pressureData)
    {
        // Left ischial tuberosity (slightly lower pressure for asymmetry)
        GenerateIschialPoint(pressureData,
            IschialLeftRowStart, IschialLeftRowEnd,
            IschialLeftColStart, IschialLeftColEnd,
            0.95);

        // Right ischial tuberosity (full pressure)
        GenerateIschialPoint(pressureData,
            IschialRightRowStart, IschialRightRowEnd,
            IschialRightColStart, IschialRightColEnd,
            1.0);
    }

    private void GenerateIschialPoint(int[] pressureData,
        int rowStart, int rowEnd, int colStart, int colEnd,
        double intensityMultiplier)
    {
        var centerRow = (rowStart + rowEnd) / 2;
        var centerCol = (colStart + colEnd) / 2;
        var maxPressure = IschialMaxPressure * intensityMultiplier;

        for (int row = rowStart; row <= rowEnd; row++)
        {
            for (int col = colStart; col <= colEnd; col++)
            {
                var distance = Math.Sqrt(
                    Math.Pow(row - centerRow, 2) + Math.Pow(col - centerCol, 2)
                );

                var maxDistance = Math.Max(rowEnd - rowStart, colEnd - colStart) / 2.0;
                var normalizedDistance = Math.Min(1.0, distance / maxDistance);

                // Exponential falloff for concentrated pressure
                var pressure = maxPressure * Math.Exp(-normalizedDistance * 3.0);

                var idx = row * MatrixSize + col;
                pressureData[idx] = Math.Max(pressureData[idx], (int)pressure);
            }
        }
    }

    #endregion

    #region Scenario Effects

    private void ApplyScenarioEffects(int[] pressureData, TimeSpan scenarioTime)
    {
        switch (_currentScenario)
        {
            case SimulationScenario.NormalSitting:
                ApplyNormalSittingProgression(pressureData, scenarioTime);
                break;

            case SimulationScenario.PressureBuildupAlert:
                ApplyPressureBuildupProgression(pressureData, scenarioTime);
                break;

            case SimulationScenario.WeightShiftingRelief:
                ApplyWeightShiftingPattern(pressureData, scenarioTime);
                break;

            case SimulationScenario.SequentialDemo:
                ApplySequentialDemo(pressureData, scenarioTime);
                break;

            case SimulationScenario.Static:
                // No time-based changes
                break;
        }
    }

    private void ApplyNormalSittingProgression(int[] pressureData, TimeSpan scenarioTime)
    {
        var pressureIncrease = (int)(scenarioTime.TotalSeconds * PressureBuildupRate * 0.5);
        ApplyPressureIncrease(pressureData, pressureIncrease);
    }

    private void ApplyPressureBuildupProgression(int[] pressureData, TimeSpan scenarioTime)
    {
        var pressureIncrease = (int)(scenarioTime.TotalSeconds * PressureBuildupRate * 2.0);
        ApplyPressureIncrease(pressureData, pressureIncrease);
    }

    private void ApplyWeightShiftingPattern(int[] pressureData, TimeSpan scenarioTime)
    {
        var shiftCycle = scenarioTime.TotalSeconds % 10.0;
        var leftWeight = 0.5 + 0.3 * Math.Sin(shiftCycle * Math.PI / 5.0);
        var rightWeight = 1.0 - leftWeight;

        ApplyAsymmetricPressure(pressureData, leftWeight, rightWeight);
    }

    private void ApplySequentialDemo(int[] pressureData, TimeSpan scenarioTime)
    {
        var phaseTime = scenarioTime.TotalSeconds % 60.0;

        if (phaseTime < 15.0)
        {
            _currentPhase = 1;
            ApplyNormalSittingProgression(pressureData, TimeSpan.FromSeconds(phaseTime));
        }
        else if (phaseTime < 30.0)
        {
            _currentPhase = 2;
            ApplyPressureBuildupProgression(pressureData, TimeSpan.FromSeconds(phaseTime - 15.0));
        }
        else if (phaseTime < 45.0)
        {
            _currentPhase = 3;
            ApplyWeightShiftingPattern(pressureData, TimeSpan.FromSeconds(phaseTime - 30.0));
        }
        else
        {
            _currentPhase = 4;
            ApplyNormalSittingProgression(pressureData, TimeSpan.FromSeconds(phaseTime - 45.0));
        }
    }

    private void ApplyPressureIncrease(int[] pressureData, int increase)
    {
        for (int i = 0; i < pressureData.Length; i++)
        {
            if (pressureData[i] > BasePressure)
            {
                pressureData[i] = Math.Min(_config.MaxValue, pressureData[i] + increase);
            }
        }
    }

    private void ApplyAsymmetricPressure(int[] pressureData, double leftWeight, double rightWeight)
    {
        var midCol = MatrixSize / 2;

        for (int row = 0; row < MatrixSize; row++)
        {
            for (int col = 0; col < MatrixSize; col++)
            {
                var idx = row * MatrixSize + col;
                if (pressureData[idx] > BasePressure)
                {
                    var weight = col < midCol ? leftWeight : rightWeight;
                    pressureData[idx] = (int)(pressureData[idx] * weight);
                }
            }
        }
    }

    #endregion

    #region Noise and Variation

    private void ApplyNoiseAndVariation(int[] pressureData)
    {
        for (int i = 0; i < pressureData.Length; i++)
        {
            if (pressureData[i] > _config.MinValue)
            {
                var basePressure = pressureData[i];
                var noiseRange = basePressure * NoiseFactor;
                var noise = (_random.NextDouble() - 0.5) * noiseRange * 2.0;

                pressureData[i] = Math.Max(_config.MinValue, (int)(basePressure + noise));
            }
        }
    }

    #endregion

    #region Fault Effects

    private void ApplyFaultEffects(int[] pressureData, DeviceFault faults)
    {
        if (faults.HasFlag(DeviceFault.DeadPixels))
        {
            // ~5% of pixels randomly return 0
            for (int i = 0; i < pressureData.Length; i++)
            {
                if (_random.NextDouble() < 0.05)
                {
                    pressureData[i] = 0;
                }
            }
        }

        if (faults.HasFlag(DeviceFault.PartialDataLoss))
        {
            // Zero out random rows
            var startRow = _random.Next(0, 28);
            var rowCount = _random.Next(2, 5);
            for (int row = startRow; row < Math.Min(startRow + rowCount, MatrixSize); row++)
            {
                for (int col = 0; col < MatrixSize; col++)
                {
                    pressureData[row * MatrixSize + col] = 0;
                }
            }
        }

        if (faults.HasFlag(DeviceFault.CalibrationDrift))
        {
            // Progressive drift over time (increases every ~20 frames)
            if (_frameNumber % 20 == 0)
            {
                _calibrationDriftOffset = Math.Min(
                    (int)(Range * 0.6), // Max 60% of range
                    _calibrationDriftOffset + (int)(Range * 0.02)); // +2% per increment
            }

            for (int i = 0; i < pressureData.Length; i++)
            {
                pressureData[i] = Math.Min(_config.MaxValue, pressureData[i] + _calibrationDriftOffset);
            }
        }

        if (faults.HasFlag(DeviceFault.ElectricalNoise))
        {
            // Random spike values
            for (int i = 0; i < pressureData.Length; i++)
            {
                if (_random.NextDouble() < 0.02)
                {
                    // Spike to random high value
                    pressureData[i] = _random.Next((int)(Range * 0.7), _config.MaxValue);
                }
            }
        }
    }

    #endregion

    #region Alert Detection

    private MedicalAlert DetectAlerts(int[] pressureData)
    {
        var alerts = MedicalAlert.None;
        var peakPressure = pressureData.Max();

        // High pressure alert
        if (peakPressure >= AlertThreshold)
        {
            alerts |= MedicalAlert.HighPressure;
            _sustainedHighPressureFrames++;
        }
        else
        {
            _sustainedHighPressureFrames = 0;
        }

        // Sustained pressure alert
        if (_sustainedHighPressureFrames >= SustainedPressureThresholdFrames)
        {
            alerts |= MedicalAlert.SustainedPressure;
        }

        // Threshold breach (any pressure above configured high threshold)
        if (peakPressure >= _config.DefaultHighThreshold)
        {
            alerts |= MedicalAlert.ThresholdBreach;
        }

        // Positioning warning (asymmetric pressure distribution)
        var leftPressure = CalculateRegionPressure(pressureData, 0, MatrixSize / 2);
        var rightPressure = CalculateRegionPressure(pressureData, MatrixSize / 2, MatrixSize);
        var asymmetryRatio = Math.Max(leftPressure, rightPressure) /
                             Math.Max(1.0, Math.Min(leftPressure, rightPressure));

        if (asymmetryRatio > 2.0)
        {
            alerts |= MedicalAlert.PositioningWarning;
        }

        return alerts;
    }

    private double CalculateRegionPressure(int[] pressureData, int colStart, int colEnd)
    {
        double sum = 0;
        int count = 0;

        for (int row = 0; row < MatrixSize; row++)
        {
            for (int col = colStart; col < colEnd; col++)
            {
                var value = pressureData[row * MatrixSize + col];
                if (value > _config.MinValue)
                {
                    sum += value;
                    count++;
                }
            }
        }

        return count > 0 ? sum / count : 0;
    }

    #endregion

    #region Helpers

    private void ClampPressureValues(int[] pressureData)
    {
        for (int i = 0; i < pressureData.Length; i++)
        {
            pressureData[i] = Math.Clamp(pressureData[i], _config.MinValue, _config.MaxValue);
        }
    }

    /// <summary>
    /// Gets the current scenario and its progress information.
    /// </summary>
    public (SimulationScenario scenario, int phase, double progressPercent) GetScenarioStatus()
    {
        var elapsedTime = DateTime.UtcNow - _startTime - _scenarioStartTime;
        var totalDuration = GetScenarioDuration(_currentScenario);
        var progress = Math.Min(100.0, (elapsedTime.TotalSeconds / totalDuration.TotalSeconds) * 100.0);

        return (_currentScenario, _currentPhase, progress);
    }

    private static TimeSpan GetScenarioDuration(SimulationScenario scenario)
    {
        return scenario switch
        {
            SimulationScenario.NormalSitting => TimeSpan.FromMinutes(2),
            SimulationScenario.PressureBuildupAlert => TimeSpan.FromSeconds(45),
            SimulationScenario.WeightShiftingRelief => TimeSpan.FromSeconds(30),
            SimulationScenario.SequentialDemo => TimeSpan.FromMinutes(1),
            SimulationScenario.Static => TimeSpan.MaxValue,
            _ => TimeSpan.FromMinutes(1)
        };
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Disposes the device and releases resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_status == DeviceStatus.Disposed)
            return;

        _logger.LogInformation("Disposing mock device {DeviceId}", DeviceId);

        await StopAsync();

        Status = DeviceStatus.Disposed;

        GC.SuppressFinalize(this);
    }

    #endregion
}
