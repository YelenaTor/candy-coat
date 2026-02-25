using System;

namespace CandyCoat.API.Models;

public class PatronNoteEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VenueId { get; set; } // Future: FK to venues table
    public string PatronName { get; set; } = string.Empty;
    public string AuthorRole { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
