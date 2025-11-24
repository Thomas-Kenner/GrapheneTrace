using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GrapheneTrace.Web.Models;

/// <summary>
/// Represents an assignment of a patient to a clinician.
/// </summary>
public class PatientClinicianAssignment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid PatientId { get; set; }

    [ForeignKey(nameof(PatientId))]
    public virtual ApplicationUser? Patient { get; set; }

    [Required]
    public Guid ClinicianId { get; set; }

    [ForeignKey(nameof(ClinicianId))]
    public virtual ApplicationUser? Clinician { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
