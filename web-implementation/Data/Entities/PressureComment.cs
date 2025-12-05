using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace GrapheneTrace.Web.Data.Entities
{
    public class PressureComment
    {
        [Key]
        public int Id { get; set; }

        // User who wrote it
        [Required]
        public Guid UserId { get; set; }

        // Patient it belongs to
        [Required]
        public Guid PatientId { get; set; }

        // Comment text
        [Required]
        public string Comment { get; set; } = null!;

        // Clinician reply
        public string? Reply { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RepliedAt { get; set; }

        // temp property for Ui "not stored in DB" to prevent accidental overwritting

        [NotMapped]
        public string TempReply { get; set; } = "";

    }
}
