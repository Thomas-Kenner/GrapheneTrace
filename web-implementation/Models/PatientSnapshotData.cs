// Author: 2414111
// Model for PatientSnapshotData (a freeze frame of the heatmap) that will be saved in the database
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
namespace GrapheneTrace.Web.Models;

public class PatientSnapshotData
{
    [Key]
    public int SnapshotId { get; set; }
    public int SessionId { get; set; }
    public DateTime? SnapshotTime { get; set; }
    // Storing all the sensor values in one comma separated string
    public string SnapshotData { get; set; } = string.Empty;
    public int? PeakSnapshotPressure { get; set; }
    public float? ContactAreaPercent { get; set; }
    public float? CoefficientOfVariation { get; set; }
}