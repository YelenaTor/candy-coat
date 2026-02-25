using System;

namespace CandyCoat.Data;

public class PatronNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PatronName { get; set; } = string.Empty;
    public StaffRole AuthorRole { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
