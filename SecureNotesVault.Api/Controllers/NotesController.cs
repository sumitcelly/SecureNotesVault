using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureNotesVault.Core;
using SecureNotesVault.Core.Models;
using SecureNotesVault.Core.Services;

namespace SecureNotesVault.Api.Controllers;

[Authorize] // Blocks all unauthenticated traffic out of the box
[ApiController]
[Route("api/notes")]
public class NotesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;

    public NotesController(ApplicationDbContext context, IEncryptionService encryptionService)
    {
        _context = context;
        _encryptionService = encryptionService;
    }

    // POST: api/notes (Create an Encrypted Note)
    [HttpPost]
    public async Task<IActionResult> CreateNote([FromBody] NoteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "Note content cannot be empty." });
        }

        int currentUserId = GetCurrentUserId();

        // Application-layer encryption before persistence
        string encryptedContent = _encryptionService.Encrypt(request.Content);

        var note = new Note
        {
            OwnerId = currentUserId,
            Content = encryptedContent,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Notes.Add(note);
        await _context.SaveChangesAsync();

        //returns 201 Created with the location of the new note and its ID
        return CreatedAtAction(nameof(GetNoteById), new { id = note.Id }, new { message = "Note secured successfully.", noteId = note.Id });
    }

     // GET: api/notes (List all notes owned by or shared with the authenticated user)
    [HttpGet]
    public async Task<IActionResult> GetAllNotes()
    {
        int currentUserId = GetCurrentUserId();

        var notes = await _context.Notes
            .Where(n => n.OwnerId == currentUserId || n.Shares.Any(s => s.SharedWithUserId == currentUserId))
            .Include(n => n.Shares)
            .ToListAsync();

        var result = notes.Select(note => new
        {
            note.Id,
            note.OwnerId,
            Content = _encryptionService.Decrypt(note.Content),
            note.CreatedAt,
            note.UpdatedAt,
            IsReadOnly = note.OwnerId != currentUserId // Shared notes are read-only for recipients
        });

        return Ok(result);
    }

    
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNote(int id)
    {
        int currentUserId = GetCurrentUserId();

        var note = await _context.Notes.FindAsync(id);
        if (note == null)
        {
            return NotFound(new { error = "Note not found." });
        }

        // Access Control Rule: Only the owner can delete a note
        if (note.OwnerId != currentUserId)
        {
            return Forbid();
        }

        _context.Notes.Remove(note);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Note deleted successfully." });
    }


    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateNote(int id, [FromBody] NoteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "Note content cannot be empty." });
        }

        int currentUserId = GetCurrentUserId();

        var note = await _context.Notes.FindAsync(id);
        if (note == null)
        {
            return NotFound(new { error = "Note not found." });
        }

        // Access Control Rule: Only the owner can update a note
        if (note.OwnerId != currentUserId)
        {
            return Forbid();
        }

        // Application-layer encryption before persistence
        note.Content = _encryptionService.Encrypt(request.Content);
        note.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Note updated successfully." });
    }
    // GET: api/notes/{id} (Fetch & Decrypt a Note)
    [HttpGet("{id}")]
    public async Task<IActionResult> GetNoteById(int id)
    {
        int currentUserId = GetCurrentUserId();

        var note = await _context.Notes
            .Include(n => n.Shares)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (note == null)
        {
            //returns a 404 Not Found status code if the note does not exist
            return NotFound(new { error = "Note not found." });
        }

        // Enforcement: Must be the owner OR explicitly listed in the share table
        bool isOwner = note.OwnerId == currentUserId;
        bool isSharedWithMe = note.Shares.Any(s => s.SharedWithUserId == currentUserId);

        if (!isOwner && !isSharedWithMe)
        {
            return Forbid(); // Return 403 Forbidden for unauthorized access attempts
        }

        // Transparent cryptographic decryption on read
        string decryptedContent = _encryptionService.Decrypt(note.Content);

        return Ok(new
        {
            note.Id,
            note.OwnerId,
            Content = decryptedContent,
            note.CreatedAt,
            note.UpdatedAt,
            IsReadOnly = !isOwner // Shared notes are explicitly read-only to recipients
        });
    }

    // POST: api/notes/{id}/share (Share access with another user)
    [HttpPost("{id}/share")]
    public async Task<IActionResult> ShareNote(int id, [FromBody] ShareRequest request)
    {
        int currentUserId = GetCurrentUserId();

        var note = await _context.Notes.FindAsync(id);
        if (note == null)
        {
            return NotFound(new { error = "Note not found." });
        }

        // Access Control Rule: Only the owner can share a note
        if (note.OwnerId != currentUserId)
        {
            return Forbid();
        }

        if (note.OwnerId == request.SharedWithUserId)
        {
            return BadRequest(new { error = "Cannot share a note with yourself." });
        }

        // Verify the recipient user exists
        var recipientExists = await _context.Users.AnyAsync(u => u.Id == request.SharedWithUserId);
        if (!recipientExists)
        {
            return BadRequest(new { error = "Target user for sharing does not exist." });
        }

        // Check if already shared to prevent duplication crashes
        var alreadyShared = await _context.Shares.AnyAsync(s => s.NoteId == id && s.SharedWithUserId == request.SharedWithUserId);
        if (alreadyShared)
        {
            return BadRequest(new { error = "This note is already shared with the specified user." });
        }

        var share = new Share
        {
            NoteId = id,
            SharedWithUserId = request.SharedWithUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Shares.Add(share);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Note access permissions updated successfully." });
    }

    // Helper method to securely extract the User ID bound inside the signed JWT token
    private int GetCurrentUserId()
    {
        var nameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(nameIdentifier) || !int.TryParse(nameIdentifier, out int userId))
        {
            throw new UnauthorizedAccessException("Valid user context missing from incoming authentication token.");
        }
        return userId;
    }
}

// Minimal request payloads for route schema bindings
public class NoteRequest { public string Content { get; set; } = string.Empty; }
public class ShareRequest { public int SharedWithUserId { get; set; } }
