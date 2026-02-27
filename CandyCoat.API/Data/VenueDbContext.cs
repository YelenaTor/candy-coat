using Microsoft.EntityFrameworkCore;
using CandyCoat.API.Models;

namespace CandyCoat.API.Data;

public class VenueDbContext : DbContext
{
    public VenueDbContext(DbContextOptions<VenueDbContext> options) : base(options) { }

    public DbSet<RoomEntity> Rooms => Set<RoomEntity>();
    public DbSet<StaffEntity> Staff => Set<StaffEntity>();
    public DbSet<PatronEntity> Patrons => Set<PatronEntity>();
    public DbSet<PatronNoteEntity> PatronNotes => Set<PatronNoteEntity>();
    public DbSet<EarningsEntity> Earnings => Set<EarningsEntity>();
    public DbSet<ServiceMenuEntity> ServiceMenu => Set<ServiceMenuEntity>();
    public DbSet<GambaPresetEntity> GambaPresets => Set<GambaPresetEntity>();
    public DbSet<CosmeticSyncEntity> CosmeticsSync => Set<CosmeticSyncEntity>();
    public DbSet<BookingEntity> Bookings => Set<BookingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Unique constraints
        modelBuilder.Entity<StaffEntity>()
            .HasIndex(s => new { s.VenueId, s.CharacterName })
            .IsUnique();

        modelBuilder.Entity<PatronEntity>()
            .HasIndex(p => new { p.VenueId, p.Name })
            .IsUnique();

        // Future: venues table for multi-venue support
        // modelBuilder.Entity<VenueEntity>(e => { ... });
        // All entities would FK to VenueEntity.Id

        // Indexes for common queries
        modelBuilder.Entity<RoomEntity>().HasIndex(r => r.VenueId);
        modelBuilder.Entity<StaffEntity>().HasIndex(s => s.VenueId);
        modelBuilder.Entity<PatronEntity>().HasIndex(p => p.VenueId);
        modelBuilder.Entity<PatronNoteEntity>().HasIndex(n => n.VenueId);
        modelBuilder.Entity<PatronNoteEntity>().HasIndex(n => n.CreatedAt);
        modelBuilder.Entity<EarningsEntity>().HasIndex(e => e.VenueId);
        modelBuilder.Entity<EarningsEntity>().HasIndex(e => e.CreatedAt);
        modelBuilder.Entity<ServiceMenuEntity>().HasIndex(m => m.VenueId);
        modelBuilder.Entity<GambaPresetEntity>().HasIndex(g => g.VenueId);
        modelBuilder.Entity<CosmeticSyncEntity>().HasIndex(c => c.VenueId);
        modelBuilder.Entity<BookingEntity>().HasIndex(b => b.VenueId);
        modelBuilder.Entity<BookingEntity>().HasIndex(b => b.UpdatedAt);
    }
}
