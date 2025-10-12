using System;
using System.Threading;
using System.Threading.Tasks;
using GrapheneTrace.Terminal.Display;
using GrapheneTrace.Services.Mocking;
using GrapheneTrace.Core.Models;

namespace GrapheneTrace;

/// <summary>
/// Main program entry point for GrapheneTrace pressure monitoring demo.
/// </summary>
/// <remarks>
/// Demo Purpose: Interactive real-time pressure map visualization showcasing:
/// - Colored heat map rendering with gradient and discrete range modes
/// - Anatomically accurate pressure simulation
/// - Real-time alert detection
/// - Professional terminal-based interface
/// - Live metrics calculation and display
///
/// Keyboard Controls:
/// - [G] Switch to Gradient color mode
/// - [R] Switch to Range color mode
/// - [P] Pause/Resume simulation
/// - [Q] Quit application
/// - [1-4] Switch simulation scenarios
/// </remarks>
/// <author>
/// Thomas J. Kenner - tjk118@student.aru.ac.uk - 2412494
/// </author>
class Program
{
    private static HeatMapRenderer? _renderer;
    private static MockPressureDataGenerator? _dataGenerator;
    private static bool _isPaused = false;
    private static bool _shouldExit = false;
    private static readonly object _stateLock = new();

    // Performance tracking
    private static DateTime _lastFrameTime = DateTime.Now;
    private static double _currentFrameRate = 0.0;
    private static int _frameCount = 0;

    // Alert detection
    private const byte ALERT_THRESHOLD = 150;
    private static string _currentAlertStatus = "NORMAL";

    /// <summary>
    /// Application entry point.
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Exit code (0 for success)</returns>
    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.CancelKeyPress += OnCancelKeyPress;

            await ShowWelcomeMessage();
            await InitializeComponents();

            // Start keyboard input monitoring after initialization
            var keyboardTask = Task.Run(MonitorKeyboardInput);

            await RunVisualizationLoop();

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
            return 1;
        }
        finally
        {
            Cleanup();
        }
    }

    /// <summary>
    /// Displays welcome message and instructions.
    /// </summary>
    /// <remarks>
    /// Professional presentation for team selection demo
    /// Sets expectations and showcases system capabilities
    /// </remarks>
    private static async Task ShowWelcomeMessage()
    {
        Console.Clear();
        Console.WriteLine("╭─────────────────────────────────────────────────────────╮");
        Console.WriteLine("│                                                         │");
        Console.WriteLine("│              GrapheneTrace Sensore                      │");
        Console.WriteLine("│           Real-Time Pressure Monitoring                 │");
        Console.WriteLine("│                                                         │");
        Console.WriteLine("│  ● Anatomically accurate pressure simulation            │");
        Console.WriteLine("│  ● Real-time colored heat map visualization             │");
        Console.WriteLine("│  ● Intelligent alert detection and metrics              │");
        Console.WriteLine("│  ● Professional medical device interface                │");
        Console.WriteLine("│                                                         │");
        Console.WriteLine("│  Controls:                                              │");
        Console.WriteLine("│  [G]radient Mode      [R]ange Mode                      │");
        Console.WriteLine("│  [P]ause/Resume       [Q]uit                            │");
        Console.WriteLine("│  [1-4] Demo Scenarios                                   │");
        Console.WriteLine("│                                                         │");
        Console.WriteLine("│  Starting demonstration in 3 seconds...                 │");
        Console.WriteLine("│                                                         │");
        Console.WriteLine("╰─────────────────────────────────────────────────────────╯");

        await Task.Delay(3000);
    }

    /// <summary>
    /// Initializes all system components.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if initialization fails</exception>
    /// <remarks>
    /// Initialization Order:
    /// 1. Virtual terminal processing
    /// 2. Heat map renderer with gradient mode
    /// 3. Mock pressure data generator with sequential demo
    /// </remarks>
    private static async Task InitializeComponents()
    {
        // Initialize virtual terminal processing
        if (!VirtualTerminalProcessor.Initialize())
        {
            throw new InvalidOperationException("Failed to initialize terminal display capabilities");
        }

        // Initialize heat map renderer with gradient mode
        _renderer = new HeatMapRenderer(ColorMapper.ColorMode.Gradient, useUnicodeChars: true);

        if (!_renderer.Initialize())
        {
            throw new InvalidOperationException("Failed to initialize heat map renderer");
        }

        // Initialize mock data generator with sequential demo scenario
        _dataGenerator = new MockPressureDataGenerator(
            MockPressureDataGenerator.SimulationScenario.SequentialDemo,
            seed: 42 // Reproducible demo
        );

        await Task.Delay(100); // Allow initialization to complete
    }

    /// <summary>
    /// Main visualization loop with real-time rendering.
    /// </summary>
    /// <remarks>
    /// Loop Structure:
    /// 1. Generate current pressure map
    /// 2. Calculate real-time metrics
    /// 3. Detect alert conditions
    /// 4. Render heat map with metrics
    /// 5. Update performance counters
    /// 6. Frame rate limiting (target 10 FPS)
    ///
    /// Performance: Optimized for smooth real-time display
    /// Error Handling: Graceful degradation on render failures
    /// </remarks>
    private static async Task RunVisualizationLoop()
    {
        const int TARGET_FPS = 10;
        const int FRAME_DELAY_MS = 1000 / TARGET_FPS;

        var frameTimer = DateTime.Now;

        while (!_shouldExit)
        {
            try
            {
                if (!_isPaused && _dataGenerator != null && _renderer != null)
                {
                    // Generate current pressure data
                    var pressureMap = _dataGenerator.GenerateCurrentPressureMap();

                    // Calculate real-time metrics
                    var metrics = CalculateRealTimeMetrics(pressureMap);

                    // Render the heat map with metrics
                    _renderer.RenderWithMetrics(
                        pressureMap,
                        metrics.PeakPressure,
                        metrics.ContactAreaPercentage,
                        metrics.AlertStatus,
                        _currentFrameRate
                    );

                    // Update performance counters
                    UpdatePerformanceMetrics();
                }

                // Frame rate limiting
                var elapsed = DateTime.Now - frameTimer;
                var remainingTime = FRAME_DELAY_MS - (int)elapsed.TotalMilliseconds;

                if (remainingTime > 0)
                {
                    await Task.Delay(remainingTime);
                }

                frameTimer = DateTime.Now;
            }
            catch (Exception ex)
            {
                // Log error but continue execution for demo stability
                System.Diagnostics.Debug.WriteLine($"Render loop error: {ex.Message}");
                await Task.Delay(100); // Brief pause on error
            }
        }
    }

    /// <summary>
    /// Calculates real-time metrics from pressure map data.
    /// </summary>
    /// <param name="pressureMap">Current pressure map</param>
    /// <returns>Calculated metrics</returns>
    /// <remarks>
    /// Metrics Calculated:
    /// - Peak Pressure: Maximum pressure value in matrix
    /// - Contact Area: Percentage of cells with significant pressure (>10% of max)
    /// - Alert Status: Based on pressure threshold analysis
    /// </remarks>
    private static (byte PeakPressure, double ContactAreaPercentage, string AlertStatus)
        CalculateRealTimeMetrics(PressureMap pressureMap)
    {
        byte peakPressure = 0;
        int contactCells = 0;
        int totalCells = PressureMap.MATRIX_SIZE * PressureMap.MATRIX_SIZE;

        // Scan entire pressure matrix
        for (int row = 0; row < PressureMap.MATRIX_SIZE; row++)
        {
            for (int col = 0; col < PressureMap.MATRIX_SIZE; col++)
            {
                var pressure = pressureMap.GetPressure(row, col);

                if (pressure > peakPressure)
                    peakPressure = pressure;

                // Count cells with significant pressure (>25% of max possible)
                if (pressure > 64) // ~25% of 255
                    contactCells++;
            }
        }

        var contactAreaPercentage = (double)contactCells / totalCells * 100.0;

        // Determine alert status
        var alertStatus = peakPressure >= ALERT_THRESHOLD ? "HIGH PRESSURE ALERT" : "NORMAL";

        lock (_stateLock)
        {
            _currentAlertStatus = alertStatus;
        }

        return (peakPressure, contactAreaPercentage, alertStatus);
    }

    /// <summary>
    /// Updates frame rate and performance metrics.
    /// </summary>
    /// <remarks>
    /// Calculation: Rolling average over last 10 frames for stability
    /// Display: Shows real-time FPS in metrics area
    /// </remarks>
    private static void UpdatePerformanceMetrics()
    {
        var now = DateTime.Now;
        var frameDelta = now - _lastFrameTime;

        if (frameDelta.TotalMilliseconds > 0)
        {
            var instantFrameRate = 1000.0 / frameDelta.TotalMilliseconds;

            // Rolling average for stability
            _currentFrameRate = (_currentFrameRate * 0.9) + (instantFrameRate * 0.1);

            _frameCount++;
        }

        _lastFrameTime = now;
    }

    /// <summary>
    /// Monitors keyboard input for user commands.
    /// </summary>
    /// <remarks>
    /// Input Handling: Non-blocking keyboard monitoring
    /// Commands: Real-time mode switching and control
    /// Thread Safety: Uses locks for state changes
    /// </remarks>
    private static async Task MonitorKeyboardInput()
    {
        while (!_shouldExit)
        {
            if (Console.KeyAvailable)
            {
                var keyInfo = Console.ReadKey(true);

                lock (_stateLock)
                {
                    HandleKeyPress(keyInfo.Key);
                }
            }

            await Task.Delay(50); // Reduce CPU usage while monitoring
        }
    }

    /// <summary>
    /// Handles individual key press commands.
    /// </summary>
    /// <param name="key">Pressed key</param>
    /// <remarks>
    /// Command Processing:
    /// - G: Switch to gradient color mode
    /// - R: Switch to discrete range color mode
    /// - P: Toggle pause/resume
    /// - Q: Quit application
    /// - 1-4: Switch simulation scenarios
    /// </remarks>
    private static void HandleKeyPress(ConsoleKey key)
    {
        switch (key)
        {
            case ConsoleKey.G:
                _renderer?.SwitchColorMode(ColorMapper.ColorMode.Gradient);
                break;

            case ConsoleKey.R:
                _renderer?.SwitchColorMode(ColorMapper.ColorMode.DiscreteRanges);
                break;

            case ConsoleKey.P:
                _isPaused = !_isPaused;
                break;

            case ConsoleKey.Q:
                _shouldExit = true;
                break;

            case ConsoleKey.D1:
                _dataGenerator?.SetScenario(MockPressureDataGenerator.SimulationScenario.NormalSitting);
                break;

            case ConsoleKey.D2:
                _dataGenerator?.SetScenario(MockPressureDataGenerator.SimulationScenario.PressureBuildupAlert);
                break;

            case ConsoleKey.D3:
                _dataGenerator?.SetScenario(MockPressureDataGenerator.SimulationScenario.WeightShiftingRelief);
                break;

            case ConsoleKey.D4:
                _dataGenerator?.SetScenario(MockPressureDataGenerator.SimulationScenario.SequentialDemo);
                break;
        }
    }

    /// <summary>
    /// Handles Ctrl+C termination signal.
    /// </summary>
    /// <param name="sender">Event sender</param>
    /// <param name="e">Cancel event args</param>
    /// <remarks>
    /// Graceful Shutdown: Ensures proper cleanup on forced termination
    /// User Experience: Prevents terminal corruption on exit
    /// </remarks>
    private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true; // Prevent immediate termination

        lock (_stateLock)
        {
            _shouldExit = true;
        }
    }

    /// <summary>
    /// Cleans up system resources and restores terminal state.
    /// </summary>
    /// <remarks>
    /// Cleanup Tasks:
    /// - Restore terminal cursor and colors
    /// - Clean renderer resources
    /// - Position cursor for clean exit
    ///
    /// Critical: Must be called to prevent terminal corruption
    /// </remarks>
    private static void Cleanup()
    {
        try
        {
            _renderer?.Cleanup();

            Console.Clear();
            Console.WriteLine("\n");
            Console.WriteLine("╭─────────────────────────────────────────────╮");
            Console.WriteLine("│                                             │");
            Console.WriteLine("│          GrapheneTrace Demo Complete        │");
            Console.WriteLine("│                                             │");
            Console.WriteLine("│    Thank you for viewing the demonstration  │");
            Console.WriteLine("│                                             │");
            Console.WriteLine("╰─────────────────────────────────────────────╯");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cleanup error: {ex.Message}");
        }
    }
}

