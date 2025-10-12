using System;
using System.ComponentModel.DataAnnotations;

namespace GrapheneTrace.Core.Models;

public class PressureDataEntity
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string SensorId { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    [Required]
    public byte[] PressureMatrix { get; set; } = Array.Empty<byte>();

    public short PeakPressure { get; set; }

    public decimal ContactAreaPercentage { get; set; }

    [Required]
    [MaxLength(20)]
    public string AlertStatus { get; set; } = "NORMAL";

    public DateTime CreatedAt { get; set; }
}

public class MonitoringSessionEntity
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string SessionName { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    [Required]
    [MaxLength(50)]
    public string SensorId { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? PatientId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class PressureAlertEntity
{
    public Guid Id { get; set; }

    public Guid? SessionId { get; set; }

    public Guid? PressureDataId { get; set; }

    [Required]
    [MaxLength(50)]
    public string AlertType { get; set; } = "HIGH_PRESSURE";

    public short ThresholdValue { get; set; }

    public short ActualValue { get; set; }

    public DateTime Timestamp { get; set; }

    public bool Acknowledged { get; set; } = false;

    [MaxLength(100)]
    public string? AcknowledgedBy { get; set; }

    public DateTime? AcknowledgedAt { get; set; }
}