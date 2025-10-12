using Microsoft.EntityFrameworkCore;
using GrapheneTrace.Core.Models;

namespace GrapheneTrace.Infrastructure.Database.Context;

public class GrapheneTraceDbContext : DbContext
{
    public GrapheneTraceDbContext(DbContextOptions<GrapheneTraceDbContext> options)
        : base(options)
    {
    }

    public DbSet<PressureDataEntity> PressureData { get; set; } = null!;
    public DbSet<MonitoringSessionEntity> MonitoringSessions { get; set; } = null!;
    public DbSet<PressureAlertEntity> PressureAlerts { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PressureDataEntity>(entity =>
        {
            entity.ToTable("pressure_data");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SensorId).HasColumnName("sensor_id").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Timestamp).HasColumnName("timestamp").IsRequired();
            entity.Property(e => e.PressureMatrix).HasColumnName("pressure_matrix").IsRequired();
            entity.Property(e => e.PeakPressure).HasColumnName("peak_pressure").IsRequired();
            entity.Property(e => e.ContactAreaPercentage).HasColumnName("contact_area_percentage").HasPrecision(5, 2).IsRequired();
            entity.Property(e => e.AlertStatus).HasColumnName("alert_status").HasMaxLength(20).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

            entity.HasIndex(e => e.Timestamp).HasDatabaseName("idx_pressure_data_timestamp");
            entity.HasIndex(e => e.SensorId).HasDatabaseName("idx_pressure_data_sensor_id");
        });

        modelBuilder.Entity<MonitoringSessionEntity>(entity =>
        {
            entity.ToTable("monitoring_sessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SessionName).HasColumnName("session_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.StartTime).HasColumnName("start_time").IsRequired();
            entity.Property(e => e.EndTime).HasColumnName("end_time");
            entity.Property(e => e.SensorId).HasColumnName("sensor_id").HasMaxLength(50).IsRequired();
            entity.Property(e => e.PatientId).HasColumnName("patient_id").HasMaxLength(50);
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

            entity.HasIndex(e => e.StartTime).HasDatabaseName("idx_monitoring_sessions_start_time");
        });

        modelBuilder.Entity<PressureAlertEntity>(entity =>
        {
            entity.ToTable("pressure_alerts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.PressureDataId).HasColumnName("pressure_data_id");
            entity.Property(e => e.AlertType).HasColumnName("alert_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.ThresholdValue).HasColumnName("threshold_value").IsRequired();
            entity.Property(e => e.ActualValue).HasColumnName("actual_value").IsRequired();
            entity.Property(e => e.Timestamp).HasColumnName("timestamp").IsRequired();
            entity.Property(e => e.Acknowledged).HasColumnName("acknowledged").IsRequired();
            entity.Property(e => e.AcknowledgedBy).HasColumnName("acknowledged_by").HasMaxLength(100);
            entity.Property(e => e.AcknowledgedAt).HasColumnName("acknowledged_at");

            entity.HasOne<MonitoringSessionEntity>()
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<PressureDataEntity>()
                .WithMany()
                .HasForeignKey(e => e.PressureDataId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.Timestamp).HasDatabaseName("idx_pressure_alerts_timestamp");
            entity.HasIndex(e => e.Acknowledged).HasDatabaseName("idx_pressure_alerts_acknowledged");
        });
    }
}