using GrapheneTrace.Web.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace GrapheneTrace.Web.Services;

/// <summary>
/// Provides authentication state for Blazor Server with Identity.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Design Pattern: Extends RevalidatingServerAuthenticationStateProvider to add
/// periodic security stamp validation.
///
/// Why This Is Needed:
/// - Blazor Server maintains long-lived SignalR connections
/// - Users could remain "logged in" even after password change or account deactivation
/// - This provider periodically revalidates authentication state
///
/// Security Features:
/// 1. Revalidation Interval: Checks authentication state every 30 minutes
/// 2. Security Stamp Validation: Invalidates sessions when user credentials change
/// 3. Automatic Logout: Disconnects users whose accounts are deactivated
///
/// How It Works:
/// - ValidateAuthenticationStateAsync() is called every RevalidationInterval
/// - Checks if user still exists and security stamp matches
/// - Returns false to trigger logout if user is invalid
/// - Security stamp changes when: password changes, lockout, roles change, etc.
///
/// Integration with Identity:
/// - Uses UserManager to fetch current user state
/// - Compares ClaimsPrincipal's security stamp with database
/// - Works with Identity's built-in security features
/// </remarks>
public class RevalidatingIdentityAuthenticationStateProvider<TUser>
    : RevalidatingServerAuthenticationStateProvider where TUser : class
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IdentityOptions _options;

    public RevalidatingIdentityAuthenticationStateProvider(
        ILoggerFactory loggerFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<IdentityOptions> optionsAccessor)
        : base(loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _options = optionsAccessor.Value;
    }

    /// <summary>
    /// How often to revalidate authentication state.
    /// </summary>
    /// <remarks>
    /// Design Decision: 30 minutes
    /// Why: Balance between security (detect compromised accounts quickly) and
    /// performance (don't query database too frequently).
    ///
    /// Considerations:
    /// - Session timeout is 20 minutes (configured in Program.cs)
    /// - Revalidation happens even within active sessions
    /// - Catches edge cases like manual database changes or admin deactivation
    /// </remarks>
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

    /// <summary>
    /// Validates that the current authentication state is still valid.
    /// </summary>
    /// <param name="authenticationState">Current authentication state from cookie/claim</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if still valid, false to trigger logout</returns>
    /// <remarks>
    /// Called automatically every RevalidationInterval by base class.
    ///
    /// Validation Steps:
    /// 1. Create a service scope (UserManager is scoped, this provider is singleton)
    /// 2. Get UserManager from DI container
    /// 3. Validate security stamp matches database
    /// 4. Return false if user deleted, deactivated, or security stamp changed
    ///
    /// Important: This runs in a background task, so we need to create a scope
    /// for accessing scoped services like UserManager.
    /// </remarks>
    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        // Create async scope for accessing scoped services
        await using var scope = _scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        // Validate security stamp
        return await ValidateSecurityStampAsync(userManager, authenticationState.User);
    }

    /// <summary>
    /// Validates the security stamp for the current user.
    /// </summary>
    /// <param name="userManager">UserManager instance for database access</param>
    /// <param name="principal">ClaimsPrincipal from authentication cookie</param>
    /// <returns>True if valid, false to force logout</returns>
    /// <remarks>
    /// Security Stamp Explained:
    /// - A random GUID stored in the database for each user
    /// - Changes whenever security-relevant properties change:
    ///   * Password changed
    ///   * Account locked out
    ///   * Roles/claims modified
    ///   * Two-factor settings changed
    /// - Cookie contains the stamp from when user logged in
    /// - If database stamp differs, user must re-authenticate
    ///
    /// This prevents:
    /// - Using old sessions after password reset
    /// - Accessing system after being locked out
    /// - Privilege escalation from stale sessions
    ///
    /// Production Enhancement: Add check for DeactivatedAt field
    /// </remarks>
    private async Task<bool> ValidateSecurityStampAsync(UserManager<TUser> userManager, ClaimsPrincipal principal)
    {
        // Get user from database
        var user = await userManager.GetUserAsync(principal);
        if (user == null)
        {
            // User deleted from database - force logout
            return false;
        }

        // Check if UserManager supports security stamps (it should)
        if (!userManager.SupportsUserSecurityStamp)
        {
            // No security stamp support - trust the cookie
            return true;
        }

        // Get security stamp from cookie (claim)
        var principalStamp = principal.FindFirstValue(_options.ClaimsIdentity.SecurityStampClaimType);

        // Get current security stamp from database
        var userStamp = await userManager.GetSecurityStampAsync(user);

        // Compare stamps - return false if they don't match
        // This will trigger logout and redirect to login page
        return principalStamp == userStamp;
    }

    // Future enhancement: Override ValidateAuthenticationStateAsync to also check DeactivatedAt
    // Example:
    // if (user is ApplicationUser appUser && appUser.DeactivatedAt != null)
    // {
    //     return false;
    // }
}
