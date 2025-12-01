using System.Text;

namespace GrapheneTrace.Web.Services.Mocking;

/// <summary>
/// Represents a single frame of heatmap data with metadata.
/// Author: SID:2412494
/// </summary>
public class HeatmapFrame
{
    private const int MatrixSize = 32;
    private const int TotalCells = MatrixSize * MatrixSize;

    /// <summary>
    /// Timestamp when frame was generated.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Sequential frame number since device start.
    /// </summary>
    public long FrameNumber { get; init; }

    /// <summary>
    /// Patient ID this frame belongs to.
    /// </summary>
    public Guid PatientId { get; init; }

    /// <summary>
    /// Device ID that generated this frame.
    /// </summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>
    /// 32x32 pressure values as flat array (row-major, 1024 elements).
    /// </summary>
    public int[] PressureData { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Active faults when frame was generated.
    /// </summary>
    public DeviceFault ActiveFaults { get; init; }

    /// <summary>
    /// Medical alerts detected in this frame.
    /// </summary>
    public MedicalAlert Alerts { get; init; }

    /// <summary>
    /// Peak pressure value in this frame.
    /// </summary>
    public int PeakPressure { get; init; }

    /// <summary>
    /// Percentage of sensors with non-zero readings (0-100).
    /// </summary>
    public float ContactAreaPercent { get; init; }

    /// <summary>
    /// Current simulation scenario.
    /// </summary>
    public SimulationScenario Scenario { get; init; }

    /// <summary>
    /// Convert pressure data to CSV string for database storage.
    /// Format: 32 rows of 32 comma-separated values, newline-separated.
    /// Matches PatientSnapshotData.SnapshotData format.
    /// </summary>
    public string ToCsvString()
    {
        if (PressureData.Length != TotalCells)
        {
            throw new InvalidOperationException(
                $"PressureData must contain exactly {TotalCells} elements, but has {PressureData.Length}");
        }

        var sb = new StringBuilder();
        for (int row = 0; row < MatrixSize; row++)
        {
            for (int col = 0; col < MatrixSize; col++)
            {
                if (col > 0) sb.Append(',');
                sb.Append(PressureData[row * MatrixSize + col]);
            }
            if (row < MatrixSize - 1) sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>
    /// Get a copy of the pressure data array.
    /// </summary>
    public int[] GetPressureDataCopy()
    {
        var copy = new int[PressureData.Length];
        Array.Copy(PressureData, copy, PressureData.Length);
        return copy;
    }
}
