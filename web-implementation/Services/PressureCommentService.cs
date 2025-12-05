using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GrapheneTrace.Web.Services;

public class PressureCommentService
{
    private readonly ApplicationDbContext _db;

    public PressureCommentService(ApplicationDbContext db)
    {
        _db = db;
    }

    // Add new patient comment
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
    }

    //Get comments for one patient
    public async Task<List<PressureComment>> GetCommentsForPatientAsync(Guid patientId)
    {
        return await _db.PressureComments
            .Where(c => c.PatientId == patientId)
            .OrderByDescending(c => c.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    //  Add clinician reply 
    public async Task<bool> AddReplyAsync(int commentId, string reply)
    {
        Console.WriteLine("▶ AddReplyAsync called for ID = " + commentId);
        Console.WriteLine("▶ Reply text = " + reply);

        var comment = await _db.PressureComments
                               .AsTracking()
                               .FirstOrDefaultAsync(c => c.Id == commentId);

        if (comment == null)
        {
            Console.WriteLine("❌ COMMENT NOT FOUND");
            return false;
        }

        Console.WriteLine("✅ FOUND COMMENT");

        comment.Reply = reply;
        comment.RepliedAt = DateTime.UtcNow;

        _db.PressureComments.Update(comment);

        var rows = await _db.SaveChangesAsync();

        Console.WriteLine("✅ ROWS UPDATED = " + rows);

        return rows > 0;
    }




    // Get all patient comments for clinician
    public async Task<List<PressureComment>> GetAllCommentsAsync()
    {
        return await _db.PressureComments
            .OrderByDescending(c => c.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }
}
