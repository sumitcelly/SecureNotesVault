using Microsoft.EntityFrameworkCore;
using SecureNotesVault.Core.Models;

namespace SecureNotesVault.Core;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Note> Notes { get; set; } = null!;
    public DbSet<Share> Shares { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Ensure usernames are globally unique at the database level
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        // 2. Prevent a note from being shared with the exact same user multiple times
        modelBuilder.Entity<Share>()
            .HasIndex(s => new { s.NoteId, s.SharedWithUserId })
            .IsUnique();

        // 3. Configure explicit Foreign Key relationships to prevent unintended cascade paths
        modelBuilder.Entity<Note>()
            .HasOne(n => n.Owner)
            .WithMany(u => u.Notes)
            .HasForeignKey(n => n.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Share>()
            .HasOne(s => s.Note)
            .WithMany(n => n.Shares)
            .HasForeignKey(s => s.NoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
