using Microsoft.AspNetCore.Identity;

namespace GrapheneTrace.Web.Models;

/// <summary>
/// Custom user entity extending ASP.NET Core Identity.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Design Pattern: Extends IdentityUser with Guid primary key for better performance.
///
/// Why Guid over string:
/// - Better performance for indexing and joins
/// - Smaller storage footprint
/// - Aligns with medical device best practices
///
/// Custom Fields:
/// - FirstName, LastName: User display information
/// - UserType: Simple role-based routing ("admin", "clinician", "patient")
/// - DeactivatedAt: Soft deletion for audit trail compliance (HIPAA requirement)
///
/// Identity provides built-in fields:
/// - Email, EmailConfirmed: Email and verification status
/// - PasswordHash: Secure password storage (PBKDF2)
/// - SecurityStamp: Token invalidation on password change
/// - LockoutEnd, AccessFailedCount: Account lockout management
/// - PhoneNumber, TwoFactorEnabled: 2FA support (future)
/// </remarks>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// User's first name.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// User's last name.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// User type for role-based routing.
    /// Valid values: "admin", "clinician", "patient"
    /// </summary>
    /// <remarks>
    /// Design Decision: Simple string property instead of AspNetRoles table.
    /// Why: We have exactly 3 fixed roles that never change. This is simpler
    /// than maintaining a join table and provides better performance.
    /// Future: Can migrate to Claims or Roles if dynamic permissions needed.
    /// </remarks>
    public string UserType { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the user account was created.
    /// </summary>
    /// <remarks>
    /// Author: 2402513
    /// Purpose: Track user registration date for analytics and reporting.
    /// Set automatically during user creation.
    /// Used for dashboard graphs showing user signup trends over time.
    /// </remarks>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the account was approved by an administrator. Null indicates pending approval.
    /// </summary>
    /// <remarks>
    /// Account Approval Workflow:
    /// - When a user registers, this field is NULL (account pending approval)
    /// - Admin must approve the account before user can access the system
    /// - Set to current timestamp when admin approves the account
    /// - Check this field during login to prevent unapproved users from accessing the system
    /// </remarks>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Soft deletion timestamp. Null indicates active account.
    /// </summary>
    /// <remarks>
    /// Compliance Requirement: HIPAA requires maintaining audit trails.
    /// We never permanently delete user accounts to preserve historical data.
    /// Check this field during login to prevent deactivated users from accessing the system.
    /// </remarks>
    public DateTime? DeactivatedAt { get; set; }

    /// <summary>
    /// Display name combining first and last name.
    /// </summary>
    public string FullName => $"{FirstName} {LastName}";
}
