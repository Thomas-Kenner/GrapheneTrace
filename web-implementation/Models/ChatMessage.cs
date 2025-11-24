using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GrapheneTrace.Web.Models;

/// <summary>
/// Represents a chat message between users.
/// </summary>
public class ChatMessage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SenderId { get; set; }

    [ForeignKey(nameof(SenderId))]
    public virtual ApplicationUser? Sender { get; set; }

    [Required]
    public Guid ReceiverId { get; set; }

    [ForeignKey(nameof(ReceiverId))]
    public virtual ApplicationUser? Receiver { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; } = false;
}
