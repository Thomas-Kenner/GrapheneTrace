using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GrapheneTrace.Web.Services;

/// <summary>
/// Service for managing pressure map comments between patients and clinicians.
/// Author: 2415776
/// Updated: SID:2412494 - Added clinician filtering, removed debug code, added logging
/// </summary>
/// <remarks>
/// Implements User Stories #13 and #14:
/// - Patients can add comments to explain pressure readings
/// - Clinicians can view and reply to comments from their assigned patients
/// </remarks>
public class PressureCommentService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<PressureCommentService> _logger;

    public PressureCommentService(ApplicationDbContext db, ILogger<PressureCommentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Adds a new comment from a patient.
    /// Author: 2415776
    /// </summary>
    public async Task AddCommentAsync(Guid userId, Guid patientId, string commentText)
    {
        var comment = new PressureComment
        {
            UserId = userId,
            PatientId = patientId,
            Comment = commentText,
            CreatedAt = DateTime.UtcNow
        };

        _db.PressureComments.Add(comment);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Comment added by patient {PatientId}", patientId);
    }

    /// <summary>
    /// Gets all comments for a specific patient.
    /// Author: 2415776
    /// </summary>
    public async Task<List<PressureComment>> GetCommentsForPatientAsync(Guid patientId)
    {
        return await _db.PressureComments
            .Where(c => c.PatientId == patientId)
            .OrderByDescending(c => c.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Adds a clinician reply to a comment.
    /// Author: 2415776
    /// Updated: SID:2412494 - Replaced Console.WriteLine with ILogger
    /// </summary>
    public async Task<bool> AddReplyAsync(int commentId, string reply)
    {
        var comment = await _db.PressureComments
            .FirstOrDefaultAsync(c => c.Id == commentId);

        if (comment == null)
        {
            _logger.LogWarning("Attempted to reply to non-existent comment {CommentId}", commentId);
            return false;
        }

        comment.Reply = reply;
        comment.RepliedAt = DateTime.UtcNow;

        var rows = await _db.SaveChangesAsync();

        if (rows > 0)
        {
            _logger.LogInformation("Reply added to comment {CommentId}", commentId);
        }

        return rows > 0;
    }

    /// <summary>
    /// Gets comments from patients assigned to a specific clinician.
    /// Author: SID:2412494
    /// </summary>
    /// <param name="clinicianId">The clinician's user ID</param>
    /// <returns>Tuple of comments list and dictionary mapping patient IDs to names</returns>
    public async Task<(List<PressureComment> Comments, Dictionary<Guid, string> PatientNames)> GetCommentsForClinicianAsync(Guid clinicianId)
    {
        // Get list of patient IDs assigned to this clinician
        var assignedPatientIds = await _db.PatientClinicians
            .Where(pc => pc.ClinicianId == clinicianId && pc.UnassignedAt == null)
            .Select(pc => pc.PatientId)
            .ToListAsync();

        if (!assignedPatientIds.Any())
        {
            return (new List<PressureComment>(), new Dictionary<Guid, string>());
        }

        // Get comments from assigned patients only
        var comments = await _db.PressureComments
            .Where(c => assignedPatientIds.Contains(c.PatientId))
            .OrderByDescending(c => c.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        // Get patient names for display
        var patientNames = await _db.Users
            .Where(u => assignedPatientIds.Contains(u.Id))
            .ToDictionaryAsync(
                u => u.Id,
                u => $"{u.FirstName} {u.LastName}"
            );

        return (comments, patientNames);
    }

    /// <summary>
    /// Gets all comments (admin use only - not filtered by assignment).
    /// Author: 2415776
    /// </summary>
    [Obsolete("Use GetCommentsForClinicianAsync for clinician views to respect patient-clinician assignments")]
    public async Task<List<PressureComment>> GetAllCommentsAsync()
    {
        return await _db.PressureComments
            .OrderByDescending(c => c.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }
}
