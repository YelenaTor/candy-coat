using Microsoft.EntityFrameworkCore;
using CandyCoat.API.Data;
using CandyCoat.API.Models;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<VenueDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
    db.Database.Migrate();
}

// ─── Middleware: Venue Key Auth ───
var venueKey = builder.Configuration["VENUE_KEY"] ?? "";
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/health"))
    {
        await next();
        return;
    }
    if (!context.Request.Headers.TryGetValue("X-Venue-Key", out var key) || key != venueKey)
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Invalid venue key");
        return;
    }
    await next();
});

// ─── Health ───
app.MapGet("/api/health", async (VenueDbContext db) =>
{
    try
    {
        await db.Database.CanConnectAsync();
        return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
    catch
    {
        return Results.StatusCode(503);
    }
});

// ═══════════════════════════════════════════
//  ROOMS (3s poll)
// ═══════════════════════════════════════════

app.MapGet("/api/rooms", async (VenueDbContext db, HttpContext ctx) =>
{
    var venueId = GetVenueId(ctx);
    return Results.Ok(await db.Rooms.Where(r => r.VenueId == venueId).ToListAsync());
});

app.MapPost("/api/rooms", async (VenueDbContext db, HttpContext ctx, RoomEntity room) =>
{
    room.VenueId = GetVenueId(ctx);
    room.Id = Guid.NewGuid();
    db.Rooms.Add(room);
    await db.SaveChangesAsync();
    return Results.Created($"/api/rooms/{room.Id}", room);
});

app.MapPut("/api/rooms/{id}", async (VenueDbContext db, HttpContext ctx, Guid id, RoomEntity update) =>
{
    var venueId = GetVenueId(ctx);
    var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == id && r.VenueId == venueId);
    if (room == null) return Results.NotFound();

    room.Status = update.Status;
    room.OccupiedBy = update.OccupiedBy;
    room.PatronName = update.PatronName;
    room.OccupiedSince = update.OccupiedSince;
    room.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(room);
});

app.MapDelete("/api/rooms/{id}", async (VenueDbContext db, HttpContext ctx, Guid id) =>
{
    var venueId = GetVenueId(ctx);
    var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == id && r.VenueId == venueId);
    if (room == null) return Results.NotFound();
    db.Rooms.Remove(room);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// ═══════════════════════════════════════════
//  STAFF (3s poll)
// ═══════════════════════════════════════════

app.MapGet("/api/staff/online", async (VenueDbContext db, HttpContext ctx) =>
{
    var venueId = GetVenueId(ctx);
    // Consider staff offline if no heartbeat in 30s
    var cutoff = DateTime.UtcNow.AddSeconds(-30);
    var staff = await db.Staff
        .Where(s => s.VenueId == venueId && s.LastHeartbeat > cutoff)
        .ToListAsync();

    // Mark stale staff as offline
    var stale = await db.Staff
        .Where(s => s.VenueId == venueId && s.LastHeartbeat <= cutoff && s.IsOnline)
        .ToListAsync();
    foreach (var s in stale) s.IsOnline = false;
    if (stale.Count > 0) await db.SaveChangesAsync();

    return Results.Ok(staff);
});

app.MapPost("/api/staff/heartbeat", async (VenueDbContext db, HttpContext ctx, StaffEntity heartbeat) =>
{
    var venueId = GetVenueId(ctx);
    var existing = await db.Staff.FirstOrDefaultAsync(
        s => s.VenueId == venueId && s.CharacterName == heartbeat.CharacterName);

    if (existing != null)
    {
        existing.IsOnline = true;
        existing.Role = heartbeat.Role;
        existing.HomeWorld = heartbeat.HomeWorld;
        existing.IsDnd = heartbeat.IsDnd;
        existing.ShiftStart = heartbeat.ShiftStart;
        existing.LastHeartbeat = DateTime.UtcNow;
    }
    else
    {
        heartbeat.VenueId = venueId;
        heartbeat.Id = Guid.NewGuid();
        heartbeat.IsOnline = true;
        heartbeat.LastHeartbeat = DateTime.UtcNow;
        db.Staff.Add(heartbeat);
    }
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPost("/api/staff/dnd", async (VenueDbContext db, HttpContext ctx, StaffEntity dndUpdate) =>
{
    var venueId = GetVenueId(ctx);
    var staff = await db.Staff.FirstOrDefaultAsync(
        s => s.VenueId == venueId && s.CharacterName == dndUpdate.CharacterName);
    if (staff == null) return Results.NotFound();
    staff.IsDnd = dndUpdate.IsDnd;
    await db.SaveChangesAsync();
    return Results.Ok(staff);
});

// ═══════════════════════════════════════════
//  EARNINGS (30s poll)
// ═══════════════════════════════════════════

app.MapGet("/api/earnings", async (VenueDbContext db, HttpContext ctx, DateTime? since) =>
{
    var venueId = GetVenueId(ctx);
    var query = db.Earnings.Where(e => e.VenueId == venueId);
    if (since.HasValue)
        query = query.Where(e => e.CreatedAt > since.Value);
    return Results.Ok(await query.OrderByDescending(e => e.CreatedAt).ToListAsync());
});

app.MapPost("/api/earnings", async (VenueDbContext db, HttpContext ctx, EarningsEntity entry) =>
{
    entry.VenueId = GetVenueId(ctx);
    entry.Id = Guid.NewGuid();
    entry.CreatedAt = DateTime.UtcNow;
    db.Earnings.Add(entry);
    await db.SaveChangesAsync();
    return Results.Created($"/api/earnings/{entry.Id}", entry);
});

app.MapGet("/api/earnings/summary", async (VenueDbContext db, HttpContext ctx) =>
{
    var venueId = GetVenueId(ctx);
    var summary = await db.Earnings
        .Where(e => e.VenueId == venueId)
        .GroupBy(e => e.Role)
        .Select(g => new { Role = g.Key, Total = g.Sum(e => e.Amount), Count = g.Count() })
        .ToListAsync();
    return Results.Ok(summary);
});

// ═══════════════════════════════════════════
//  PATRONS (30s poll)
// ═══════════════════════════════════════════

app.MapGet("/api/patrons", async (VenueDbContext db, HttpContext ctx) =>
{
    var venueId = GetVenueId(ctx);
    return Results.Ok(await db.Patrons.Where(p => p.VenueId == venueId).ToListAsync());
});

app.MapPost("/api/patrons", async (VenueDbContext db, HttpContext ctx, PatronEntity patron) =>
{
    var venueId = GetVenueId(ctx);
    // Upsert by name
    var existing = await db.Patrons.FirstOrDefaultAsync(
        p => p.VenueId == venueId && p.Name == patron.Name);
    if (existing != null)
    {
        existing.World = patron.World;
        existing.Status = patron.Status;
        existing.VisitCount = patron.VisitCount;
        existing.TotalGilSpent = patron.TotalGilSpent;
        existing.Notes = patron.Notes;
        existing.RpHooks = patron.RpHooks;
        existing.FavoriteDrink = patron.FavoriteDrink;
        existing.Allergies = patron.Allergies;
        existing.BlacklistReason = patron.BlacklistReason;
        existing.BlacklistDate = patron.BlacklistDate;
        existing.BlacklistFlaggedBy = patron.BlacklistFlaggedBy;
        existing.LastSeen = patron.LastSeen;
        await db.SaveChangesAsync();
        return Results.Ok(existing);
    }

    patron.VenueId = venueId;
    patron.Id = Guid.NewGuid();
    db.Patrons.Add(patron);
    await db.SaveChangesAsync();
    return Results.Created($"/api/patrons/{patron.Id}", patron);
});

app.MapPut("/api/patrons/{id}", async (VenueDbContext db, HttpContext ctx, Guid id, PatronEntity update) =>
{
    var venueId = GetVenueId(ctx);
    var patron = await db.Patrons.FirstOrDefaultAsync(p => p.Id == id && p.VenueId == venueId);
    if (patron == null) return Results.NotFound();

    patron.World = update.World;
    patron.Status = update.Status;
    patron.VisitCount = update.VisitCount;
    patron.TotalGilSpent = update.TotalGilSpent;
    patron.Notes = update.Notes;
    patron.RpHooks = update.RpHooks;
    patron.FavoriteDrink = update.FavoriteDrink;
    patron.Allergies = update.Allergies;
    patron.BlacklistReason = update.BlacklistReason;
    patron.BlacklistDate = update.BlacklistDate;
    patron.BlacklistFlaggedBy = update.BlacklistFlaggedBy;
    patron.LastSeen = update.LastSeen;
    await db.SaveChangesAsync();
    return Results.Ok(patron);
});

// ═══════════════════════════════════════════
//  PATRON NOTES (30s poll)
// ═══════════════════════════════════════════

app.MapGet("/api/notes", async (VenueDbContext db, HttpContext ctx, DateTime? since) =>
{
    var venueId = GetVenueId(ctx);
    var query = db.PatronNotes.Where(n => n.VenueId == venueId);
    if (since.HasValue)
        query = query.Where(n => n.CreatedAt > since.Value);
    return Results.Ok(await query.OrderByDescending(n => n.CreatedAt).ToListAsync());
});

app.MapPost("/api/notes", async (VenueDbContext db, HttpContext ctx, PatronNoteEntity note) =>
{
    note.VenueId = GetVenueId(ctx);
    note.Id = Guid.NewGuid();
    note.CreatedAt = DateTime.UtcNow;
    db.PatronNotes.Add(note);
    await db.SaveChangesAsync();
    return Results.Created($"/api/notes/{note.Id}", note);
});

// ═══════════════════════════════════════════
//  SERVICE MENU (on-demand)
// ═══════════════════════════════════════════

app.MapGet("/api/menu", async (VenueDbContext db, HttpContext ctx) =>
{
    var venueId = GetVenueId(ctx);
    return Results.Ok(await db.ServiceMenu.Where(m => m.VenueId == venueId).ToListAsync());
});

app.MapPut("/api/menu", async (VenueDbContext db, HttpContext ctx, List<ServiceMenuEntity> menu) =>
{
    var venueId = GetVenueId(ctx);
    // Replace entire menu
    var existing = await db.ServiceMenu.Where(m => m.VenueId == venueId).ToListAsync();
    db.ServiceMenu.RemoveRange(existing);
    foreach (var item in menu)
    {
        item.VenueId = venueId;
        item.Id = Guid.NewGuid();
    }
    db.ServiceMenu.AddRange(menu);
    await db.SaveChangesAsync();
    return Results.Ok(menu);
});

// ═══════════════════════════════════════════
//  GAMBA PRESETS (on-demand)
// ═══════════════════════════════════════════

app.MapGet("/api/gamba/presets", async (VenueDbContext db, HttpContext ctx) =>
{
    var venueId = GetVenueId(ctx);
    return Results.Ok(await db.GambaPresets.Where(g => g.VenueId == venueId).ToListAsync());
});

app.MapPut("/api/gamba/presets", async (VenueDbContext db, HttpContext ctx, List<GambaPresetEntity> presets) =>
{
    var venueId = GetVenueId(ctx);
    var existing = await db.GambaPresets.Where(g => g.VenueId == venueId).ToListAsync();
    db.GambaPresets.RemoveRange(existing);
    foreach (var p in presets)
    {
        p.VenueId = venueId;
        p.Id = Guid.NewGuid();
    }
    db.GambaPresets.AddRange(presets);
    await db.SaveChangesAsync();
    return Results.Ok(presets);
});

// ═══════════════════════════════════════════
//  COSMETICS SYNCHRONIZATION
// ═══════════════════════════════════════════

app.MapGet("/api/cosmetics", async (VenueDbContext db, HttpContext ctx) =>
{
    var venueId = GetVenueId(ctx);
    // Return all cosmetic envelopes for the venue
    var cosmetics = await db.CosmeticsSync
        .Where(c => c.VenueId == venueId)
        .Select(c => new
        {
            characterHash = c.CharacterHash,
            brotliBlob = c.BrotliBlob,
            lastUpdatedUtc = c.LastUpdatedUtc
        })
        .ToListAsync();
        
    return Results.Ok(cosmetics);
});

app.MapPost("/api/cosmetics", async (VenueDbContext db, HttpContext ctx, CosmeticSyncEntity req) =>
{
    var venueId = GetVenueId(ctx);
    
    var existing = await db.CosmeticsSync.FirstOrDefaultAsync(
        c => c.VenueId == venueId && c.CharacterHash == req.CharacterHash);
        
    if (existing != null)
    {
        existing.BrotliBlob = req.BrotliBlob;
        existing.LastUpdatedUtc = DateTime.UtcNow;
    }
    else
    {
        req.VenueId = venueId;
        req.LastUpdatedUtc = DateTime.UtcNow;
        db.CosmeticsSync.Add(req);
    }
    
    await db.SaveChangesAsync();
    return Results.Ok();
});

// ═══════════════════════════════════════════
//  BOOKINGS (30s slow poll)
// ═══════════════════════════════════════════

app.MapGet("/api/bookings", async (VenueDbContext db, HttpContext ctx) =>
{
    var venueId = GetVenueId(ctx);
    return Results.Ok(await db.Bookings.Where(b => b.VenueId == venueId).ToListAsync());
});

app.MapPost("/api/bookings", async (VenueDbContext db, HttpContext ctx, BookingEntity booking) =>
{
    var venueId = GetVenueId(ctx);
    var existing = await db.Bookings.FirstOrDefaultAsync(b => b.VenueId == venueId && b.Id == booking.Id);
    if (existing != null)
    {
        existing.PatronName = booking.PatronName;
        existing.Service = booking.Service;
        existing.Room = booking.Room;
        existing.Gil = booking.Gil;
        existing.State = booking.State;
        existing.StaffName = booking.StaffName;
        existing.Duration = booking.Duration;
        existing.UpdatedAt = DateTime.UtcNow;
    }
    else
    {
        booking.VenueId = venueId;
        booking.UpdatedAt = DateTime.UtcNow;
        db.Bookings.Add(booking);
    }
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapDelete("/api/bookings/{id}", async (VenueDbContext db, HttpContext ctx, Guid id) =>
{
    var venueId = GetVenueId(ctx);
    var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == id && b.VenueId == venueId);
    if (booking == null) return Results.NotFound();
    db.Bookings.Remove(booking);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// ═══════════════════════════════════════════
//  GLOBAL PROFILES
// ═══════════════════════════════════════════

app.MapPost("/api/profile", async (VenueDbContext db, GlobalProfileEntity req) =>
{
    if (string.IsNullOrWhiteSpace(req.ProfileId))
        return Results.BadRequest("ProfileId is required");

    var existing = await db.GlobalProfiles.FindAsync(req.ProfileId);
    if (existing != null)
    {
        existing.CharacterName = req.CharacterName;
        existing.HomeWorld = req.HomeWorld;
        existing.Mode = req.Mode;
        existing.LastSeen = DateTime.UtcNow;
    }
    else
    {
        req.CreatedAt = DateTime.UtcNow;
        req.LastSeen = DateTime.UtcNow;
        db.GlobalProfiles.Add(req);
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { profileId = req.ProfileId });
});

app.Run();

// ─── Helper: Extract venue ID from venue key config ───
// For now (Sugar-only), venue ID is derived from the venue key.
// Future: look up venue by key in a venues table.
static Guid GetVenueId(HttpContext ctx)
{
    var key = ctx.Request.Headers["X-Venue-Key"].ToString();
    // Deterministic UUID from the key string (simple hash-based approach)
    // This means the same key always maps to the same venue ID.
    return new Guid(System.Security.Cryptography.MD5.HashData(
        System.Text.Encoding.UTF8.GetBytes(key)));
}
