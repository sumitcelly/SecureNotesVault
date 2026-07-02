using System.ComponentModel.DataAnnotations;

namespace SecureNotesVault.Core.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)] // Large length to safely store modern salted hashes
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties for Entity Framework relationship mapping
    public ICollection<Note> Notes { get; set; } = new List<Note>();
}
