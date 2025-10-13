namespace GrapheneTrace.Web.Services;

/// <summary>
/// Manages authentication state for the Blazor web application.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Design Pattern: Observable Pattern (Event-based State Management)
/// Why: Allows multiple components to react to authentication state changes without tight coupling.
///
/// Lifecycle: Registered as Singleton in DI container
/// Why Singleton: Authentication state must persist across all Blazor circuits and page navigations.
/// Using Scoped would create separate instances per circuit, causing users to lose authentication
/// when navigating between pages.
///
/// Security Note: This is a temporary implementation for UI development.
/// Production implementation should use ASP.NET Core Identity with proper token management,
/// secure session storage, and database-backed user validation.
/// </remarks>
public class AuthenticationService
{
    private bool _isAuthenticated;
    private string? _userName;

    /// <summary>
    /// Event fired when authentication state changes (login/logout).
    /// </summary>
    /// <remarks>
    /// Purpose: Allows MainLayout and other components to re-render when auth state changes.
    /// Pattern: Observer pattern for decoupled state notification.
    /// </remarks>
    public event Action? OnAuthenticationStateChanged;

    /// <summary>
    /// Gets whether a user is currently authenticated.
    /// </summary>
    public bool IsAuthenticated => _isAuthenticated;

    /// <summary>
    /// Gets the authenticated user's name (email in current implementation).
    /// </summary>
    public string? UserName => _userName;

    /// <summary>
    /// Authenticates a user with the provided username.
    /// </summary>
    /// <param name="userName">User's email or username for display purposes</param>
    /// <remarks>
    /// Current Implementation: Accepts any non-empty username without validation.
    /// Why: This is a placeholder for UI development. No actual credential checking occurs.
    ///
    /// Production TODO: Replace with proper authentication:
    /// - Validate credentials against database
    /// - Hash password comparison using BCrypt
    /// - Generate secure session token
    /// - Set HttpOnly authentication cookies
    /// </remarks>
    public void Login(string userName)
    {
        _isAuthenticated = true;
        _userName = userName;
        NotifyAuthenticationStateChanged();
    }

    /// <summary>
    /// Logs out the current user and clears authentication state.
    /// </summary>
    /// <remarks>
    /// Current Implementation: Simple state reset.
    /// Production TODO: Clear server-side session, invalidate tokens, clear cookies.
    /// </remarks>
    public void Logout()
    {
        _isAuthenticated = false;
        _userName = null;
        NotifyAuthenticationStateChanged();
    }

    /// <summary>
    /// Notifies all subscribers that authentication state has changed.
    /// </summary>
    /// <remarks>
    /// Design Decision: Null-conditional operator (?.) for safe invocation.
    /// Why: Prevents NullReferenceException if no components are subscribed to the event.
    /// </remarks>
    private void NotifyAuthenticationStateChanged()
    {
        OnAuthenticationStateChanged?.Invoke();
    }
}
