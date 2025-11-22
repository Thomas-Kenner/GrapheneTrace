using Microsoft.JSInterop;

namespace GrapheneTrace.Web.Services;

/// <summary>
/// Service for managing browser notifications via JavaScript Interop.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Purpose: Provides OS-level desktop notifications for critical alerts (pressure thresholds,
/// equipment faults, etc.) that appear even when the browser is minimized or in the background.
///
/// Dependencies:
/// - IJSRuntime: Blazor JavaScript interop for calling browser Notification API
/// - notificationHandler.js: JavaScript module with notification functions
///
/// Design Pattern: Service layer wraps JavaScript Interop for type-safe notification access.
/// This allows components to request notifications without dealing with JS interop directly.
///
/// Related User Stories:
/// - Story #7: Clinician notifications for peak pressure index alerts
/// - Story #10: Patient alerts for threshold exceedance
/// - Story #18: Equipment fault/sensor issue notifications
/// - Story #28: Direct clinician-to-patient notifications
///
/// Methods:
/// - IsNotificationSupportedAsync(): Checks if browser supports Notification API
/// - GetPermissionAsync(): Gets current notification permission status
/// - RequestPermissionAsync(): Requests user permission to show notifications
/// - ShowNotificationAsync(): Shows a desktop notification with system sound
/// </remarks>
public class NotificationService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    /// <summary>
    /// Initializes the NotificationService with IJSRuntime dependency.
    /// Author: SID:2412494
    /// </summary>
    public NotificationService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Ensures the JavaScript module is loaded lazily on first use.
    /// Author: SID:2412494
    /// </summary>
    /// <remarks>
    /// Why lazy loading: JavaScript modules cannot be imported during prerendering.
    /// Loading on first use ensures we only import when actually needed in interactive mode.
    /// </remarks>
    private async Task EnsureModuleAsync()
    {
        if (_module == null)
        {
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./js/notificationHandler.js");
        }
    }

    /// <summary>
    /// Checks if the browser supports the Notification API.
    /// Author: SID:2412494
    /// </summary>
    /// <returns>True if notifications are supported, false otherwise</returns>
    /// <remarks>
    /// Use this to check browser compatibility before requesting permission or showing notifications.
    /// Most modern browsers support notifications, but some older browsers or privacy-focused
    /// configurations may not.
    /// </remarks>
    public async Task<bool> IsNotificationSupportedAsync()
    {
        try
        {
            await EnsureModuleAsync();
            return await _module!.InvokeAsync<bool>("isNotificationSupported");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the current notification permission status.
    /// Author: SID:2412494
    /// </summary>
    /// <returns>"granted", "denied", or "default"</returns>
    /// <remarks>
    /// Permission states:
    /// - "granted": User has granted permission, notifications will be shown
    /// - "denied": User has denied permission, notifications will be blocked
    /// - "default": User has not been asked yet, need to call RequestPermissionAsync()
    ///
    /// Use this to check if you need to request permission before attempting to show notifications.
    /// </remarks>
    public async Task<string> GetPermissionAsync()
    {
        try
        {
            await EnsureModuleAsync();
            return await _module!.InvokeAsync<string>("getNotificationPermission");
        }
        catch
        {
            return "denied";
        }
    }

    /// <summary>
    /// Requests permission from the user to show notifications.
    /// Author: SID:2412494
    /// </summary>
    /// <returns>"granted", "denied", or "default"</returns>
    /// <remarks>
    /// Best practices:
    /// - Only request permission in response to a user action (button click, etc.)
    /// - Explain WHY you need notifications before requesting permission
    /// - Once user grants or denies, browser will remember the choice
    /// - If denied, user must manually change permission in browser settings
    ///
    /// Example usage:
    /// - Show a prompt explaining pressure monitoring alerts
    /// - User clicks "Enable Notifications" button
    /// - Call this method to trigger browser permission dialog
    /// </remarks>
    public async Task<string> RequestPermissionAsync()
    {
        try
        {
            await EnsureModuleAsync();
            return await _module!.InvokeAsync<string>("requestNotificationPermission");
        }
        catch
        {
            return "denied";
        }
    }

    /// <summary>
    /// Shows a desktop notification with system sound.
    /// Author: SID:2412494
    /// </summary>
    /// <param name="title">The notification title (required)</param>
    /// <param name="body">The notification body text (optional)</param>
    /// <param name="icon">URL to an icon image (optional, defaults to favicon)</param>
    /// <param name="tag">Unique tag to identify the notification (optional)</param>
    /// <param name="requireInteraction">Keep notification visible until user interacts (optional)</param>
    /// <returns>True if notification was shown successfully, false otherwise</returns>
    /// <remarks>
    /// Behavior:
    /// - Shows OS-level desktop notification (Windows Action Center, macOS Notification Center, etc.)
    /// - Plays system notification sound automatically (unless user has disabled in OS settings)
    /// - Notification appears even if browser is minimized or tab is in background
    /// - Clicking notification brings browser window to focus
    ///
    /// Parameters:
    /// - title: Short summary (e.g., "Pressure Alert", "Equipment Fault")
    /// - body: Detailed message (e.g., "Patient bed A3 exceeds threshold")
    /// - icon: Visual indicator (e.g., warning icon, patient photo)
    /// - tag: Prevents duplicate notifications with same tag (e.g., "patient-123-pressure-alert")
    /// - requireInteraction: For critical alerts that must be acknowledged
    ///
    /// Prerequisites:
    /// - Browser must support notifications (check with IsNotificationSupportedAsync)
    /// - User must have granted permission (check with GetPermissionAsync)
    ///
    /// Example usage:
    /// await notificationService.ShowNotificationAsync(
    ///     title: "Pressure Alert",
    ///     body: "Patient bed A3: Pressure exceeds threshold (85 mmHg)",
    ///     tag: "patient-a3-pressure-high",
    ///     requireInteraction: true
    /// );
    /// </remarks>
    public async Task<bool> ShowNotificationAsync(
        string title,
        string? body = null,
        string? icon = null,
        string? tag = null,
        bool requireInteraction = false)
    {
        try
        {
            await EnsureModuleAsync();

            var options = new
            {
                body = body ?? "",
                icon = icon ?? "",
                tag = tag ?? "",
                requireInteraction
            };

            return await _module!.InvokeAsync<bool>("showNotification", title, options);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Disposes the JavaScript module reference.
    /// Author: SID:2412494
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            await _module.DisposeAsync();
        }
    }
}
