using System;
using System.Drawing;
using GrapheneTrace.Core.Models;

namespace GrapheneTrace.Terminal.Display;

/// <summary>
/// Maps pressure values to colors for both gradient and discrete range visualization modes.
/// </summary>
/// <remarks>
/// Design Pattern: Strategy Pattern with factory methods for different color modes
/// Why: Supports multiple visualization strategies (gradient vs. discrete) with optimal
/// performance characteristics for each mode.
///
/// Business Purpose: Converts raw pressure data into meaningful color representations,
/// enabling intuitive pressure map visualization where color intensity corresponds
/// to pressure magnitude.
///
/// Technical Details: Pre-calculates color mappings for performance, supports both
/// smooth gradient transitions and discrete range-based coloring aligned with
/// PressureMap's configurable range system.
/// </remarks>
public class ColorMapper
{
    /// <summary>
    /// Defines available color mapping modes for pressure visualization.
    /// </summary>
    public enum ColorMode
    {
        /// <summary>Smooth gradient from blue (low) to red (high) pressure</summary>
        Gradient,
        /// <summary>Discrete colors for each pressure range</summary>
        DiscreteRanges
    }

    private readonly ColorMode _mode;
    private readonly Color[] _gradientCache;
    private readonly Color[] _rangeColorPalette;
    private readonly PressureMap? _referencePressureMap;

    // Gradient color stops for smooth pressure visualization
    private static readonly (byte pressure, Color color)[] GradientStops =
    {
        (1, Color.FromArgb(0, 0, 139)),      // Dark Blue (no pressure)
        (51, Color.FromArgb(0, 139, 139)),   // Dark Cyan
        (102, Color.FromArgb(0, 255, 0)),    // Green
        (153, Color.FromArgb(255, 255, 0)),  // Yellow
        (204, Color.FromArgb(255, 140, 0)),  // Orange
        (255, Color.FromArgb(255, 0, 0))     // Red (maximum pressure)
    };

    // Discrete color palette for range-based visualization (colorblind-friendly)
    private static readonly Color[] DiscreteColorPalette =
    {
        Color.FromArgb(0, 0, 139),      // Dark Blue (no pressure)
        Color.FromArgb(30, 144, 255),   // Dodger Blue
        Color.FromArgb(0, 191, 255),    // Deep Sky Blue
        Color.FromArgb(0, 255, 255),    // Cyan
        Color.FromArgb(0, 255, 127),    // Spring Green
        Color.FromArgb(0, 255, 0),      // Lime
        Color.FromArgb(127, 255, 0),    // Chart Reuse
        Color.FromArgb(255, 255, 0),    // Yellow
        Color.FromArgb(255, 215, 0),    // Gold
        Color.FromArgb(255, 165, 0),    // Orange
        Color.FromArgb(255, 140, 0),    // Dark Orange
        Color.FromArgb(255, 69, 0),     // Orange Red
        Color.FromArgb(255, 0, 0),      // Red
        Color.FromArgb(220, 20, 60),    // Crimson
        Color.FromArgb(139, 0, 0),      // Dark Red
        Color.FromArgb(128, 0, 128),    // Purple (overflow/alert)
        Color.FromArgb(75, 0, 130),     // Indigo
        Color.FromArgb(138, 43, 226),   // Blue Violet
        Color.FromArgb(148, 0, 211),    // Dark Violet
        Color.FromArgb(199, 21, 133),   // Medium Violet Red
        Color.FromArgb(255, 20, 147),   // Deep Pink
        Color.FromArgb(255, 105, 180),  // Hot Pink
        Color.FromArgb(255, 192, 203),  // Pink
        Color.FromArgb(255, 255, 255)   // White (extreme overflow)
    };

    /// <summary>
    /// Creates a gradient-mode color mapper with pre-calculated color interpolations.
    /// </summary>
    /// <returns>ColorMapper instance configured for gradient visualization</returns>
    /// <remarks>
    /// Design Decision: Factory method for gradient mode
    /// Performance: Pre-calculates all 255 color values for O(1) lookup
    /// Memory Trade-off: ~3KB cache for significantly improved rendering performance
    /// </remarks>
    public static ColorMapper CreateGradient()
    {
        return new ColorMapper(ColorMode.Gradient);
    }

    /// <summary>
    /// Creates a discrete range-mode color mapper using PressureMap's range configuration.
    /// </summary>
    /// <returns>ColorMapper instance configured for discrete range visualization</returns>
    /// <remarks>
    /// Design Decision: Uses PressureMap.RangeConfiguration for consistent range mapping
    /// Performance: Direct array lookup based on pressure range index
    /// </remarks>
    public static ColorMapper CreateDiscreteRanges()
    {
        return new ColorMapper(ColorMode.DiscreteRanges);
    }

    /// <summary>
    /// Private constructor enforces use of factory methods.
    /// </summary>
    /// <param name="mode">Color mapping mode to initialize</param>
    /// <remarks>
    /// Initialization Strategy: Pre-calculates color mappings based on mode
    /// - Gradient: Interpolates 255 colors using piecewise linear interpolation
    /// - Discrete: Maps PressureMap ranges to distinct colors
    /// </remarks>
    private ColorMapper(ColorMode mode)
    {
        _mode = mode;

        if (mode == ColorMode.Gradient)
        {
            _gradientCache = new Color[256]; // Index 0 unused, 1-255 mapped
            BuildGradientCache();
        }
        else
        {
            var rangeCount = PressureMap.RangeConfiguration.GetRangeCount();
            _rangeColorPalette = new Color[rangeCount];
            BuildRangeColorPalette(rangeCount);
        }
    }

    /// <summary>
    /// Maps a pressure value to its corresponding color.
    /// </summary>
    /// <param name="pressure">Pressure value (1-255)</param>
    /// <returns>Color representing the pressure intensity</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when pressure is outside valid range</exception>
    /// <remarks>
    /// Performance: O(1) lookup using pre-calculated color arrays
    /// Algorithm:
    /// - Gradient mode: Direct array lookup
    /// - Range mode: Converts pressure to range index, then color lookup
    /// </remarks>
    public Color GetColor(byte pressure)
    {
        if (pressure == 0)
            throw new ArgumentOutOfRangeException(nameof(pressure), "Pressure value 0 is invalid (use 1 for no pressure)");

        if (_mode == ColorMode.Gradient)
        {
            return _gradientCache[pressure];
        }
        else
        {
            var rangeIndex = GetPressureRangeIndex(pressure);
            return _rangeColorPalette[Math.Min(rangeIndex, _rangeColorPalette.Length - 1)];
        }
    }

    /// <summary>
    /// Maps pressure value to range index using same algorithm as PressureMap.
    /// </summary>
    /// <param name="pressure">Pressure value to map</param>
    /// <returns>Range index for color lookup</returns>
    /// <remarks>
    /// Design Note: Duplicates PressureMap logic to avoid dependency
    /// TODO: Extract to shared utility class to eliminate duplication
    /// </remarks>
    private static int GetPressureRangeIndex(byte pressure)
    {
        return pressure switch
        {
            PressureMap.NO_PRESSURE_VALUE => 0,
            PressureMap.MAX_PRESSURE_VALUE => PressureMap.RangeConfiguration.GetRangeCount() - 1,
            _ => 1 + ((pressure - 2) / PressureMap.RangeConfiguration.SliceSize)
        };
    }

    /// <summary>
    /// Gets a character representation for the pressure value with appropriate intensity.
    /// </summary>
    /// <param name="pressure">Pressure value (1-255)</param>
    /// <returns>Unicode character representing pressure intensity</returns>
    /// <remarks>
    /// Character Selection: Uses Unicode block elements for visual intensity
    /// - Low pressure: Light shading (░)
    /// - Medium pressure: Medium shading (▒)
    /// - High pressure: Dark shading (▓)
    /// - Maximum pressure: Full block (█)
    ///
    /// Fallback Strategy: ASCII characters for terminals without Unicode support
    /// </remarks>
    public char GetPressureChar(byte pressure)
    {
        if (pressure == PressureMap.NO_PRESSURE_VALUE)
            return ' '; // No pressure = empty space

        // Unicode block elements (preferred)
        return pressure switch
        {
            <= 64 => '░',   // Light shade (░)
            <= 128 => '▒',  // Medium shade (▒)
            <= 192 => '▓',  // Dark shade (▓)
            _ => '█'        // Full block (█)
        };
    }

    /// <summary>
    /// Gets an ASCII fallback character for terminals without Unicode support.
    /// </summary>
    /// <param name="pressure">Pressure value (1-255)</param>
    /// <returns>ASCII character representing pressure intensity</returns>
    public char GetPressureCharAscii(byte pressure)
    {
        if (pressure == PressureMap.NO_PRESSURE_VALUE)
            return ' ';

        return pressure switch
        {
            <= 64 => '.',   // Minimal pressure
            <= 128 => '+',  // Low pressure
            <= 192 => '*',  // Medium pressure
            _ => '#'        // High pressure
        };
    }

    /// <summary>
    /// Pre-calculates gradient colors using piecewise linear interpolation.
    /// </summary>
    /// <remarks>
    /// Algorithm: Piecewise linear interpolation between defined color stops
    /// Performance: One-time calculation during initialization
    /// Color Theory: Blue-to-red spectrum provides intuitive pressure visualization
    /// </remarks>
    private void BuildGradientCache()
    {
        _gradientCache[0] = Color.Black; // Unused index

        for (int pressure = 1; pressure <= 255; pressure++)
        {
            _gradientCache[pressure] = InterpolateGradientColor((byte)pressure);
        }
    }

    /// <summary>
    /// Interpolates color for given pressure using gradient stops.
    /// </summary>
    /// <param name="pressure">Pressure value to interpolate</param>
    /// <returns>Interpolated color</returns>
    /// <remarks>
    /// Algorithm: Finds adjacent color stops and performs linear interpolation
    /// Edge Cases: Values outside gradient range clamp to nearest stop
    /// </remarks>
    private static Color InterpolateGradientColor(byte pressure)
    {
        // Find the two color stops that bracket this pressure value
        for (int i = 0; i < GradientStops.Length - 1; i++)
        {
            var (lowerPressure, lowerColor) = GradientStops[i];
            var (upperPressure, upperColor) = GradientStops[i + 1];

            if (pressure <= upperPressure)
            {
                if (pressure <= lowerPressure)
                    return lowerColor;

                // Linear interpolation between color stops
                var ratio = (double)(pressure - lowerPressure) / (upperPressure - lowerPressure);
                return Color.FromArgb(
                    (int)(lowerColor.R + ratio * (upperColor.R - lowerColor.R)),
                    (int)(lowerColor.G + ratio * (upperColor.G - lowerColor.G)),
                    (int)(lowerColor.B + ratio * (upperColor.B - lowerColor.B))
                );
            }
        }

        // If pressure exceeds maximum gradient stop, use the highest color
        return GradientStops[^1].color;
    }

    /// <summary>
    /// Builds discrete color palette for range-based visualization.
    /// </summary>
    /// <param name="rangeCount">Number of pressure ranges to map</param>
    /// <remarks>
    /// Palette Selection: Ensures sufficient color variation for visual distinction
    /// Accessibility: Uses colorblind-friendly color progression
    /// Overflow Handling: Cycles through palette if ranges exceed available colors
    /// </remarks>
    private void BuildRangeColorPalette(int rangeCount)
    {
        for (int i = 0; i < rangeCount; i++)
        {
            // Cycle through discrete palette if ranges exceed available colors
            var paletteIndex = i % DiscreteColorPalette.Length;
            _rangeColorPalette[i] = DiscreteColorPalette[paletteIndex];
        }
    }

    /// <summary>
    /// Gets the current color mapping mode.
    /// </summary>
    public ColorMode Mode => _mode;

    /// <summary>
    /// Gets the number of distinct colors available in current mode.
    /// </summary>
    /// <returns>Number of unique colors in the current mapping</returns>
    public int GetColorCount()
    {
        return _mode switch
        {
            ColorMode.Gradient => 255,
            ColorMode.DiscreteRanges => _rangeColorPalette.Length,
            _ => throw new InvalidOperationException($"Unknown color mode: {_mode}")
        };
    }
}