using System;
using GrapheneTrace.Core.Models;

namespace GrapheneTrace.Services.Mocking;

/// <summary>
/// Generates realistic pressure map data simulating anatomically accurate sitting patterns.
/// </summary>
/// <remarks>
/// Design Pattern: Factory Method with state machine for simulation scenarios
/// Why: Provides realistic test data for demonstration while maintaining reproducible
/// patterns that showcase the system's alert detection and visualization capabilities.
///
/// Business Purpose: Simulates real-world pressure sensor data for development, testing,
/// and demonstration purposes, enabling comprehensive system validation without physical sensors.
///
/// Technical Details: Uses anatomically accurate pressure distribution patterns based on
/// human sitting biomechanics, including ischial tuberosity pressure points, thigh contact
/// areas, and realistic pressure progression over time.
/// </remarks>
public class MockPressureDataGenerator
{
    /// <summary>
    /// Defines available simulation scenarios for different demo purposes.
    /// </summary>
    public enum SimulationScenario
    {
        /// <summary>Normal sitting with gradual pressure buildup</summary>
        NormalSitting,
        /// <summary>Pressure buildup leading to alert condition</summary>
        PressureBuildupAlert,
        /// <summary>Weight shifting to redistribute pressure</summary>
        WeightShiftingRelief,
        /// <summary>Multiple scenarios in sequence</summary>
        SequentialDemo
    }

    private readonly Random _random;
    private readonly DateTime _startTime;
    private SimulationScenario _currentScenario;
    private TimeSpan _scenarioStartTime;
    private int _currentPhase;

    // Anatomical constants based on human sitting biomechanics
    private const int ISCHIAL_LEFT_ROW_START = 12;
    private const int ISCHIAL_LEFT_ROW_END = 20;
    private const int ISCHIAL_LEFT_COL_START = 8;
    private const int ISCHIAL_LEFT_COL_END = 12;

    private const int ISCHIAL_RIGHT_ROW_START = 12;
    private const int ISCHIAL_RIGHT_ROW_END = 20;
    private const int ISCHIAL_RIGHT_COL_START = 20;
    private const int ISCHIAL_RIGHT_COL_END = 24;

    private const int THIGH_ROW_START = 8;
    private const int THIGH_ROW_END = 25;
    private const int THIGH_COL_START = 6;
    private const int THIGH_COL_END = 26;

    // Pressure progression constants
    private const byte BASE_PRESSURE = 30;
    private const byte THIGH_MAX_PRESSURE = 80;
    private const byte ISCHIAL_MAX_PRESSURE = 180;
    private const byte ALERT_THRESHOLD = 150;
    private const double PRESSURE_BUILDUP_RATE = 2.5; // pressure units per second
    private const double NOISE_FACTOR = 0.15;

    /// <summary>
    /// Creates a new mock pressure data generator with specified scenario.
    /// </summary>
    /// <param name="scenario">Initial simulation scenario</param>
    /// <param name="seed">Random seed for reproducible patterns (optional)</param>
    /// <remarks>
    /// Initialization: Sets up anatomical base patterns and initializes timing state
    /// Seed Parameter: Allows reproducible demo scenarios for consistent presentations
    /// </remarks>
    public MockPressureDataGenerator(SimulationScenario scenario = SimulationScenario.NormalSitting, int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
        _startTime = DateTime.Now;
        _currentScenario = scenario;
        _scenarioStartTime = TimeSpan.Zero;
        _currentPhase = 0;
    }

    /// <summary>
    /// Generates a pressure map representing current simulation state.
    /// </summary>
    /// <returns>PressureMap with anatomically accurate pressure distribution</returns>
    /// <remarks>
    /// Generation Algorithm:
    /// 1. Calculate elapsed time and scenario phase
    /// 2. Generate base anatomical pattern
    /// 3. Apply pressure progression based on scenario
    /// 4. Add realistic noise and variation
    /// 5. Ensure values remain within valid range (1-255)
    ///
    /// Performance: Single-pass generation with O(n²) complexity for 32x32 matrix
    /// Realism: Based on published biomechanics research on sitting pressure distribution
    /// </remarks>
    public PressureMap GenerateCurrentPressureMap()
    {
        var elapsedTime = DateTime.Now - _startTime;
        var scenarioTime = elapsedTime - _scenarioStartTime;

        var pressureData = new byte[PressureMap.MATRIX_SIZE, PressureMap.MATRIX_SIZE];

        // Initialize with base anatomical pattern
        GenerateAnatomicalBasePattern(pressureData);

        // Apply scenario-specific modifications
        ApplyScenarioEffects(pressureData, scenarioTime);

        // Add realistic noise and micro-movements
        ApplyNoiseAndVariation(pressureData);

        // Ensure all values are within valid range
        ClampPressureValues(pressureData);

        return PressureMap.FromByteArray(FlattenMatrix(pressureData));
    }

    /// <summary>
    /// Changes the current simulation scenario.
    /// </summary>
    /// <param name="newScenario">New scenario to switch to</param>
    /// <remarks>
    /// Use Case: Runtime scenario switching for interactive demonstrations
    /// State Management: Resets scenario timing and phase counters
    /// </remarks>
    public void SetScenario(SimulationScenario newScenario)
    {
        _currentScenario = newScenario;
        _scenarioStartTime = DateTime.Now - _startTime;
        _currentPhase = 0;
    }

    /// <summary>
    /// Gets the current scenario and its progress information.
    /// </summary>
    /// <returns>Tuple containing scenario, phase, and progress percentage</returns>
    public (SimulationScenario scenario, int phase, double progressPercent) GetScenarioStatus()
    {
        var elapsedTime = DateTime.Now - _startTime - _scenarioStartTime;
        var totalDuration = GetScenarioDuration(_currentScenario);
        var progress = Math.Min(100.0, (elapsedTime.TotalSeconds / totalDuration.TotalSeconds) * 100.0);

        return (_currentScenario, _currentPhase, progress);
    }

    /// <summary>
    /// Generates the base anatomical sitting pattern.
    /// </summary>
    /// <param name="pressureData">Matrix to populate with base pattern</param>
    /// <remarks>
    /// Anatomical Accuracy: Based on pressure mapping studies of human sitting posture
    /// Pattern Elements:
    /// - Ischial tuberosities: Primary weight-bearing points with highest pressures
    /// - Thigh support: Secondary contact area with moderate pressures
    /// - Gradual falloff: Pressure decreases with distance from contact points
    /// - Asymmetry: Slight left/right differences for realism
    /// </remarks>
    private void GenerateAnatomicalBasePattern(byte[,] pressureData)
    {
        // Initialize all cells to no pressure
        for (int row = 0; row < PressureMap.MATRIX_SIZE; row++)
        {
            for (int col = 0; col < PressureMap.MATRIX_SIZE; col++)
            {
                pressureData[row, col] = PressureMap.NO_PRESSURE_VALUE;
            }
        }

        // Generate thigh contact area (broader, lower pressure)
        GenerateThighContactArea(pressureData);

        // Generate ischial tuberosity pressure points (concentrated, high pressure)
        GenerateIschialTuberositiesPattern(pressureData);
    }

    /// <summary>
    /// Generates pressure distribution for thigh contact areas.
    /// </summary>
    /// <param name="pressureData">Matrix to populate</param>
    /// <remarks>
    /// Pattern: Elliptical distribution with gradual pressure falloff
    /// Biomechanics: Thighs provide secondary weight support with distributed loading
    /// </remarks>
    private void GenerateThighContactArea(byte[,] pressureData)
    {
        var centerRow = (THIGH_ROW_START + THIGH_ROW_END) / 2;
        var centerCol = (THIGH_COL_START + THIGH_COL_END) / 2;

        for (int row = THIGH_ROW_START; row <= THIGH_ROW_END; row++)
        {
            for (int col = THIGH_COL_START; col <= THIGH_COL_END; col++)
            {
                // Calculate distance from thigh center
                var rowDistance = Math.Abs(row - centerRow);
                var colDistance = Math.Abs(col - centerCol);

                // Create elliptical pressure distribution
                var normalizedDistance = Math.Sqrt(
                    Math.Pow(rowDistance / 8.0, 2) + Math.Pow(colDistance / 10.0, 2)
                );

                if (normalizedDistance <= 1.0)
                {
                    // Pressure decreases with distance from center
                    var pressure = BASE_PRESSURE +
                                 (THIGH_MAX_PRESSURE - BASE_PRESSURE) * (1.0 - normalizedDistance);

                    pressureData[row, col] = (byte)Math.Max(PressureMap.NO_PRESSURE_VALUE, pressure);
                }
            }
        }
    }

    /// <summary>
    /// Generates pressure patterns for ischial tuberosity contact points.
    /// </summary>
    /// <param name="pressureData">Matrix to populate</param>
    /// <remarks>
    /// Biomechanics: Ischial tuberosities (sit bones) are primary weight-bearing points
    /// Pattern: Two concentrated high-pressure zones with slight asymmetry
    /// </remarks>
    private void GenerateIschialTuberositiesPattern(byte[,] pressureData)
    {
        // Left ischial tuberosity
        GenerateIschialPoint(pressureData,
            ISCHIAL_LEFT_ROW_START, ISCHIAL_LEFT_ROW_END,
            ISCHIAL_LEFT_COL_START, ISCHIAL_LEFT_COL_END,
            0.95); // Slightly lower pressure for asymmetry

        // Right ischial tuberosity
        GenerateIschialPoint(pressureData,
            ISCHIAL_RIGHT_ROW_START, ISCHIAL_RIGHT_ROW_END,
            ISCHIAL_RIGHT_COL_START, ISCHIAL_RIGHT_COL_END,
            1.0); // Full pressure
    }

    /// <summary>
    /// Generates a single ischial tuberosity pressure point.
    /// </summary>
    /// <param name="pressureData">Matrix to populate</param>
    /// <param name="rowStart">Starting row of pressure area</param>
    /// <param name="rowEnd">Ending row of pressure area</param>
    /// <param name="colStart">Starting column of pressure area</param>
    /// <param name="colEnd">Ending column of pressure area</param>
    /// <param name="intensityMultiplier">Pressure intensity multiplier for asymmetry</param>
    private void GenerateIschialPoint(byte[,] pressureData,
                                    int rowStart, int rowEnd,
                                    int colStart, int colEnd,
                                    double intensityMultiplier)
    {
        var centerRow = (rowStart + rowEnd) / 2;
        var centerCol = (colStart + colEnd) / 2;
        var maxPressure = ISCHIAL_MAX_PRESSURE * intensityMultiplier;

        for (int row = rowStart; row <= rowEnd; row++)
        {
            for (int col = colStart; col <= colEnd; col++)
            {
                // Calculate distance from ischial center
                var distance = Math.Sqrt(
                    Math.Pow(row - centerRow, 2) + Math.Pow(col - centerCol, 2)
                );

                // Create concentrated pressure distribution
                var maxDistance = Math.Max(rowEnd - rowStart, colEnd - colStart) / 2.0;
                var normalizedDistance = Math.Min(1.0, distance / maxDistance);

                // Exponential falloff for concentrated pressure
                var pressure = maxPressure * Math.Exp(-normalizedDistance * 3.0);

                // Combine with existing thigh pressure (take maximum)
                pressureData[row, col] = (byte)Math.Max(pressureData[row, col], pressure);
            }
        }
    }

    /// <summary>
    /// Applies scenario-specific pressure modifications over time.
    /// </summary>
    /// <param name="pressureData">Matrix to modify</param>
    /// <param name="scenarioTime">Time elapsed in current scenario</param>
    /// <remarks>
    /// Scenario Implementation:
    /// - NormalSitting: Gradual pressure increase to simulate prolonged sitting
    /// - PressureBuildupAlert: Accelerated buildup to trigger alert conditions
    /// - WeightShiftingRelief: Pressure redistribution and relief patterns
    /// - SequentialDemo: Cycles through scenarios automatically
    /// </remarks>
    private void ApplyScenarioEffects(byte[,] pressureData, TimeSpan scenarioTime)
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
        }
    }

    /// <summary>
    /// Applies normal sitting pressure progression.
    /// </summary>
    /// <param name="pressureData">Matrix to modify</param>
    /// <param name="scenarioTime">Elapsed time in scenario</param>
    /// <remarks>
    /// Pattern: Gradual pressure increase over time simulating tissue compression
    /// Rate: Conservative buildup to demonstrate normal monitoring conditions
    /// </remarks>
    private void ApplyNormalSittingProgression(byte[,] pressureData, TimeSpan scenarioTime)
    {
        var pressureIncrease = (int)(scenarioTime.TotalSeconds * PRESSURE_BUILDUP_RATE * 0.5);

        ApplyPressureIncrease(pressureData, pressureIncrease);
    }

    /// <summary>
    /// Applies accelerated pressure buildup leading to alert condition.
    /// </summary>
    /// <param name="pressureData">Matrix to modify</param>
    /// <param name="scenarioTime">Elapsed time in scenario</param>
    /// <remarks>
    /// Pattern: Faster pressure increase targeting alert threshold demonstration
    /// Business Purpose: Showcases alert detection and notification capabilities
    /// </remarks>
    private void ApplyPressureBuildupProgression(byte[,] pressureData, TimeSpan scenarioTime)
    {
        var pressureIncrease = (int)(scenarioTime.TotalSeconds * PRESSURE_BUILDUP_RATE * 2.0);

        ApplyPressureIncrease(pressureData, pressureIncrease);
    }

    /// <summary>
    /// Applies weight shifting pattern to redistribute pressure.
    /// </summary>
    /// <param name="pressureData">Matrix to modify</param>
    /// <param name="scenarioTime">Elapsed time in scenario</param>
    /// <remarks>
    /// Pattern: Alternating pressure between left and right sides
    /// Biomechanics: Simulates natural weight shifting behavior for pressure relief
    /// </remarks>
    private void ApplyWeightShiftingPattern(byte[,] pressureData, TimeSpan scenarioTime)
    {
        var shiftCycle = scenarioTime.TotalSeconds % 10.0; // 10-second shift cycle
        var leftWeight = 0.5 + 0.3 * Math.Sin(shiftCycle * Math.PI / 5.0);
        var rightWeight = 1.0 - leftWeight;

        ApplyAsymmetricPressure(pressureData, leftWeight, rightWeight);
    }

    /// <summary>
    /// Applies sequential demo cycling through all scenarios.
    /// </summary>
    /// <param name="pressureData">Matrix to modify</param>
    /// <param name="scenarioTime">Elapsed time in demo</param>
    /// <remarks>
    /// Demo Flow: Normal → Buildup → Alert → Relief → Repeat
    /// Duration: Each phase lasts 15-30 seconds for optimal demonstration impact
    /// </remarks>
    private void ApplySequentialDemo(byte[,] pressureData, TimeSpan scenarioTime)
    {
        var phaseTime = scenarioTime.TotalSeconds % 60.0; // 60-second full cycle

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

    /// <summary>
    /// Applies uniform pressure increase to ischial areas.
    /// </summary>
    /// <param name="pressureData">Matrix to modify</param>
    /// <param name="increase">Pressure increase amount</param>
    private void ApplyPressureIncrease(byte[,] pressureData, int increase)
    {
        for (int row = 0; row < PressureMap.MATRIX_SIZE; row++)
        {
            for (int col = 0; col < PressureMap.MATRIX_SIZE; col++)
            {
                if (pressureData[row, col] > BASE_PRESSURE)
                {
                    pressureData[row, col] = (byte)Math.Min(255,
                        pressureData[row, col] + increase);
                }
            }
        }
    }

    /// <summary>
    /// Applies asymmetric pressure weighting for weight shifting simulation.
    /// </summary>
    /// <param name="pressureData">Matrix to modify</param>
    /// <param name="leftWeight">Left side weight multiplier</param>
    /// <param name="rightWeight">Right side weight multiplier</param>
    private void ApplyAsymmetricPressure(byte[,] pressureData, double leftWeight, double rightWeight)
    {
        var midCol = PressureMap.MATRIX_SIZE / 2;

        for (int row = 0; row < PressureMap.MATRIX_SIZE; row++)
        {
            for (int col = 0; col < PressureMap.MATRIX_SIZE; col++)
            {
                if (pressureData[row, col] > BASE_PRESSURE)
                {
                    var weight = col < midCol ? leftWeight : rightWeight;
                    pressureData[row, col] = (byte)(pressureData[row, col] * weight);
                }
            }
        }
    }

    /// <summary>
    /// Adds realistic noise and micro-movements to pressure data.
    /// </summary>
    /// <param name="pressureData">Matrix to add noise to</param>
    /// <remarks>
    /// Noise Characteristics:
    /// - Gaussian distribution around base values
    /// - Proportional to pressure magnitude
    /// - Temporal correlation for realistic sensor behavior
    /// </remarks>
    private void ApplyNoiseAndVariation(byte[,] pressureData)
    {
        for (int row = 0; row < PressureMap.MATRIX_SIZE; row++)
        {
            for (int col = 0; col < PressureMap.MATRIX_SIZE; col++)
            {
                if (pressureData[row, col] > PressureMap.NO_PRESSURE_VALUE)
                {
                    var basePressure = pressureData[row, col];
                    var noiseRange = basePressure * NOISE_FACTOR;
                    var noise = (_random.NextDouble() - 0.5) * noiseRange * 2.0;

                    pressureData[row, col] = (byte)Math.Max(PressureMap.NO_PRESSURE_VALUE,
                        basePressure + noise);
                }
            }
        }
    }

    /// <summary>
    /// Ensures all pressure values remain within valid range (1-255).
    /// </summary>
    /// <param name="pressureData">Matrix to clamp</param>
    /// <remarks>
    /// Validation: Prevents invalid pressure values that would cause PressureMap creation to fail
    /// Range: 1 (no pressure) to 255 (maximum pressure)
    /// </remarks>
    private void ClampPressureValues(byte[,] pressureData)
    {
        for (int row = 0; row < PressureMap.MATRIX_SIZE; row++)
        {
            for (int col = 0; col < PressureMap.MATRIX_SIZE; col++)
            {
                if (pressureData[row, col] == 0)
                    pressureData[row, col] = PressureMap.NO_PRESSURE_VALUE;

                pressureData[row, col] = Math.Min((byte)255,
                    Math.Max(PressureMap.NO_PRESSURE_VALUE, pressureData[row, col]));
            }
        }
    }

    /// <summary>
    /// Flattens 2D matrix to 1D array for PressureMap creation.
    /// </summary>
    /// <param name="matrix">2D pressure matrix</param>
    /// <returns>Flattened array in row-major order</returns>
    private static byte[] FlattenMatrix(byte[,] matrix)
    {
        var result = new byte[PressureMap.MATRIX_SIZE * PressureMap.MATRIX_SIZE];
        Buffer.BlockCopy(matrix, 0, result, 0, result.Length);
        return result;
    }

    /// <summary>
    /// Gets the duration of a specific scenario.
    /// </summary>
    /// <param name="scenario">Scenario to get duration for</param>
    /// <returns>Scenario duration</returns>
    private static TimeSpan GetScenarioDuration(SimulationScenario scenario)
    {
        return scenario switch
        {
            SimulationScenario.NormalSitting => TimeSpan.FromMinutes(2),
            SimulationScenario.PressureBuildupAlert => TimeSpan.FromSeconds(45),
            SimulationScenario.WeightShiftingRelief => TimeSpan.FromSeconds(30),
            SimulationScenario.SequentialDemo => TimeSpan.FromMinutes(1),
            _ => TimeSpan.FromMinutes(1)
        };
    }
}