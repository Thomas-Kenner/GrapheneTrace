using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GrapheneTrace.Web.Models;

/// <summary>
/// Represents a patient comment on their pressure map data with optional clinician reply.
/// Author: 2415776
/// Updated: SID:2412494 - Moved to Models folder, added documentation
/// </summary>
/// <remarks>
/// Implements User Stories #13 and #14:
/// - Patients can add comments to explain readings (Story #13)
/// - Clinicians can reply to provide additional information (Story #14)
///
/// Design:
/// - Comments are linked to PatientId (the patient the comment is about)
/// - UserId tracks who created the comment (usually same as PatientId)
/// - TempReply is [NotMapped] for UI binding during reply editing
/// </remarks>
public class PressureComment
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// User who created the comment.
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Patient this comment belongs to.
    /// </summary>
    [Required]
    public Guid PatientId { get; set; }

    /// <summary>
    /// Session this comment is attached to (optional for general comments).
    /// Author: SID:2412494
    /// </summary>
    public int? SessionId { get; set; }

    /// <summary>
    /// Frame index within the session when the comment was made (0-based).
    /// Author: SID:2412494
    /// </summary>
    public int? FrameIndex { get; set; }

    /// <summary>
    /// The comment text.
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string Comment { get; set; } = null!;

    /// <summary>
    /// Clinician's reply to the comment (optional).
    /// </summary>
    [MaxLength(2000)]
    public string? Reply { get; set; }

    /// <summary>
    /// When the comment was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the clinician replied (null if no reply yet).
    /// </summary>
    public DateTime? RepliedAt { get; set; }

    /// <summary>
    /// Temporary storage for reply text during UI editing.
    /// Not persisted to database.
    /// </summary>
    [NotMapped]
    public string TempReply { get; set; } = "";
}
