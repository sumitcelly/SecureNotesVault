using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureNotesVault.Core.Models;

public class Share
{
    public int Id { get; set; }

    [Required]
    public int NoteId { get; set; }

    [ForeignKey(nameof(NoteId))]
    public Note? Note { get; set; }

    [Required]
    public int SharedWithUserId { get; set; }

    [ForeignKey(nameof(SharedWithUserId))]
    public User? SharedWithUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
