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
        await AddCommentAsync(userId, patientId, commentText, null, null);
    }

    /// <summary>
    /// Adds a new comment from a patient, optionally tied to a specific session and frame.
    /// Author: SID:2412494
    /// </summary>
    /// <param name="userId">The user creating the comment</param>
    /// <param name="patientId">The patient the comment is about</param>
    /// <param name="commentText">The comment text</param>
    /// <param name="sessionId">Optional session ID to link the comment to</param>
    /// <param name="frameIndex">Optional frame index within the session</param>
    public async Task AddCommentAsync(Guid userId, Guid patientId, string commentText, int? sessionId, int? frameIndex)
    {
        var comment = new PressureComment
        {
            UserId = userId,
            PatientId = patientId,
            Comment = commentText,
            SessionId = sessionId,
            FrameIndex = frameIndex,
            CreatedAt = DateTime.UtcNow
        };

        _db.PressureComments.Add(comment);
        await _db.SaveChangesAsync();

        if (sessionId.HasValue)
        {
            _logger.LogInformation("Comment added by patient {PatientId} on session {SessionId} frame {FrameIndex}",
                patientId, sessionId, frameIndex);
        }
        else
        {
            _logger.LogInformation("Comment added by patient {PatientId}", patientId);
        }
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
    /// Gets all comments for a specific session.
    /// Author: SID:2412494
    /// </summary>
    /// <param name="sessionId">The session ID to get comments for</param>
    /// <returns>List of comments ordered by frame index, then creation time</returns>
    public async Task<List<PressureComment>> GetCommentsForSessionAsync(int sessionId)
    {
        return await _db.PressureComments
            .Where(c => c.SessionId == sessionId)
            .OrderBy(c => c.FrameIndex ?? int.MaxValue)
            .ThenBy(c => c.CreatedAt)
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
