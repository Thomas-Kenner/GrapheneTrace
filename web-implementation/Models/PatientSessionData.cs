// Author: 2414111
// Model for PatientSessionData (all data from csv file) that will be in the database
using System.ComponentModel.DataAnnotations;

namespace GrapheneTrace.Web.Models;

public class PatientSessionData
{
    [Key]
    public int SessionId { get; set; }

    // Author: SID:2412494
    // Added PatientId to directly associate sessions with patients
    public Guid? PatientId { get; set; }
    public ApplicationUser? Patient { get; set; }

    // String at the start of the file name
    public string DeviceId { get; set; } = string.Empty;
    // Date in the file name
    public DateTime Start { get; set; }
    // Taken from last snapshot time in session
    public DateTime? End { get; set; }
    public int? PeakSessionPressure { get; set; }
    public bool ClinicianFlag { get; set; } = false;
}
