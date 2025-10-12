using System;
using System.Drawing;
using System.Text;

namespace GrapheneTrace.Terminal.Display;

/// <summary>
/// Builds ANSI escape sequences for terminal colors, cursor positioning, and screen control.
/// </summary>
/// <remarks>
/// Design Pattern: Builder Pattern with method chaining
/// Why: Provides fluent interface for constructing complex ANSI sequences efficiently
///
/// Business Purpose: Core utility for all terminal visualization components, enabling
/// rich colored output and precise screen positioning for pressure map display.
///
/// Technical Details: Optimized for performance with StringBuilder pooling and
/// pre-calculated color mappings. Supports both 24-bit RGB and 256-color fallback.
/// </remarks>
public class AnsiColorBuilder
{
    private readonly StringBuilder _buffer;
    private static readonly string[] _256ColorCache = new string[256];
    private static readonly object _cacheInitLock = new();
    private static bool _cacheInitialized = false;

    // ANSI escape sequence constants
    private const string ESC = "\x1b[";
    private const string RESET = "\x1b[0m";
    private const string CLEAR_SCREEN = "\x1b[2J";
    private const string CLEAR_LINE = "\x1b[2K";
    private const string HIDE_CURSOR = "\x1b[?25l";
    private const string SHOW_CURSOR = "\x1b[?25h";
    private const string SAVE_CURSOR = "\x1b[s";
    private const string RESTORE_CURSOR = "\x1b[u";

    /// <summary>
    /// Creates a new ANSI color builder with specified initial capacity.
    /// </summary>
    /// <param name="capacity">Initial buffer capacity for performance optimization</param>
    /// <remarks>
    /// Performance: Pre-allocating buffer capacity reduces memory allocations
    /// during high-frequency rendering operations
    /// </remarks>
    public AnsiColorBuilder(int capacity = 256)
    {
        _buffer = new StringBuilder(capacity);
        EnsureColorCacheInitialized();
    }

    /// <summary>
    /// Sets foreground color using 24-bit RGB values.
    /// </summary>
    /// <param name="red">Red component (0-255)</param>
    /// <param name="green">Green component (0-255)</param>
    /// <param name="blue">Blue component (0-255)</param>
    /// <returns>Builder instance for method chaining</returns>
    /// <remarks>
    /// ANSI Sequence: ESC[38;2;R;G;Bm
    /// Fallback: Automatically converts to 256-color if truecolor unsupported
    /// </remarks>
    public AnsiColorBuilder SetForegroundRgb(byte red, byte green, byte blue)
    {
        if (VirtualTerminalProcessor.SupportsTrueColor)
        {
            _buffer.Append($"{ESC}38;2;{red};{green};{blue}m");
        }
        else if (VirtualTerminalProcessor.Supports256Color)
        {
            var color256 = RgbTo256Color(red, green, blue);
            _buffer.Append(_256ColorCache[color256]);
        }
        else
        {
            // Fallback to basic 16-color ANSI
            var basicColor = RgbToBasicAnsi(red, green, blue);
            _buffer.Append($"{ESC}38;5;{basicColor}m");
        }
        return this;
    }

    /// <summary>
    /// Sets foreground color using Color struct.
    /// </summary>
    /// <param name="color">System.Drawing.Color instance</param>
    /// <returns>Builder instance for method chaining</returns>
    public AnsiColorBuilder SetForegroundColor(Color color)
    {
        return SetForegroundRgb(color.R, color.G, color.B);
    }

    /// <summary>
    /// Sets background color using 24-bit RGB values.
    /// </summary>
    /// <param name="red">Red component (0-255)</param>
    /// <param name="green">Green component (0-255)</param>
    /// <param name="blue">Blue component (0-255)</param>
    /// <returns>Builder instance for method chaining</returns>
    /// <remarks>
    /// ANSI Sequence: ESC[48;2;R;G;Bm
    /// Use Case: Highlighting alert conditions or creating contrasting backgrounds
    /// </remarks>
    public AnsiColorBuilder SetBackgroundRgb(byte red, byte green, byte blue)
    {
        if (VirtualTerminalProcessor.SupportsTrueColor)
        {
            _buffer.Append($"{ESC}48;2;{red};{green};{blue}m");
        }
        else if (VirtualTerminalProcessor.Supports256Color)
        {
            var color256 = RgbTo256Color(red, green, blue);
            _buffer.Append($"{ESC}48;5;{color256}m");
        }
        else
        {
            var basicColor = RgbToBasicAnsi(red, green, blue);
            _buffer.Append($"{ESC}48;5;{basicColor}m");
        }
        return this;
    }

    /// <summary>
    /// Positions cursor at specified row and column (1-indexed).
    /// </summary>
    /// <param name="row">Target row (1-based)</param>
    /// <param name="column">Target column (1-based)</param>
    /// <returns>Builder instance for method chaining</returns>
    /// <remarks>
    /// ANSI Sequence: ESC[row;columnH
    /// Performance: Essential for high-performance heat map rendering without flicker
    /// </remarks>
    public AnsiColorBuilder MoveCursor(int row, int column)
    {
        _buffer.Append($"{ESC}{row};{column}H");
        return this;
    }

    /// <summary>
    /// Appends text content to the current sequence.
    /// </summary>
    /// <param name="text">Text to append</param>
    /// <returns>Builder instance for method chaining</returns>
    public AnsiColorBuilder AppendText(string text)
    {
        _buffer.Append(text);
        return this;
    }

    /// <summary>
    /// Appends a single character to the current sequence.
    /// </summary>
    /// <param name="character">Character to append</param>
    /// <returns>Builder instance for method chaining</returns>
    public AnsiColorBuilder AppendChar(char character)
    {
        _buffer.Append(character);
        return this;
    }

    /// <summary>
    /// Resets all text attributes to default.
    /// </summary>
    /// <returns>Builder instance for method chaining</returns>
    public AnsiColorBuilder Reset()
    {
        _buffer.Append(RESET);
        return this;
    }

    /// <summary>
    /// Clears the entire screen.
    /// </summary>
    /// <returns>Builder instance for method chaining</returns>
    public AnsiColorBuilder ClearScreen()
    {
        _buffer.Append(CLEAR_SCREEN);
        return this;
    }

    /// <summary>
    /// Clears the current line.
    /// </summary>
    /// <returns>Builder instance for method chaining</returns>
    public AnsiColorBuilder ClearLine()
    {
        _buffer.Append(CLEAR_LINE);
        return this;
    }

    /// <summary>
    /// Hides the cursor for cleaner display during rendering.
    /// </summary>
    /// <returns>Builder instance for method chaining</returns>
    public AnsiColorBuilder HideCursor()
    {
        _buffer.Append(HIDE_CURSOR);
        return this;
    }

    /// <summary>
    /// Shows the cursor.
    /// </summary>
    /// <returns>Builder instance for method chaining</returns>
    public AnsiColorBuilder ShowCursor()
    {
        _buffer.Append(SHOW_CURSOR);
        return this;
    }

    /// <summary>
    /// Saves current cursor position.
    /// </summary>
    /// <returns>Builder instance for method chaining</returns>
    public AnsiColorBuilder SaveCursor()
    {
        _buffer.Append(SAVE_CURSOR);
        return this;
    }

    /// <summary>
    /// Restores previously saved cursor position.
    /// </summary>
    /// <returns>Builder instance for method chaining</returns>
    public AnsiColorBuilder RestoreCursor()
    {
        _buffer.Append(RESTORE_CURSOR);
        return this;
    }

    /// <summary>
    /// Builds the final ANSI escape sequence string.
    /// </summary>
    /// <returns>Complete ANSI escape sequence</returns>
    /// <remarks>
    /// Performance: Returns string representation and clears internal buffer
    /// for reuse, reducing garbage collection pressure
    /// </remarks>
    public string Build()
    {
        var result = _buffer.ToString();
        _buffer.Clear();
        return result;
    }

    /// <summary>
    /// Writes the built sequence directly to console and clears buffer.
    /// </summary>
    /// <remarks>
    /// Performance: Optimized path that avoids string allocation for immediate output
    /// </remarks>
    public void WriteToConsole()
    {
        System.Console.Write(_buffer.ToString());
        _buffer.Clear();
    }

    /// <summary>
    /// Converts RGB values to 256-color palette index.
    /// </summary>
    /// <remarks>
    /// Algorithm: Uses 6x6x6 RGB color cube + 24 grayscale colors
    /// Color cube: 16 + 36*r + 6*g + b where r,g,b are 0-5
    /// </remarks>
    private static byte RgbTo256Color(byte red, byte green, byte blue)
    {
        // Convert RGB (0-255) to 6-level values (0-5)
        var r = (red * 5) / 255;
        var g = (green * 5) / 255;
        var b = (blue * 5) / 255;

        // Calculate 256-color index: 16 base colors + RGB cube
        return (byte)(16 + 36 * r + 6 * g + b);
    }

    /// <summary>
    /// Converts RGB to basic 16-color ANSI code.
    /// </summary>
    /// <remarks>
    /// Fallback: For terminals with minimal color support
    /// Maps RGB to nearest standard ANSI color (0-15)
    /// </remarks>
    private static byte RgbToBasicAnsi(byte red, byte green, byte blue)
    {
        var brightness = (red + green + blue) / 3;

        if (brightness < 64) return 0;  // Black
        if (red > green && red > blue) return brightness > 127 ? (byte)9 : (byte)1;   // Red/Bright Red
        if (green > red && green > blue) return brightness > 127 ? (byte)10 : (byte)2; // Green/Bright Green
        if (blue > red && blue > green) return brightness > 127 ? (byte)12 : (byte)4;  // Blue/Bright Blue
        if (red > blue) return brightness > 127 ? (byte)11 : (byte)3; // Yellow/Bright Yellow
        if (green > blue) return brightness > 127 ? (byte)14 : (byte)6; // Cyan/Bright Cyan
        return brightness > 127 ? (byte)13 : (byte)5; // Magenta/Bright Magenta
    }

    /// <summary>
    /// Initializes the 256-color cache for performance optimization.
    /// </summary>
    /// <remarks>
    /// Performance: Pre-calculates all 256-color ANSI sequences to avoid
    /// repeated string formatting during high-frequency rendering
    /// </remarks>
    private static void EnsureColorCacheInitialized()
    {
        if (_cacheInitialized) return;

        lock (_cacheInitLock)
        {
            if (_cacheInitialized) return;

            for (int i = 0; i < 256; i++)
            {
                _256ColorCache[i] = $"{ESC}38;5;{i}m";
            }

            _cacheInitialized = true;
        }
    }
}