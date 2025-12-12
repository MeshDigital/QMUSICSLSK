using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SLSKDONET.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SLSKDONET.Services;

public class DatabaseService
{
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(ILogger<DatabaseService> logger)
    {
        _logger = logger;
    }

    public async Task InitAsync()
    {
        using var context = new AppDbContext();
        await context.Database.EnsureCreatedAsync();
        _logger.LogInformation("Database initialized.");
    }

    // ===== Track Methods =====

    public async Task<List<TrackEntity>> LoadTracksAsync()
    {
        using var context = new AppDbContext();
        return await context.Tracks.ToListAsync();
    }

    public async Task SaveTrackAsync(TrackEntity track)
    {
        using var context = new AppDbContext();
        var existing = await context.Tracks.FindAsync(track.GlobalId);
        
        if (existing == null)
        {
            await context.Tracks.AddAsync(track);
        }
        else
        {
            // Update fields
            existing.State = track.State;
            existing.ErrorMessage = track.ErrorMessage;
            existing.CoverArtUrl = track.CoverArtUrl; // Persist album art
            // Should we update others? Usually just state changes.
        }
        
        await context.SaveChangesAsync();
    }

    public async Task RemoveTrackAsync(string globalId)
    {
        using var context = new AppDbContext();
        var track = await context.Tracks.FindAsync(globalId);
        if (track != null)
        {
            context.Tracks.Remove(track);
            await context.SaveChangesAsync();
        }
    }

    // Helper to bulk save if needed
    public async Task SaveAllAsync(IEnumerable<TrackEntity> tracks)
    {
        using var context = new AppDbContext();
        foreach(var t in tracks)
        {
            if (!await context.Tracks.AnyAsync(x => x.GlobalId == t.GlobalId))
            {
                await context.Tracks.AddAsync(t);
            }
        }
        await context.SaveChangesAsync();
    }

    // ===== PlaylistJob Methods =====

    public async Task<List<PlaylistJobEntity>> LoadAllPlaylistJobsAsync()
    {
        using var context = new AppDbContext();
        return await context.PlaylistJobs
            .Include(j => j.Tracks)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    public async Task<PlaylistJobEntity?> LoadPlaylistJobAsync(Guid jobId)
    {
        using var context = new AppDbContext();
        return await context.PlaylistJobs
            .Include(j => j.Tracks)
            .FirstOrDefaultAsync(j => j.Id == jobId);
    }

    public async Task SavePlaylistJobAsync(PlaylistJobEntity job)
    {
        using var context = new AppDbContext();
        var existing = await context.PlaylistJobs.FindAsync(job.Id);
        
        if (existing == null)
        {
            job.CreatedAt = DateTime.UtcNow;
            await context.PlaylistJobs.AddAsync(job);
        }
        else
        {
            existing.SourceTitle = job.SourceTitle;
            existing.SourceType = job.SourceType;
            existing.DestinationFolder = job.DestinationFolder;
            existing.TotalTracks = job.TotalTracks;
            existing.SuccessfulCount = job.SuccessfulCount;
            existing.FailedCount = job.FailedCount;
            existing.CompletedAt = job.CompletedAt;
        }
        
        await context.SaveChangesAsync();
        _logger.LogInformation("Saved PlaylistJob: {Title} ({Id})", job.SourceTitle, job.Id);
    }

    public async Task DeletePlaylistJobAsync(Guid jobId)
    {
        using var context = new AppDbContext();
        var job = await context.PlaylistJobs.FindAsync(jobId);
        if (job != null)
        {
            context.PlaylistJobs.Remove(job);
            await context.SaveChangesAsync();
            _logger.LogInformation("Deleted PlaylistJob: {Id}", jobId);
        }
    }

    // ===== PlaylistTrack Methods =====

    public async Task<List<PlaylistTrackEntity>> LoadPlaylistTracksAsync(Guid jobId)
    {
        using var context = new AppDbContext();
        return await context.PlaylistTracks
            .Where(t => t.PlaylistId == jobId)
            .OrderBy(t => t.TrackNumber)
            .ToListAsync();
    }

    public async Task SavePlaylistTrackAsync(PlaylistTrackEntity track)
    {
        using var context = new AppDbContext();
        var existing = await context.PlaylistTracks.FindAsync(track.Id);
        
        if (existing == null)
        {
            track.AddedAt = DateTime.UtcNow;
            await context.PlaylistTracks.AddAsync(track);
        }
        else
        {
            existing.Status = track.Status;
            existing.ResolvedFilePath = track.ResolvedFilePath;
        }
        
        await context.SaveChangesAsync();
    }

    public async Task SavePlaylistTracksAsync(IEnumerable<PlaylistTrackEntity> tracks)
    {
        using var context = new AppDbContext();
        foreach (var track in tracks)
        {
            var existing = await context.PlaylistTracks.FindAsync(track.Id);
            if (existing == null)
            {
                track.AddedAt = DateTime.UtcNow;
                await context.PlaylistTracks.AddAsync(track);
            }
        }
        await context.SaveChangesAsync();
        _logger.LogInformation("Saved {Count} playlist tracks", tracks.Count());
    }

    public async Task DeletePlaylistTracksAsync(Guid jobId)
    {
        using var context = new AppDbContext();
        var tracks = await context.PlaylistTracks
            .Where(t => t.PlaylistId == jobId)
            .ToListAsync();
        
        foreach (var track in tracks)
        {
            context.PlaylistTracks.Remove(track);
        }
        
        await context.SaveChangesAsync();
        _logger.LogInformation("Deleted {Count} playlist tracks for job {JobId}", tracks.Count, jobId);
    }
}
