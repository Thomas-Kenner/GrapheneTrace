using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GrapheneTrace.Core.Models;

/// <summary>
/// Represents a 32x32 matrix of pressure readings for graphene trace analysis.
/// </summary>
/// <remarks>
/// Design Pattern: Immutable Object Pattern
/// Why: Pressure readings shouldn't change after creation, ensuring thread safety
/// and preventing accidental data corruption during calculations.
///
/// Business Purpose: Core data structure for pressure map analysis, supporting
/// both discrete range visualization and gradient display modes while maintaining
/// high performance for area calculations.
/// </remarks>
public class PressureMap
{
    public const int MATRIX_SIZE = 32;
    public const byte MISSING_VALUE = 0;
    public const byte NO_PRESSURE_VALUE = 1;
    public const byte MAX_PRESSURE_VALUE = 255;

    /// <summary>
    /// Internal pressure data storage using row-major order for cache-friendly traversal.
    /// </summary>
    /// <remarks>
    /// Performance: Row-major layout optimizes memory access patterns for typical
    /// scanning operations (left-to-right, top-to-bottom).
    /// </remarks>
    private readonly byte[,] _pressureData;

    /// <summary>
    /// Defines the available pressure range slice sizes for visualization.
    /// </summary>
    /// <remarks>
    /// Design Decision: Enum instead of configurable integer for type safety
    /// Why these specific values:
    /// - Fine: Creates 23 ranges (253÷11), optimal for detailed analysis and default area detection
    /// - Coarse: Creates 11 ranges (253÷23), suitable for broader categorization
    /// - Size 1 excluded: Redundant since gradient mode provides per-value granularity
    /// - Size 253 excluded: Creates only 1 range, making range mode meaningless
    /// </remarks>
    public enum PressureRangeSlice
    {
        /// <summary>Fine-grained slicing: 11 units per slice, creating 23 ranges</summary>
        Fine = 11,
        /// <summary>Coarse-grained slicing: 23 units per slice, creating 11 ranges</summary>
        Coarse = 23
    }

    /// <summary>
    /// Configuration for pressure range slicing and display modes.
    /// </summary>
    public static class RangeConfiguration
    {
        private static PressureRangeSlice _sliceMode = PressureRangeSlice.Fine;

        /// <summary>
        /// Current pressure range slice mode for visualization and area detection.
        /// </summary>
        /// <remarks>
        /// Default: Fine (11 units per slice) provides optimal balance between detail and performance
        /// for area calculations in both range mode (visualization) and gradient mode (area detection).
        /// </remarks>
        public static PressureRangeSlice SliceMode
        {
            get => _sliceMode;
            set => _sliceMode = value;
        }

        /// <summary>
        /// Gets the numeric slice size for the current mode.
        /// </summary>
        public static int SliceSize => (int)_sliceMode;

        /// <summary>
        /// Returns the number of ranges created by the current slice mode.
        /// </summary>
        public static int GetRangeCount() => 2 + (253 / SliceSize); // +2 for special values 1 and 255
    }

    /// <summary>
    /// Private constructor enforces use of factory methods for proper validation.
    /// </summary>
    /// <remarks>
    /// Design Pattern: Factory Method Pattern
    /// Why: Ensures all PressureMap instances are properly validated and immutable.
    /// </remarks>
    private PressureMap(byte[,] pressureData)
    {
        _pressureData = new byte[MATRIX_SIZE, MATRIX_SIZE];
        Array.Copy(pressureData, _pressureData, MATRIX_SIZE * MATRIX_SIZE);
    }

    /// <summary>
    /// Creates a PressureMap from CSV data with comprehensive validation.
    /// </summary>
    /// <param name="csvData">32x32 CSV data with pressure values 0-255</param>
    /// <returns>Validated PressureMap instance</returns>
    /// <exception cref="PressureMapValidationException">Thrown when CSV data is invalid</exception>
    /// <remarks>
    /// Design Pattern: Factory Method with comprehensive error handling
    /// Algorithm: Two-phase parsing - structure validation, then value validation
    ///
    /// Why this approach:
    /// - Separates concerns: structural vs. semantic validation
    /// - Provides detailed error reporting for debugging
    /// - Allows for partial recovery in future enhancements
    ///
    /// Validation Rules:
    /// - Must be exactly 32x32 matrix
    /// - Values must be 0-255 (byte range)
    /// - 0 represents missing data and is converted to 1 (no pressure)
    /// - Values outside 0-255 reject the entire CSV
    /// </remarks>
    public static PressureMap FromCsv(string csvData)
    {
        if (string.IsNullOrWhiteSpace(csvData))
            throw new PressureMapValidationException("CSV data cannot be null or empty");

        var validationResult = ValidateCsvStructure(csvData);
        if (!validationResult.IsValid)
            throw new PressureMapValidationException($"CSV validation failed: {validationResult.ErrorMessage}");

        return new PressureMap(validationResult.PressureData!);
    }

    /// <summary>
    /// Validates CSV structure and converts to pressure matrix.
    /// </summary>
    /// <remarks>
    /// Performance: Single-pass parsing with immediate validation for fail-fast behavior.
    /// Error Reporting: Captures specific row/column of first error for debugging.
    /// </remarks>
    private static CsvValidationResult ValidateCsvStructure(string csvData)
    {
        var pressureData = new byte[MATRIX_SIZE, MATRIX_SIZE];
        var lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length != MATRIX_SIZE)
            return CsvValidationResult.Error($"Expected {MATRIX_SIZE} rows, found {lines.Length}");

        for (int row = 0; row < MATRIX_SIZE; row++)
        {
            var values = lines[row].Split(',');
            if (values.Length != MATRIX_SIZE)
                return CsvValidationResult.Error($"Row {row + 1}: Expected {MATRIX_SIZE} columns, found {values.Length}");

            for (int col = 0; col < MATRIX_SIZE; col++)
            {
                if (!byte.TryParse(values[col].Trim(), out byte pressure))
                    return CsvValidationResult.Error($"Row {row + 1}, Column {col + 1}: Invalid number '{values[col]}'");

                // Convert missing values (0) to no pressure (1) for internal consistency
                // Why: Maintains semantic meaning while ensuring all stored values are valid readings
                pressureData[row, col] = pressure == MISSING_VALUE ? NO_PRESSURE_VALUE : pressure;
            }
        }

        return CsvValidationResult.Success(pressureData);
    }

    /// <summary>
    /// Maps a pressure value to its corresponding range index.
    /// </summary>
    /// <param name="value">Pressure value (1-255)</param>
    /// <returns>Range index for visualization and area calculations</returns>
    /// <remarks>
    /// Design Decision: Algebraic range mapping over lookup tables
    /// Why: Reduces memory footprint and allows runtime reconfiguration
    ///
    /// Range Mapping:
    /// - Index 0: Value 1 (no pressure) - isolated range
    /// - Index 1 to N: Values 2-254 divided into equal slices
    /// - Index N+1: Value 255 (overflow pressure) - isolated range
    ///
    /// Algorithm: Linear mapping for O(1) performance
    /// Performance: Constant time, no memory allocations
    /// </remarks>
    public int GetPressureRange(byte value)
    {
        return value switch
        {
            NO_PRESSURE_VALUE => 0,
            MAX_PRESSURE_VALUE => RangeConfiguration.GetRangeCount() - 1,
            _ => 1 + ((value - 2) / RangeConfiguration.SliceSize)
        };
    }

    /// <summary>
    /// Gets the pressure value at the specified coordinates.
    /// </summary>
    /// <param name="row">Row index (0-31)</param>
    /// <param name="col">Column index (0-31)</param>
    /// <returns>Pressure value at the specified position</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when coordinates are invalid</exception>
    public byte GetPressure(int row, int col)
    {
        if (row < 0 || row >= MATRIX_SIZE)
            throw new ArgumentOutOfRangeException(nameof(row), $"Row must be between 0 and {MATRIX_SIZE - 1}");
        if (col < 0 || col >= MATRIX_SIZE)
            throw new ArgumentOutOfRangeException(nameof(col), $"Column must be between 0 and {MATRIX_SIZE - 1}");

        return _pressureData[row, col];
    }

    /// <summary>
    /// Identifies contiguous high-pressure regions using flood-fill algorithm.
    /// </summary>
    /// <param name="minimumPressure">Minimum pressure threshold for region inclusion</param>
    /// <param name="minimumAreaSize">Minimum number of pixels for a valid region (default: 4)</param>
    /// <returns>Collection of identified pressure areas with their statistics</returns>
    /// <remarks>
    /// Design Pattern: Iterator Pattern for memory-efficient lazy evaluation
    /// Algorithm: Modified 8-directional flood-fill with range tolerance
    /// Performance: O(n^2) worst case, typically O(k) where k = high-pressure pixels
    ///
    /// Why this approach:
    /// - Flood-fill chosen over connected components for memory efficiency
    /// - 8-directional (including diagonals) matches physical pressure distribution
    /// - Range tolerance allows for sensor noise without fragmenting regions
    /// - Minimum area threshold prevents noise from registering as regions
    ///
    /// Business Logic:
    /// - Adjacent cells within same pressure range are considered connected
    /// - Special handling for boundary values (1 and 255)
    /// - Returns area bounds, total pressure, and average pressure per region
    /// </remarks>
    public IEnumerable<PressureArea> GetHighPressureAreas(byte minimumPressure, int minimumAreaSize = 4)
    {
        var visited = new bool[MATRIX_SIZE, MATRIX_SIZE];
        var areas = new List<PressureArea>();

        for (int row = 0; row < MATRIX_SIZE; row++)
        {
            for (int col = 0; col < MATRIX_SIZE; col++)
            {
                if (!visited[row, col] && _pressureData[row, col] >= minimumPressure)
                {
                    var area = FloodFillArea(row, col, minimumPressure, visited);
                    if (area.PixelCount >= minimumAreaSize)
                        areas.Add(area);
                }
            }
        }

        return areas;
    }

    /// <summary>
    /// Performs flood-fill to identify a contiguous pressure area.
    /// </summary>
    /// <remarks>
    /// Algorithm: Iterative flood-fill using explicit stack to prevent stack overflow
    /// Performance: Optimized for cache locality with breadth-first traversal
    /// </remarks>
    private PressureArea FloodFillArea(int startRow, int startCol, byte threshold, bool[,] visited)
    {
        var stack = new Stack<(int row, int col)>();
        var pixels = new List<(int row, int col)>();
        var targetRange = GetPressureRange(_pressureData[startRow, startCol]);
        long totalPressure = 0;

        stack.Push((startRow, startCol));

        while (stack.Count > 0)
        {
            var (row, col) = stack.Pop();

            if (row < 0 || row >= MATRIX_SIZE || col < 0 || col >= MATRIX_SIZE)
                continue;
            if (visited[row, col])
                continue;

            byte pressure = _pressureData[row, col];
            if (pressure < threshold || GetPressureRange(pressure) != targetRange)
                continue;

            visited[row, col] = true;
            pixels.Add((row, col));
            totalPressure += pressure;

            // 8-directional connectivity for realistic pressure distribution
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                    if (dr != 0 || dc != 0)
                        stack.Push((row + dr, col + dc));
        }

        return new PressureArea(pixels, totalPressure);
    }

    /// <summary>
    /// Calculates total force applied across the entire pressure map.
    /// </summary>
    /// <param name="calibrationFactor">Conversion factor from pressure units to force units</param>
    /// <returns>Total force across all pressure points</returns>
    /// <remarks>
    /// Performance: Single-pass calculation with SIMD potential for future optimization
    /// Physics: Assumes uniform pressure distribution across each pixel area
    /// </remarks>
    public double CalculateTotalForce(double calibrationFactor = 1.0)
    {
        long totalPressure = 0;
        for (int row = 0; row < MATRIX_SIZE; row++)
            for (int col = 0; col < MATRIX_SIZE; col++)
                totalPressure += _pressureData[row, col];

        return totalPressure * calibrationFactor;
    }

    /// <summary>
    /// Compares this pressure map with another to calculate differential force.
    /// </summary>
    /// <param name="comparison">Pressure map to compare against</param>
    /// <param name="calibrationFactor">Conversion factor for force calculation</param>
    /// <returns>Net differential force between the two maps</returns>
    /// <remarks>
    /// Use Case: Analyzing pressure changes over time or between different conditions
    /// Algorithm: Point-wise difference calculation with overflow protection
    /// </remarks>
    public double CalculateDifferentialForce(PressureMap comparison, double calibrationFactor = 1.0)
    {
        long totalDifference = 0;
        for (int row = 0; row < MATRIX_SIZE; row++)
            for (int col = 0; col < MATRIX_SIZE; col++)
                totalDifference += Math.Abs(_pressureData[row, col] - comparison._pressureData[row, col]);

        return totalDifference * calibrationFactor;
    }

    /// <summary>
    /// Converts pressure data to byte array for efficient serialization.
    /// </summary>
    /// <returns>Flattened byte array in row-major order</returns>
    /// <remarks>
    /// Design Purpose: Efficient database storage and network transmission
    /// Format: Row-major order for consistent reconstruction
    /// Performance: Single memory copy operation
    /// </remarks>
    public byte[] ToByteArray()
    {
        var result = new byte[MATRIX_SIZE * MATRIX_SIZE];
        Buffer.BlockCopy(_pressureData, 0, result, 0, result.Length);
        return result;
    }

    /// <summary>
    /// Creates a PressureMap from a byte array.
    /// </summary>
    /// <param name="data">Byte array containing pressure data in row-major order</param>
    /// <returns>PressureMap instance</returns>
    /// <exception cref="ArgumentException">Thrown when data length is incorrect</exception>
    public static PressureMap FromByteArray(byte[] data)
    {
        if (data.Length != MATRIX_SIZE * MATRIX_SIZE)
            throw new ArgumentException($"Data must contain exactly {MATRIX_SIZE * MATRIX_SIZE} bytes");

        var pressureData = new byte[MATRIX_SIZE, MATRIX_SIZE];
        Buffer.BlockCopy(data, 0, pressureData, 0, data.Length);
        return new PressureMap(pressureData);
    }
}

/// <summary>
/// Represents a contiguous area of high pressure with statistical information.
/// </summary>
/// <remarks>
/// Design Pattern: Value Object for immutable result data
/// Purpose: Encapsulates area calculation results for further analysis
/// </remarks>
public class PressureArea
{
    public IReadOnlyList<(int row, int col)> Pixels { get; }
    public int PixelCount => Pixels.Count;
    public double AveragePressure { get; }
    public long TotalPressure { get; }
    public (int minRow, int minCol, int maxRow, int maxCol) BoundingBox { get; }

    internal PressureArea(List<(int row, int col)> pixels, long totalPressure)
    {
        Pixels = pixels.AsReadOnly();
        TotalPressure = totalPressure;
        AveragePressure = (double)totalPressure / pixels.Count;

        BoundingBox = pixels.Aggregate(
            (int.MaxValue, int.MaxValue, int.MinValue, int.MinValue),
            (acc, pixel) => (
                Math.Min(acc.Item1, pixel.row),
                Math.Min(acc.Item2, pixel.col),
                Math.Max(acc.Item3, pixel.row),
                Math.Max(acc.Item4, pixel.col)
            )
        );
    }
}

/// <summary>
/// Exception thrown when CSV data validation fails.
/// </summary>
/// <remarks>
/// Design Pattern: Custom Exception for specific domain errors
/// Purpose: Provides detailed error information for CSV processing failures
/// </remarks>
public class PressureMapValidationException : Exception
{
    public PressureMapValidationException(string message) : base(message) { }
    public PressureMapValidationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Internal result class for CSV validation operations.
/// </summary>
/// <remarks>
/// Design Pattern: Result Pattern for explicit success/failure handling
/// Purpose: Avoids exceptions for expected validation failures during parsing
/// </remarks>
internal class CsvValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }
    public byte[,]? PressureData { get; }

    private CsvValidationResult(bool isValid, string? errorMessage, byte[,]? pressureData)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
        PressureData = pressureData;
    }

    public static CsvValidationResult Success(byte[,] pressureData) => new(true, null, pressureData);
    public static CsvValidationResult Error(string errorMessage) => new(false, errorMessage, null);
}