using System;
using System.ComponentModel.DataAnnotations;

namespace CandyCoat.API.Models;

public class CosmeticSyncEntity
{
    [Key]
    public string CharacterHash { get; set; } = string.Empty;
    
    public Guid VenueId { get; set; }
    
    public byte[] BrotliBlob { get; set; } = Array.Empty<byte>();
    
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
