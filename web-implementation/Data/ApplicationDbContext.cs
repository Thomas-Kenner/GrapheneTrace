using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using GrapheneTrace.Web.Models;
using System.Security.Cryptography.X509Certificates;

namespace GrapheneTrace.Web.Data;

/// <summary>
/// Database context for Identity and application data.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Design Pattern: Extends IdentityDbContext to leverage ASP.NET Core Identity's
/// built-in authentication and authorization infrastructure.
///
/// Why IdentityDbContext:
/// - Automatically configures Identity tables (AspNetUsers, AspNetRoles, etc.)
/// - Handles relationships between Identity entities
/// - Provides optimized schema for authentication workflows
/// - Battle-tested by millions of applications
///
/// Generic Parameters:
/// - ApplicationUser: Our custom user entity with Guid key
/// - IdentityRole&lt;Guid&gt;: Role entity with Guid key (for future use)
/// - Guid: Primary key type for all Identity tables
///
/// Tables Created Automatically by Identity:
/// - AspNetUsers: User accounts (with our custom fields)
/// - AspNetRoles: Role definitions (optional for our use case)
/// - AspNetUserRoles: User-role mapping
/// - AspNetUserClaims: Custom claims per user
/// - AspNetUserLogins: External login providers (Google, etc.)
/// - AspNetUserTokens: Password reset tokens, 2FA tokens
/// - AspNetRoleClaims: Claims per role
///
/// Future Expansion:
/// Add DbSets here for application data (PressureReadings, Devices, etc.)
/// </remarks>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Requests for clinician assignment (patient-initiated workflow).
    /// </summary>
    public DbSet<PatientClinicianRequest> PatientClinicianRequests { get; set; }

    /// <summary>
    /// Established relationships between patients and clinicians.
    /// </summary>
    public DbSet<PatientClinician> PatientClinicians { get; set; }

    /// <summary>
    /// Patient-specific settings for pressure monitoring alerts.
    /// </summary>
    public DbSet<PatientSettings> PatientSettings { get; set; }

    /// <summary>
    /// Comments made by patients on their pressure sessions, with optional clinician replies.
    /// Author: 2415776 (original), SID:2412494 (integration)
    /// </summary>
    public DbSet<PressureComment> PressureComments { get; set; }

    /// <summary>
    /// Configures the database schema using Fluent API.
    /// </summary>
    /// <remarks>
    /// Called by EF Core when building the model.
    /// base.OnModelCreating() must be called to apply Identity's default configuration.
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Apply Identity's default configuration (creates all Identity tables)
        base.OnModelCreating(builder);

        // Configure ApplicationUser custom properties
        builder.Entity<ApplicationUser>(entity =>
        {
            // Enforce required constraints and max lengths for data integrity
            entity.Property(e => e.FirstName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.LastName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.UserType)
                .IsRequired()
                .HasMaxLength(20);

            // ApprovedAt and DeactivatedAt are nullable, no configuration needed

            // Configure ApprovedBy self-referencing foreign key
            // This allows tracking which admin approved each user account
            entity.HasOne(e => e.ApprovedByAdmin)
                .WithMany()
                .HasForeignKey(e => e.ApprovedBy)
                .OnDelete(DeleteBehavior.Restrict);  // Prevent cascading deletes

            // Optional: Create index on UserType for faster role-based queries
            entity.HasIndex(e => e.UserType);

            // Optional: Create index on ApprovedAt for filtering approved/pending users
            entity.HasIndex(e => e.ApprovedAt);

            // Optional: Create index on DeactivatedAt for filtering active users
            entity.HasIndex(e => e.DeactivatedAt);
        });

        // Configure PatientClinicianRequest entity
        builder.Entity<PatientClinicianRequest>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.ResponseReason)
                .HasMaxLength(500);

            // Foreign key: PatientId
            entity.HasOne(e => e.Patient)
                .WithMany()
                .HasForeignKey(e => e.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            // Foreign key: ClinicianId
            entity.HasOne(e => e.Clinician)
                .WithMany()
                .HasForeignKey(e => e.ClinicianId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for query performance
            entity.HasIndex(e => e.PatientId);
            entity.HasIndex(e => e.ClinicianId);
            entity.HasIndex(e => e.Status)
                .HasFilter("\"Status\" = 'pending'");

            // Unique constraint on pending requests (only one pending per patient-clinician pair)
            entity.HasIndex(e => new { e.PatientId, e.ClinicianId })
                .HasFilter("\"Status\" = 'pending'")
                .IsUnique();
        });

        // Configure PatientClinician entity
        builder.Entity<PatientClinician>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Foreign key: PatientId
            entity.HasOne(e => e.Patient)
                .WithMany()
                .HasForeignKey(e => e.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            // Foreign key: ClinicianId
            entity.HasOne(e => e.Clinician)
                .WithMany()
                .HasForeignKey(e => e.ClinicianId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for query performance
            entity.HasIndex(e => e.PatientId);
            entity.HasIndex(e => e.ClinicianId);
            entity.HasIndex(e => new { e.PatientId, e.ClinicianId })
                .HasFilter("\"UnassignedAt\" IS NULL")
                .IsUnique();

            // Unique constraint on active assignments (only one active per patient-clinician pair)
            // This prevents duplicate active assignments
        });

        // Future: Add configurations for other entities here
        // Example:
        // builder.Entity<PressureReading>(entity =>
        // {
        //     entity.HasOne(p => p.User)
        //         .WithMany()
        //         .HasForeignKey(p => p.UserId);
        // });

        // Author: 2414111
        // Configure PatientSessionData properties
        builder.Entity<PatientSessionData>(entity =>
        {
            entity.HasKey(a => a.SessionId);
            entity.Property(a => a.SessionId)
                .IsRequired()
                .ValueGeneratedOnAdd();

            entity.Property(a => a.DeviceId)
                .IsRequired()
                .HasMaxLength(8);

            entity.HasIndex(a => a.SessionId);
            entity.HasIndex(a => a.DeviceId);
        });

        // Author: 2414111
        // Configure PatientSnapshotData properties
        builder.Entity<PatientSnapshotData>(entity =>
        {
            entity.HasKey(b => b.SnapshotId);
            entity.Property(b => b.SnapshotId)
                .IsRequired()
                .ValueGeneratedOnAdd();

            entity.Property(b => b.SessionId)
                .IsRequired();

            entity.Property(b => b.SnapshotData)
                .IsRequired();

            entity.HasIndex(b => b.SnapshotId);
            entity.HasIndex(b => b.SessionId);

            entity.HasOne<PatientSessionData>()
                .WithMany()
                .HasForeignKey(b => b.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    // Author: 2414111
    // Connect the PatientSessionData and PatientSnapshotData models to the database
    public DbSet<PatientSessionData> PatientSessionDatas { get; set; }
    public DbSet<PatientSnapshotData> PatientSnapshotDatas { get; set; }
}
