namespace GrapheneTrace.Web.Services.Mocking;

/// <summary>
/// Simulation scenarios for pressure pattern generation.
/// Adapted from CLI-demo MockPressureDataGenerator.
/// Author: SID:2412494
/// </summary>
public enum SimulationScenario
{
    /// <summary>Normal sitting with gradual pressure buildup</summary>
    NormalSitting,

    /// <summary>Pressure buildup leading to alert condition</summary>
    PressureBuildupAlert,

    /// <summary>Weight shifting to redistribute pressure</summary>
    WeightShiftingRelief,

    /// <summary>Multiple scenarios cycling automatically (60-second cycle)</summary>
    SequentialDemo,

    /// <summary>Static pattern - no time-based changes (for testing)</summary>
    Static
}

/// <summary>
/// Device operational status.
/// Author: SID:2412494
/// </summary>
public enum DeviceStatus
{
    /// <summary>Device created but not streaming</summary>
    Idle,

    /// <summary>Device is actively generating frames</summary>
    Running,

    /// <summary>Device encountered an error state</summary>
    Error,

    /// <summary>Device has been disposed</summary>
    Disposed
}

/// <summary>
/// Fault types that can be simulated. Can be combined using flags.
/// Author: SID:2412494
/// </summary>
[Flags]
public enum DeviceFault
{
    /// <summary>No faults active</summary>
    None = 0,

    /// <summary>Random pixels return 0 (sensor element failure)</summary>
    DeadPixels = 1 << 0,

    /// <summary>Device stops responding (connection lost) - all values return 0</summary>
    Disconnected = 1 << 1,

    /// <summary>Some rows/columns missing (partial data)</summary>
    PartialDataLoss = 1 << 2,

    /// <summary>Values drift higher over time (sensor calibration issue)</summary>
    CalibrationDrift = 1 << 3,

    /// <summary>Random noise spikes in readings</summary>
    ElectricalNoise = 1 << 4,

    /// <summary>All values read maximum (sensor saturation)</summary>
    Saturation = 1 << 5
}

/// <summary>
/// Medical alert conditions detected in pressure data. Can be combined using flags.
/// Author: SID:2412494
/// </summary>
[Flags]
public enum MedicalAlert
{
    /// <summary>No alerts active</summary>
    None = 0,

    /// <summary>Pressure exceeds high threshold at any point</summary>
    HighPressure = 1 << 0,

    /// <summary>Sustained high pressure over time window</summary>
    SustainedPressure = 1 << 1,

    /// <summary>Pressure pattern suggests poor positioning</summary>
    PositioningWarning = 1 << 2,

    /// <summary>Patient-specific threshold breached</summary>
    ThresholdBreach = 1 << 3
}
