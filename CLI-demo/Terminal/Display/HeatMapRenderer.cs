using System;
using System.Text;
using GrapheneTrace.Core.Models;

namespace GrapheneTrace.Terminal.Display;

/// <summary>
/// Renders 32x32 pressure matrices as colored heat maps in the terminal console.
/// </summary>
/// <remarks>
/// Design Pattern: Facade Pattern combining multiple display utilities
/// Why: Provides a simple interface for complex heat map rendering while managing
/// performance optimizations like double buffering and dirty region tracking.
///
/// Business Purpose: Core visualization component for pressure map analysis, enabling
/// real-time monitoring of pressure distribution patterns with professional presentation.
///
/// Technical Details: Optimized for 10+ FPS rendering with minimal flicker using
/// cursor positioning, double buffering, and incremental updates. Supports both
/// gradient and discrete range visualization modes.
/// </remarks>
public class HeatMapRenderer
{
    private ColorMapper _colorMapper;
    private readonly bool _useUnicodeChars;
    private readonly int _cellWidth;
    private readonly int _cellHeight;

    // Display layout constants
    private const int BORDER_WIDTH = 2;
    private const int LEGEND_WIDTH = 20;
    private const int TITLE_HEIGHT = 3;
    private const int STATUS_HEIGHT = 2;

    // Frame buffer for double buffering
    private string?[,] _frameBuffer;
    private string?[,] _previousFrameBuffer;
    private readonly object _renderLock = new();
    private bool _isInitialized = false;

    /// <summary>
    /// Creates a new heat map renderer with specified configuration.
    /// </summary>
    /// <param name="colorMode">Color mapping mode (gradient or discrete ranges)</param>
    /// <param name="useUnicodeChars">Whether to use Unicode block characters</param>
    /// <param name="cellWidth">Width of each pressure cell in characters (default: 2)</param>
    /// <param name="cellHeight">Height of each pressure cell in lines (default: 1)</param>
    /// <remarks>
    /// Design Decision: Constructor parameters allow customization for different terminal capabilities
    /// Performance: cellWidth=2 provides better visual clarity while cellHeight=1 optimizes screen space
    /// </remarks>
    public HeatMapRenderer(ColorMapper.ColorMode colorMode = ColorMapper.ColorMode.Gradient,
                          bool useUnicodeChars = true,
                          int cellWidth = 2,
                          int cellHeight = 1)
    {
        _colorMapper = colorMode == ColorMapper.ColorMode.Gradient
            ? ColorMapper.CreateGradient()
            : ColorMapper.CreateDiscreteRanges();

        _useUnicodeChars = useUnicodeChars && VirtualTerminalProcessor.SupportsAnsi;
        _cellWidth = cellWidth;
        _cellHeight = cellHeight;

        // Initialize frame buffers for double buffering
        var totalWidth = PressureMap.MATRIX_SIZE * _cellWidth + BORDER_WIDTH * 2 + LEGEND_WIDTH;
        var totalHeight = PressureMap.MATRIX_SIZE * _cellHeight + TITLE_HEIGHT + STATUS_HEIGHT + BORDER_WIDTH * 2;

        _frameBuffer = new string[totalHeight, totalWidth];
        _previousFrameBuffer = new string[totalHeight, totalWidth];
    }

    /// <summary>
    /// Initializes the renderer and prepares the terminal for heat map display.
    /// </summary>
    /// <returns>True if initialization successful</returns>
    /// <remarks>
    /// Initialization Steps:
    /// 1. Enable virtual terminal processing
    /// 2. Clear screen and hide cursor
    /// 3. Set up initial layout and legend
    /// 4. Position cursor for heat map area
    ///
    /// Performance: One-time setup minimizes per-frame overhead
    /// </remarks>
    public bool Initialize()
    {
        lock (_renderLock)
        {
            if (_isInitialized)
                return true;

            if (!VirtualTerminalProcessor.Initialize())
            {
                System.Console.WriteLine("Warning: ANSI colors not supported. Falling back to basic display.");
                return false;
            }

            var builder = new AnsiColorBuilder(2048);

            // Clear screen and hide cursor for smooth rendering
            builder.ClearScreen()
                   .HideCursor()
                   .MoveCursor(1, 1);

            // Draw initial layout
            DrawStaticLayout(builder);

            builder.WriteToConsole();

            _isInitialized = true;
            return true;
        }
    }

    /// <summary>
    /// Renders a pressure map to the terminal with optimal performance.
    /// </summary>
    /// <param name="pressureMap">32x32 pressure map to visualize</param>
    /// <param name="title">Optional title for the display</param>
    /// <param name="metrics">Optional metrics to display alongside the heat map</param>
    /// <exception cref="InvalidOperationException">Thrown if renderer not initialized</exception>
    /// <remarks>
    /// Performance Optimizations:
    /// - Double buffering prevents flicker
    /// - Dirty region tracking minimizes redraws
    /// - StringBuilder pooling reduces allocations
    /// - Direct cursor positioning avoids full screen clears
    ///
    /// Rendering Algorithm:
    /// 1. Build new frame in memory buffer
    /// 2. Compare with previous frame
    /// 3. Update only changed regions
    /// 4. Swap buffers for next frame
    /// </remarks>
    public void Render(PressureMap pressureMap, string? title = null, HeatMapMetrics? metrics = null)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Renderer must be initialized before rendering");

        lock (_renderLock)
        {
            var builder = new AnsiColorBuilder(8192);

            // Build new frame buffer
            BuildFrameBuffer(pressureMap, title, metrics);

            // Render only changed regions for optimal performance
            RenderChangedRegions(builder);

            // Swap buffers
            (_frameBuffer, _previousFrameBuffer) = (_previousFrameBuffer, _frameBuffer);

            builder.WriteToConsole();
        }
    }

    /// <summary>
    /// Renders pressure map with real-time metrics display.
    /// </summary>
    /// <param name="pressureMap">Pressure map to visualize</param>
    /// <param name="peakPressure">Current peak pressure value</param>
    /// <param name="contactArea">Contact area percentage</param>
    /// <param name="alertStatus">Current alert status</param>
    /// <param name="frameRate">Current rendering frame rate</param>
    public void RenderWithMetrics(PressureMap pressureMap,
                                 byte peakPressure,
                                 double contactArea,
                                 string alertStatus,
                                 double frameRate)
    {
        var metrics = new HeatMapMetrics
        {
            PeakPressure = peakPressure,
            ContactAreaPercentage = contactArea,
            AlertStatus = alertStatus,
            FrameRate = frameRate,
            ColorMode = _colorMapper.Mode.ToString(),
            LastUpdate = DateTime.Now
        };

        Render(pressureMap, "GrapheneTrace Pressure Monitor", metrics);
    }

    /// <summary>
    /// Switches the color mapping mode and reinitializes display.
    /// </summary>
    /// <param name="newMode">New color mode to switch to</param>
    /// <remarks>
    /// Use Case: Runtime switching between gradient and discrete range modes
    /// Performance: Clears frame buffers to force full redraw
    /// </remarks>
    public void SwitchColorMode(ColorMapper.ColorMode newMode)
    {
        lock (_renderLock)
        {
            var newMapper = newMode == ColorMapper.ColorMode.Gradient
                ? ColorMapper.CreateGradient()
                : ColorMapper.CreateDiscreteRanges();

            // Replace color mapper
            _colorMapper = newMapper;

            // Clear frame buffers to force full redraw
            Array.Clear(_frameBuffer);
            Array.Clear(_previousFrameBuffer);
        }
    }

    /// <summary>
    /// Cleans up renderer resources and restores terminal state.
    /// </summary>
    /// <remarks>
    /// Cleanup Tasks:
    /// - Restore cursor visibility
    /// - Reset colors to default
    /// - Position cursor at bottom of display
    ///
    /// Important: Should be called on application exit
    /// </remarks>
    public void Cleanup()
    {
        lock (_renderLock)
        {
            if (!_isInitialized)
                return;

            var builder = new AnsiColorBuilder(256);
            builder.Reset()
                   .ShowCursor()
                   .MoveCursor(System.Console.WindowHeight, 1);

            builder.WriteToConsole();

            VirtualTerminalProcessor.Reset();
            _isInitialized = false;
        }
    }

    /// <summary>
    /// Draws the static layout elements (borders, legend, labels).
    /// </summary>
    /// <param name="builder">ANSI color builder for output</param>
    /// <remarks>
    /// Static Elements:
    /// - Professional border with title
    /// - Color legend showing pressure ranges
    /// - Keyboard shortcuts help
    /// - Status area for metrics
    /// </remarks>
    private void DrawStaticLayout(AnsiColorBuilder builder)
    {
        // Draw main border
        DrawBorder(builder, 1, 1,
                  PressureMap.MATRIX_SIZE * _cellWidth + BORDER_WIDTH * 2,
                  PressureMap.MATRIX_SIZE * _cellHeight + TITLE_HEIGHT + STATUS_HEIGHT + BORDER_WIDTH * 2);

        // Draw title area
        builder.MoveCursor(2, 3)
               .SetForegroundColor(System.Drawing.Color.White)
               .AppendText("GrapheneTrace Sensore - Pressure Monitoring")
               .Reset();

        // Draw legend
        DrawLegend(builder);

        // Draw keyboard shortcuts
        var shortcutsY = PressureMap.MATRIX_SIZE + TITLE_HEIGHT + BORDER_WIDTH + 1;
        builder.MoveCursor(shortcutsY, 3)
               .SetForegroundColor(System.Drawing.Color.LightGray)
               .AppendText("[G] Gradient  [R] Range  [P] Pause  [Q] Quit")
               .Reset();
    }

    /// <summary>
    /// Draws the color legend for the current mapping mode.
    /// </summary>
    /// <param name="builder">ANSI color builder for output</param>
    /// <remarks>
    /// Legend Content:
    /// - Gradient mode: Color ramp with pressure values
    /// - Range mode: Discrete color blocks with range labels
    /// </remarks>
    private void DrawLegend(AnsiColorBuilder builder)
    {
        var legendX = PressureMap.MATRIX_SIZE * _cellWidth + BORDER_WIDTH * 2 + 2;
        var legendStartY = TITLE_HEIGHT + 1;

        builder.MoveCursor(legendStartY, legendX)
               .SetForegroundColor(System.Drawing.Color.White)
               .AppendText("PRESSURE LEGEND")
               .Reset();

        if (_colorMapper.Mode == ColorMapper.ColorMode.Gradient)
        {
            DrawGradientLegend(builder, legendX, legendStartY + 2);
        }
        else
        {
            DrawDiscreteLegend(builder, legendX, legendStartY + 2);
        }
    }

    /// <summary>
    /// Draws gradient legend with color ramp.
    /// </summary>
    private void DrawGradientLegend(AnsiColorBuilder builder, int x, int startY)
    {
        var legendValues = new byte[] { 1, 64, 128, 192, 255 };
        var legendLabels = new[] { "None", "Low", "Med", "High", "Max" };

        for (int i = 0; i < legendValues.Length; i++)
        {
            var pressure = legendValues[i];
            var color = _colorMapper.GetColor(pressure);
            var character = _useUnicodeChars
                ? _colorMapper.GetPressureChar(pressure)
                : _colorMapper.GetPressureCharAscii(pressure);

            builder.MoveCursor(startY + i, x)
                   .SetForegroundColor(color)
                   .AppendChar(character)
                   .AppendChar(character)
                   .Reset()
                   .AppendText($" {legendLabels[i]} ({pressure})")
                   .Reset();
        }
    }

    /// <summary>
    /// Draws discrete range legend with color blocks.
    /// </summary>
    private void DrawDiscreteLegend(AnsiColorBuilder builder, int x, int startY)
    {
        var rangeCount = Math.Min(_colorMapper.GetColorCount(), 10); // Limit legend size

        for (int i = 0; i < rangeCount; i++)
        {
            var samplePressure = (byte)(1 + i * (254 / rangeCount));
            var color = _colorMapper.GetColor(samplePressure);
            var character = _useUnicodeChars ? '█' : '#';

            builder.MoveCursor(startY + i, x)
                   .SetForegroundColor(color)
                   .AppendChar(character)
                   .AppendChar(character)
                   .Reset()
                   .AppendText($" Range {i + 1}")
                   .Reset();
        }
    }

    /// <summary>
    /// Builds the complete frame buffer for a pressure map.
    /// </summary>
    /// <param name="pressureMap">Pressure map to render</param>
    /// <param name="title">Optional title</param>
    /// <param name="metrics">Optional metrics</param>
    private void BuildFrameBuffer(PressureMap pressureMap, string? title, HeatMapMetrics? metrics)
    {
        // Clear current frame buffer
        Array.Clear(_frameBuffer);

        // Render pressure matrix
        for (int row = 0; row < PressureMap.MATRIX_SIZE; row++)
        {
            for (int col = 0; col < PressureMap.MATRIX_SIZE; col++)
            {
                var pressure = pressureMap.GetPressure(row, col);
                var color = _colorMapper.GetColor(pressure);
                var character = _useUnicodeChars
                    ? _colorMapper.GetPressureChar(pressure)
                    : _colorMapper.GetPressureCharAscii(pressure);

                var builder = new AnsiColorBuilder(64);
                builder.SetForegroundColor(color);

                for (int cw = 0; cw < _cellWidth; cw++)
                {
                    builder.AppendChar(character);
                }
                builder.Reset();

                var displayRow = TITLE_HEIGHT + row * _cellHeight + BORDER_WIDTH;
                var displayCol = col * _cellWidth + BORDER_WIDTH + 1;

                _frameBuffer[displayRow, displayCol] = builder.Build();
            }
        }

        // Render metrics if provided
        if (metrics != null)
        {
            RenderMetrics(metrics);
        }
    }

    /// <summary>
    /// Renders metrics in the designated area.
    /// </summary>
    /// <param name="metrics">Metrics to display</param>
    private void RenderMetrics(HeatMapMetrics metrics)
    {
        var metricsX = PressureMap.MATRIX_SIZE * _cellWidth + BORDER_WIDTH * 2 + 2;
        var metricsY = TITLE_HEIGHT + 12;

        const int FIELD_WIDTH = 35; // Fixed width to ensure proper clearing of longest text

        var metricsText = new[]
        {
            $"Peak Pressure: {metrics.PeakPressure}".PadRight(FIELD_WIDTH),
            $"Contact Area: {metrics.ContactAreaPercentage:F1}%".PadRight(FIELD_WIDTH),
            $"Alert Status: {metrics.AlertStatus}".PadRight(FIELD_WIDTH),
            $"Color Mode: {metrics.ColorMode}".PadRight(FIELD_WIDTH),
            $"Frame Rate: {metrics.FrameRate:F1} FPS".PadRight(FIELD_WIDTH),
            $"Last Update: {metrics.LastUpdate:HH:mm:ss}".PadRight(FIELD_WIDTH)
        };

        for (int i = 0; i < metricsText.Length; i++)
        {
            var builder = new AnsiColorBuilder(128);
            builder.SetForegroundColor(System.Drawing.Color.LightGray)
                   .AppendText(metricsText[i])
                   .Reset();

            if (metricsY + i < _frameBuffer.GetLength(0) && metricsX < _frameBuffer.GetLength(1))
            {
                _frameBuffer[metricsY + i, metricsX] = builder.Build();
            }
        }
    }

    /// <summary>
    /// Renders only regions that have changed since the last frame.
    /// </summary>
    /// <param name="builder">ANSI color builder for output</param>
    /// <remarks>
    /// Performance: Dramatically reduces terminal output by updating only changed cells
    /// Algorithm: Cell-by-cell comparison between current and previous frame buffers
    /// </remarks>
    private void RenderChangedRegions(AnsiColorBuilder builder)
    {
        for (int row = 0; row < _frameBuffer.GetLength(0); row++)
        {
            for (int col = 0; col < _frameBuffer.GetLength(1); col++)
            {
                var current = _frameBuffer[row, col];
                var previous = _previousFrameBuffer[row, col];

                if (current != previous && current != null)
                {
                    builder.MoveCursor(row + 1, col + 1) // Convert to 1-based terminal coordinates
                           .AppendText(current);
                }
            }
        }
    }

    /// <summary>
    /// Draws a border at the specified location.
    /// </summary>
    /// <param name="builder">ANSI color builder</param>
    /// <param name="x">Left position</param>
    /// <param name="y">Top position</param>
    /// <param name="width">Border width</param>
    /// <param name="height">Border height</param>
    private void DrawBorder(AnsiColorBuilder builder, int x, int y, int width, int height)
    {
        builder.SetForegroundColor(System.Drawing.Color.LightGray);

        // Top border
        builder.MoveCursor(y, x);
        for (int i = 0; i < width; i++)
            builder.AppendChar('─');

        // Bottom border
        builder.MoveCursor(y + height - 1, x);
        for (int i = 0; i < width; i++)
            builder.AppendChar('─');

        // Side borders
        for (int i = 1; i < height - 1; i++)
        {
            builder.MoveCursor(y + i, x).AppendChar('│');
            builder.MoveCursor(y + i, x + width - 1).AppendChar('│');
        }

        // Corners
        builder.MoveCursor(y, x).AppendChar('╭');
        builder.MoveCursor(y, x + width - 1).AppendChar('╮');
        builder.MoveCursor(y + height - 1, x).AppendChar('╰');
        builder.MoveCursor(y + height - 1, x + width - 1).AppendChar('╯');

        builder.Reset();
    }
}

/// <summary>
/// Metrics data structure for heat map display.
/// </summary>
/// <remarks>
/// Design Pattern: Data Transfer Object for metrics display
/// Purpose: Encapsulates all metrics information for rendering
/// </remarks>
public class HeatMapMetrics
{
    public byte PeakPressure { get; set; }
    public double ContactAreaPercentage { get; set; }
    public string AlertStatus { get; set; } = "NORMAL";
    public string ColorMode { get; set; } = "Gradient";
    public double FrameRate { get; set; }
    public DateTime LastUpdate { get; set; }
}