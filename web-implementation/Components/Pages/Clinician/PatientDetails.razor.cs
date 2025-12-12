using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using GrapheneTrace.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;

namespace GrapheneTrace.Web.Components.Pages.Clinician;

/// <summary>
/// Clinician Patient Details Page Code-Behind
/// Author: 2402513
///
/// Purpose:
/// Manages the data loading and interaction logic for the Patient Details page.
/// Verifies clinician access to the patient, loads patient information, sessions,
/// and comments. Handles heatmap playback initialization and session selection.
/// </summary>
public partial class PatientDetails
{
    [Parameter] public Guid PatientId { get; set; }

    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private ApplicationDbContext DbContext { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private PressureDataService PressureDataService { get; set; } = default!;
    [Inject] private HeatmapPlaybackService PlaybackService { get; set; } = default!;
    [Inject] private PatientSettingsService PatientSettingsService { get; set; } = default!;
    [Inject] private PressureCommentService PressureCommentService { get; set; } = default!;

    private bool isLoading = true;
    private Guid clinicianId = Guid.Empty;
    private ApplicationUser? patient;
    private PatientSettings? patientSettings;
    private List<PatientSessionData>? sessions;
    private PatientSessionData? selectedSession;
    private int? selectedSessionId;
    private List<PressureComment>? comments;
    private string canvasId = $"heatmap-{Guid.NewGuid():N}";
    private bool isInitialized;
    private bool isPlaybackInitialized;
    private bool isDragging;

    protected override async Task OnInitializedAsync()
    {
        await LoadClinicianId();
        await LoadPatientData();
        isLoading = false;
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            isInitialized = true;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Loads the clinician ID from the authenticated user.
    /// Author: 2402513
    /// </summary>
    private async Task LoadClinicianId()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        var clinicianIdClaim = user.FindFirst("sub")?.Value;
        if (Guid.TryParse(clinicianIdClaim, out var id))
        {
            clinicianId = id;
            return;
        }

        var username = user.Identity?.Name;
        if (!string.IsNullOrEmpty(username))
        {
            var clinician = await DbContext.Users
                .FirstOrDefaultAsync(u => u.UserName == username && u.UserType == "clinician");
            if (clinician != null)
            {
                clinicianId = clinician.Id;
                return;
            }
        }

        var email = user.FindFirst("email")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (!string.IsNullOrEmpty(email))
        {
            var clinician = await DbContext.Users
                .FirstOrDefaultAsync(u => u.Email == email && u.UserType == "clinician");
            if (clinician != null)
            {
                clinicianId = clinician.Id;
            }
        }
    }

    /// <summary>
    /// Loads patient data and verifies clinician has access.
    /// Author: 2402513
    /// </summary>
    /// <remarks>
    /// Verifies that the patient is assigned to the logged-in clinician via
    /// the PatientClinician table. If not, patient will be null and an
    /// error message will be displayed.
    /// </remarks>
    private async Task LoadPatientData()
    {
        if (clinicianId == Guid.Empty || PatientId == Guid.Empty)
        {
            patient = null;
            return;
        }

        // Verify clinician has access to this patient
        var hasAccess = await DbContext.PatientClinicians
            .AnyAsync(pc => pc.ClinicianId == clinicianId && 
                           pc.PatientId == PatientId && 
                           pc.UnassignedAt == null);

        if (!hasAccess)
        {
            patient = null;
            return;
        }

        // Load patient
        patient = await DbContext.Users
            .FirstOrDefaultAsync(u => u.Id == PatientId && u.UserType == "patient");

        if (patient == null)
        {
            return;
        }

        // Load patient settings
        patientSettings = await PatientSettingsService.GetSettingsAsync(PatientId);

        // Load sessions
        sessions = await PressureDataService.GetSessionsForPatientAsync(PatientId);

        // Auto-select first session if available
        if (sessions != null && sessions.Count > 0)
        {
            selectedSessionId = sessions[0].SessionId;
            selectedSession = sessions[0];
            _ = SelectSessionAsync(sessions[0]);
        }

        // Load comments
        comments = await PressureCommentService.GetCommentsForPatientAsync(PatientId);
    }

    /// <summary>
    /// Handles session selection change from dropdown.
    /// Author: 2402513
    /// </summary>
    private async Task OnSessionChangedAsync()
    {
        if (selectedSessionId.HasValue)
        {
            var session = sessions?.FirstOrDefault(s => s.SessionId == selectedSessionId.Value);
            if (session != null)
            {
                await SelectSessionAsync(session);
            }
        }
    }

    /// <summary>
    /// Selects a session and initializes heatmap playback.
    /// Author: 2402513
    /// </summary>
    private async Task SelectSessionAsync(PatientSessionData session)
    {
        if (selectedSessionId == session.SessionId)
            return;

        selectedSession = session;
        selectedSessionId = session.SessionId;
        StateHasChanged();

        // Wait for render if needed
        if (!isInitialized)
        {
            await Task.Delay(100);
        }

        // Initialize playback service if not already done
        if (!isPlaybackInitialized)
        {
            await PlaybackService.InitializeAsync(canvasId);
            isPlaybackInitialized = true;
        }
        else
        {
            // Pause current playback before loading new session
            await PlaybackService.PausePlaybackAsync();
        }

        // Load the session into the playback service
        var loaded = await PlaybackService.LoadSessionAsync(session.SessionId);
        if (loaded)
        {
            // Subscribe to frame changes to update UI
            PlaybackService.OnFrameChanged += OnFrameChanged;

            // Start playback (loops automatically)
            PlaybackService.StartPlayback();
        }

        StateHasChanged();
    }

    /// <summary>
    /// Handles frame change events from playback service.
    /// Author: 2402513
    /// </summary>
    private void OnFrameChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Calculates progress percentage for playback progress bar.
    /// Author: 2402513
    /// </summary>
    private double GetProgressPercent()
    {
        if (PlaybackService.TotalFrames == 0)
            return 0;
        return (double)PlaybackService.CurrentFrameIndex / (PlaybackService.TotalFrames - 1) * 100;
    }

    /// <summary>
    /// Handles mouse down on progress bar for seeking.
    /// Author: 2402513
    /// </summary>
    private async Task OnProgressMouseDown(MouseEventArgs e)
    {
        isDragging = true;
        await PlaybackService.PausePlaybackAsync();
        await SeekToMousePosition(e);
    }

    /// <summary>
    /// Handles mouse move on progress bar while dragging.
    /// Author: 2402513
    /// </summary>
    private async Task OnProgressMouseMove(MouseEventArgs e)
    {
        if (!isDragging)
            return;
        await SeekToMousePosition(e);
    }

    /// <summary>
    /// Handles mouse up on progress bar.
    /// Author: 2402513
    /// </summary>
    private void OnProgressMouseUp(MouseEventArgs e)
    {
        isDragging = false;
    }

    /// <summary>
    /// Seeks to a position in the playback based on mouse click.
    /// Author: 2402513
    /// </summary>
    private async Task SeekToMousePosition(MouseEventArgs e)
    {
        const double trackWidth = 400.0;
        var clickPercent = Math.Clamp(e.OffsetX / trackWidth, 0, 1);
        var targetFrame = (int)(clickPercent * (PlaybackService.TotalFrames - 1));
        await PlaybackService.SeekToFrameAsync(targetFrame);
        StateHasChanged();
    }

    /// <summary>
    /// Toggles playback between play and pause.
    /// Author: 2402513
    /// </summary>
    private async Task TogglePlayback()
    {
        await PlaybackService.TogglePlaybackAsync();
        StateHasChanged();
    }

    /// <summary>
    /// Formats duration between two dates.
    /// Author: 2402513
    /// </summary>
    private string FormatDuration(DateTime start, DateTime? end)
    {
        if (!end.HasValue)
            return "Duration unknown";

        var duration = end.Value - start;
        if (duration.TotalHours >= 1)
            return $"{duration.Hours}h {duration.Minutes}m";
        if (duration.TotalMinutes >= 1)
            return $"{duration.Minutes}m {duration.Seconds}s";
        return $"{duration.Seconds}s";
    }

    /// <summary>
    /// Gets CSS class for pressure value based on thresholds.
    /// Author: 2402513
    /// </summary>
    private string GetPressureClass(int pressure)
    {
        if (patientSettings == null)
            return "";

        if (pressure >= patientSettings.HighPressureThreshold)
            return "pressure-high";
        if (pressure >= patientSettings.LowPressureThreshold)
            return "pressure-medium";
        return "pressure-low";
    }

    /// <summary>
    /// Navigates back to the Patient List page.
    /// Author: 2402513
    /// </summary>
    private void GoBackToPatientList()
    {
        Navigation.NavigateTo("/clinician/patient-list");
    }

    /// <summary>
    /// Cleans up playback service on component disposal.
    /// Author: 2402513
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        PlaybackService.OnFrameChanged -= OnFrameChanged;
        await PlaybackService.DisposeAsync();
    }
}

