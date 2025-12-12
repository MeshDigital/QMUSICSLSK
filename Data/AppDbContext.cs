using Microsoft.EntityFrameworkCore;
using System.IO;

namespace SLSKDONET.Data;

public class AppDbContext : DbContext
{
    public DbSet<TrackEntity> Tracks { get; set; }
    public DbSet<PlaylistJobEntity> PlaylistJobs { get; set; }
    public DbSet<PlaylistTrackEntity> PlaylistTracks { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
        var dbPath = Path.Combine(appData, "SLSKDONET", "library.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure PlaylistJob -> PlaylistTrack relationship
        modelBuilder.Entity<PlaylistJobEntity>()
            .HasMany(j => j.Tracks)
            .WithOne(t => t.Job)
            .HasForeignKey(t => t.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
