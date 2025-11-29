using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace GrapheneTrace.Web.Services;

/// <summary>
/// Manages playback of pressure heatmap sessions on a per-heatmap basis.
/// Loads session frames from database and renders them via JS interop.
/// Author: SID:2412494
/// </summary>
public class HeatmapPlaybackService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly int _minValue;
    private readonly int _maxValue;

    private string? _canvasId;
    private int? _currentSessionId;
    private List<int[]>? _frames; // Each frame is 1024 ints (32x32 row-major)
    private int _currentFrameIndex;
    private PeriodicTimer? _playbackTimer;
    private CancellationTokenSource? _playbackCts;
    private Task? _playbackTask;
    private double _framesPerSecond = 15.0;
    private bool _isPlaying;
    private HeatmapRenderMode _renderMode = HeatmapRenderMode.Gradient;
    private bool _isInitialized;

    public HeatmapPlaybackService(
        IJSRuntime jsRuntime,
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IConfiguration configuration)
    {
        _jsRuntime = jsRuntime;
        _dbContextFactory = dbContextFactory;

        // Load min/max from appsettings
        _minValue = configuration.GetValue("PressureThresholds:MinValue", 1);
        _maxValue = configuration.GetValue("PressureThresholds:MaxValue", 255);
    }

    /// <summary>Current frame index (0-based)</summary>
    public int CurrentFrameIndex => _currentFrameIndex;

    /// <summary>Total number of frames in the loaded session</summary>
    public int TotalFrames => _frames?.Count ?? 0;

    /// <summary>Whether playback is currently running</summary>
    public bool IsPlaying => _isPlaying;

    /// <summary>Current playback speed in frames per second</summary>
    public double FramesPerSecond => _framesPerSecond;

    /// <summary>Current render mode (Discrete or Gradient)</summary>
    public HeatmapRenderMode RenderMode => _renderMode;

    /// <summary>The currently loaded session ID, if any</summary>
    public int? CurrentSessionId => _currentSessionId;

    /// <summary>Event raised when the frame changes during playback</summary>
    public event Action? OnFrameChanged;

    /// <summary>
    /// Initialize the JS heatmap renderer for a canvas element.
    /// Must be called before any other operations.
    /// </summary>
    /// <param name="canvasId">The HTML canvas element ID</param>
    /// <param name="cellSize">Pixel size per cell (default 10)</param>
    public async Task InitializeAsync(string canvasId, int cellSize = 10)
    {
        _canvasId = canvasId;
        await _jsRuntime.InvokeVoidAsync(
            "heatmapRenderer.initialize",
            canvasId, _minValue, _maxValue, cellSize);
        _isInitialized = true;
    }

    /// <summary>
    /// Load all frames for a session from the database.
    /// Stops any current playback and resets to frame 0.
    /// </summary>
    /// <param name="sessionId">The session primary key</param>
    /// <returns>True if session was found and loaded</returns>
    public async Task<bool> LoadSessionAsync(int sessionId)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Call InitializeAsync before LoadSessionAsync");

        // Stop current playback if running
        await StopPlaybackInternalAsync();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Load all snapshots for the session in one query, ordered by time
        var snapshots = await dbContext.PatientSnapshotDatas
            .Where(s => s.SessionId == sessionId)
            .OrderBy(s => s.SnapshotTime)
            .ThenBy(s => s.SnapshotId)
            .Select(s => s.SnapshotData)
            .ToListAsync();

        if (snapshots.Count == 0)
        {
            _frames = null;
            _currentSessionId = null;
            return false;
        }

        // Parse all frames into flat int arrays
        _frames = snapshots.Select(ParseSnapshotToFlatArray).ToList();
        _currentSessionId = sessionId;
        _currentFrameIndex = 0;

        // Render the first frame immediately
        await RenderCurrentFrameAsync();

        return true;
    }

    /// <summary>
    /// Start or resume playback from the current frame.
    /// </summary>
    public void StartPlayback()
    {
        if (!_isInitialized || _frames == null || _frames.Count == 0)
            return;

        if (_isPlaying)
            return;

        _isPlaying = true;
        _playbackCts = new CancellationTokenSource();
        _playbackTimer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / _framesPerSecond));
        _playbackTask = RunPlaybackLoopAsync(_playbackCts.Token);
    }

    /// <summary>
    /// Pause playback at the current frame.
    /// </summary>
    public async Task PausePlaybackAsync()
    {
        await StopPlaybackInternalAsync();
    }

    /// <summary>
    /// Toggle between playing and paused states.
    /// </summary>
    public async Task TogglePlaybackAsync()
    {
        if (_isPlaying)
            await PausePlaybackAsync();
        else
            StartPlayback();
    }

    /// <summary>
    /// Set the playback speed. Takes effect on next frame.
    /// </summary>
    /// <param name="fps">Frames per second (default is 15)</param>
    public async Task SetPlaybackSpeedAsync(double fps)
    {
        if (fps <= 0)
            throw new ArgumentOutOfRangeException(nameof(fps), "FPS must be positive");

        _framesPerSecond = fps;

        // If currently playing, restart with new speed
        if (_isPlaying)
        {
            await StopPlaybackInternalAsync();
            StartPlayback();
        }
    }

    /// <summary>
    /// Seek to a specific frame index.
    /// </summary>
    /// <param name="frameIndex">Zero-based frame index</param>
    public async Task SeekToFrameAsync(int frameIndex)
    {
        if (_frames == null || _frames.Count == 0)
            return;

        _currentFrameIndex = Math.Clamp(frameIndex, 0, _frames.Count - 1);
        await RenderCurrentFrameAsync();
        OnFrameChanged?.Invoke();
    }

    /// <summary>
    /// Set the render mode (Discrete or Gradient).
    /// </summary>
    /// <param name="mode">The render mode to use</param>
    public async Task SetRenderModeAsync(HeatmapRenderMode mode)
    {
        _renderMode = mode;

        // Re-render current frame with new mode
        if (_frames != null && _frames.Count > 0)
        {
            await RenderCurrentFrameAsync();
        }
    }

    private async Task RunPlaybackLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _playbackTimer != null)
            {
                if (!await _playbackTimer.WaitForNextTickAsync(ct))
                    break;

                // Advance frame (loop back to start if at end)
                _currentFrameIndex++;
                if (_currentFrameIndex >= _frames!.Count)
                    _currentFrameIndex = 0;

                await RenderCurrentFrameAsync();
                OnFrameChanged?.Invoke();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
    }

    private async Task RenderCurrentFrameAsync()
    {
        if (_frames == null || _canvasId == null || _currentFrameIndex >= _frames.Count)
            return;

        var frame = _frames[_currentFrameIndex];
        var methodName = _renderMode == HeatmapRenderMode.Discrete
            ? "heatmapRenderer.renderDiscrete"
            : "heatmapRenderer.renderGradient";

        await _jsRuntime.InvokeVoidAsync(methodName, _canvasId, frame);
    }

    private async Task StopPlaybackInternalAsync()
    {
        _isPlaying = false;

        if (_playbackCts != null)
        {
            await _playbackCts.CancelAsync();

            if (_playbackTask != null)
            {
                try
                {
                    await Task.WhenAny(_playbackTask, Task.Delay(500));
                }
                catch
                {
                    // Ignore exceptions during shutdown
                }
            }

            _playbackCts.Dispose();
            _playbackCts = null;
        }

        _playbackTimer?.Dispose();
        _playbackTimer = null;
        _playbackTask = null;
    }

    /// <summary>
    /// Parse snapshot data string to flat 1024-element int array (row-major).
    /// </summary>
    private static int[] ParseSnapshotToFlatArray(string snapshotData)
    {
        var result = new int[1024];
        int index = 0;

        using var reader = new StringReader(snapshotData);
        string? line;
        while ((line = reader.ReadLine()) != null && index < 1024)
        {
            var parts = line.Split(',');
            foreach (var part in parts)
            {
                if (index >= 1024) break;
                if (int.TryParse(part.Trim(), out int value))
                    result[index++] = value;
                else
                    result[index++] = 0;
            }
        }

        return result;
    }

    public async ValueTask DisposeAsync()
    {
        await StopPlaybackInternalAsync();

        if (_isInitialized && _canvasId != null)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("heatmapRenderer.dispose", _canvasId);
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected, can't call JS
            }
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Render mode for the heatmap visualization.
/// </summary>
public enum HeatmapRenderMode
{
    /// <summary>7 discrete color bands from blue to red</summary>
    Discrete,

    /// <summary>Smooth gradient interpolation between colors</summary>
    Gradient
}