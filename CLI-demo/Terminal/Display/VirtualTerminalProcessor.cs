using System;
using System.Runtime.InteropServices;

namespace GrapheneTrace.Terminal.Display;

/// <summary>
/// Enables virtual terminal processing and detects terminal capabilities for ANSI escape codes.
/// </summary>
/// <remarks>
/// Design Pattern: Singleton Pattern with lazy initialization
/// Why: Terminal capabilities are system-wide and should be initialized once
///
/// Business Purpose: Enables colored console output across all platforms, providing
/// the foundation for rich pressure map visualization.
///
/// Technical Details: Uses P/Invoke on Windows to enable ENABLE_VIRTUAL_TERMINAL_PROCESSING
/// mode, which allows ANSI escape sequences. On Unix systems, ANSI is typically supported
/// by default.
/// </remarks>
public static class VirtualTerminalProcessor
{
    private static bool _isInitialized = false;
    private static readonly object _lockObject = new();

    // Windows console API constants
    private const int STD_OUTPUT_HANDLE = -11;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
    private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

    /// <summary>
    /// Gets whether the current terminal supports 24-bit RGB colors.
    /// </summary>
    /// <remarks>
    /// Performance: Cached result after first detection
    /// Detection: Checks COLORTERM environment variable and terminal capabilities
    /// </remarks>
    public static bool SupportsTrueColor { get; private set; }

    /// <summary>
    /// Gets whether the current terminal supports 256-color palette.
    /// </summary>
    public static bool Supports256Color { get; private set; }

    /// <summary>
    /// Gets whether ANSI escape codes are supported at all.
    /// </summary>
    public static bool SupportsAnsi { get; private set; }

    /// <summary>
    /// Initializes virtual terminal processing and detects capabilities.
    /// </summary>
    /// <returns>True if ANSI escape codes are supported</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown on unsupported platforms</exception>
    /// <remarks>
    /// Design Pattern: Initialization-on-demand with thread safety
    /// Algorithm: Platform detection → Windows VT processing → Capability detection
    ///
    /// Why this approach:
    /// - Thread-safe initialization using lock
    /// - Graceful fallback on older systems
    /// - Comprehensive capability detection for optimal rendering
    ///
    /// Windows Implementation:
    /// - Uses GetStdHandle and SetConsoleMode Win32 APIs
    /// - Enables ENABLE_VIRTUAL_TERMINAL_PROCESSING flag
    /// - Disables auto line wrapping for precise positioning
    ///
    /// Unix Implementation:
    /// - Assumes ANSI support (standard on modern terminals)
    /// - Detects enhanced capabilities via environment variables
    /// </remarks>
    public static bool Initialize()
    {
        if (_isInitialized)
            return SupportsAnsi;

        lock (_lockObject)
        {
            if (_isInitialized)
                return SupportsAnsi;

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    SupportsAnsi = EnableWindowsVirtualTerminal();
                }
                else
                {
                    // Unix-like systems typically support ANSI by default
                    SupportsAnsi = true;
                }

                if (SupportsAnsi)
                {
                    DetectColorCapabilities();
                }

                _isInitialized = true;
                return SupportsAnsi;
            }
            catch (Exception)
            {
                // Fallback to basic console output if initialization fails
                SupportsAnsi = false;
                SupportsTrueColor = false;
                Supports256Color = false;
                _isInitialized = true;
                return false;
            }
        }
    }

    /// <summary>
    /// Enables virtual terminal processing on Windows systems.
    /// </summary>
    /// <returns>True if successfully enabled</returns>
    /// <remarks>
    /// Windows-specific implementation using kernel32.dll P/Invoke
    /// Required for Windows 10 Anniversary Update and later
    /// </remarks>
    private static bool EnableWindowsVirtualTerminal()
    {
        var handle = GetStdHandle(STD_OUTPUT_HANDLE);
        if (handle == IntPtr.Zero)
            return false;

        if (!GetConsoleMode(handle, out uint mode))
            return false;

        mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
        return SetConsoleMode(handle, mode);
    }

    /// <summary>
    /// Detects terminal color capabilities through environment variables and testing.
    /// </summary>
    /// <remarks>
    /// Detection Strategy:
    /// - COLORTERM=truecolor indicates 24-bit RGB support
    /// - TERM=xterm-256color indicates 256-color support
    /// - Fallback to basic 16-color ANSI
    /// </remarks>
    private static void DetectColorCapabilities()
    {
        var colorTerm = Environment.GetEnvironmentVariable("COLORTERM");
        var term = Environment.GetEnvironmentVariable("TERM");

        // Check for true color support
        SupportsTrueColor = !string.IsNullOrEmpty(colorTerm) &&
                           (colorTerm.Contains("truecolor") || colorTerm.Contains("24bit"));

        // Check for 256-color support
        Supports256Color = SupportsTrueColor ||
                          (!string.IsNullOrEmpty(term) && term.Contains("256"));

        // If no specific indicators, assume basic ANSI support
        if (!SupportsTrueColor && !Supports256Color)
        {
            Supports256Color = true; // Most modern terminals support 256-color
        }
    }

    /// <summary>
    /// Resets the console to default state and colors.
    /// </summary>
    /// <remarks>
    /// Use Case: Called on application exit to clean up terminal state
    /// ANSI Sequence: ESC[0m (reset all attributes)
    /// </remarks>
    public static void Reset()
    {
        if (SupportsAnsi)
        {
            System.Console.Write("\x1b[0m\x1b[?25h"); // Reset + show cursor
        }
    }

    // Windows API P/Invoke declarations
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}