using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GrapheneTrace.Web.Services;

public class ChatService
{
    private readonly ApplicationDbContext _context;

    public ChatService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ApplicationUser>> GetAssignedUsersAsync(Guid userId, string userType)
    {
        if (userType == "clinician")
        {
            return await _context.PatientClinicianAssignments
                .Where(a => a.ClinicianId == userId)
                .Select(a => a.Patient!)
                .ToListAsync();
        }
        else if (userType == "patient")
        {
            return await _context.PatientClinicianAssignments
                .Where(a => a.PatientId == userId)
                .Select(a => a.Clinician!)
                .ToListAsync();
        }
        return new List<ApplicationUser>();
    }

    public async Task<List<ChatMessage>> GetConversationAsync(Guid user1Id, Guid user2Id)
    {
        return await _context.ChatMessages
            .Where(m => (m.SenderId == user1Id && m.ReceiverId == user2Id) ||
                        (m.SenderId == user2Id && m.ReceiverId == user1Id))
            .OrderBy(m => m.SentAt)
            .ToListAsync();
    }

    public async Task<ChatMessage> SaveMessageAsync(Guid senderId, Guid receiverId, string content)
    {
        var message = new ChatMessage
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Content = content,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();
        return message;
    }

    public async Task MarkAsReadAsync(Guid messageId)
    {
        var message = await _context.ChatMessages.FindAsync(messageId);
        if (message != null)
        {
            message.IsRead = true;
            await _context.SaveChangesAsync();
        }
    }
}
