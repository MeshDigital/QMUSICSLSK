using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Configuration;
using SLSKDONET.Data;
using SLSKDONET.Models;

namespace SLSKDONET.Services;

/// <summary>
/// Concrete implementation of ILibraryService.
/// Manages three persistent indexes:
/// 1. LibraryEntry (main global index of unique files)
/// 2. PlaylistJob (playlist headers) - Database backed
/// 3. PlaylistTrack (relational index linking playlists to tracks) - Database backed
/// </summary>
public class LibraryService : ILibraryService
{
    private readonly ILogger<LibraryService> _logger;
    private readonly DownloadLogService _downloadLogService;
    private readonly DatabaseService _databaseService;
    private readonly string _libraryIndexPath;

    // In-memory cache (for performance)
    private List<LibraryEntry> _libraryCache = new();
    private DateTime _lastLibraryCacheTime = DateTime.MinValue;

    public LibraryService(ILogger<LibraryService> logger, DownloadLogService downloadLogService, DatabaseService databaseService)
    {
        _logger = logger;
        _downloadLogService = downloadLogService;
        _databaseService = databaseService;
        
        var configDir = Path.GetDirectoryName(ConfigManager.GetDefaultConfigPath());
        var dataDir = Path.Combine(configDir ?? AppContext.BaseDirectory, "library_data");
        Directory.CreateDirectory(dataDir);

        _libraryIndexPath = Path.Combine(dataDir, "library_entries.json");

        _logger.LogDebug("LibraryService initialized with database persistence for playlists");
    }

    // ===== INDEX 1: LibraryEntry (Main Global Index - JSON backed) =====

    public LibraryEntry? FindLibraryEntry(string uniqueHash)
    {
        var entries = LoadDownloadedTracks();
        return entries.FirstOrDefault(e => e.UniqueHash == uniqueHash);
    }

    public async Task<LibraryEntry?> FindLibraryEntryAsync(string uniqueHash)
    {
        var entries = await LoadDownloadedTracksAsync();
        return entries.FirstOrDefault(e => e.UniqueHash == uniqueHash);
    }

    public async Task<List<LibraryEntry>> LoadAllLibraryEntriesAsync()
    {
        return await LoadDownloadedTracksAsync();
    }

    public async Task AddLibraryEntryAsync(LibraryEntry entry)
    {
        try
        {
            var entries = LoadDownloadedTracks();
            var existing = entries.FirstOrDefault(e => e.UniqueHash == entry.UniqueHash);
            
            if (existing != null)
                entries.Remove(existing);
            
            entry.AddedAt = DateTime.UtcNow;
            entries.Add(entry);
            
            await Task.Run(() => SaveLibraryIndex(entries));
            _lastLibraryCacheTime = DateTime.MinValue;
            _logger.LogDebug("Added library entry: {Hash}", entry.UniqueHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add library entry");
            throw;
        }
    }

    public async Task UpdateLibraryEntryAsync(LibraryEntry entry)
    {
        try
        {
            var entries = LoadDownloadedTracks();
            var index = entries.FindIndex(e => e.UniqueHash == entry.UniqueHash);
            
            if (index >= 0)
                entries[index] = entry;
            else
                entries.Add(entry);
            
            await Task.Run(() => SaveLibraryIndex(entries));
            _lastLibraryCacheTime = DateTime.MinValue;
            _logger.LogDebug("Updated library entry: {Hash}", entry.UniqueHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update library entry");
            throw;
        }
    }

    // ===== INDEX 2: PlaylistJob (Playlist Headers - Database Backed) =====

    public async Task<List<PlaylistJob>> LoadAllPlaylistJobsAsync()
    {
        try
        {
            var entities = await _databaseService.LoadAllPlaylistJobsAsync();
            return entities.Select(EntityToPlaylistJob).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load playlist jobs from database");
            return new List<PlaylistJob>();
        }
    }

    public PlaylistJob? FindPlaylistJob(Guid playlistId)
    {
        var job = _databaseService.LoadPlaylistJobAsync(playlistId).Result;
        return job != null ? EntityToPlaylistJob(job) : null;
    }

    public async Task<PlaylistJob?> FindPlaylistJobAsync(Guid playlistId)
    {
        try
        {
            var entity = await _databaseService.LoadPlaylistJobAsync(playlistId);
            return entity != null ? EntityToPlaylistJob(entity) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load playlist job {Id}", playlistId);
            return null;
        }
    }

    public async Task SavePlaylistJobAsync(PlaylistJob job)
    {
        try
        {
            var entity = new PlaylistJobEntity
            {
                Id = job.Id,
                SourceTitle = job.SourceTitle,
                SourceType = job.SourceType,
                DestinationFolder = job.DestinationFolder,
                CreatedAt = job.CreatedAt,
                TotalTracks = job.OriginalTracks.Count,
                SuccessfulCount = 0,
                FailedCount = 0
            };

            await _databaseService.SavePlaylistJobAsync(entity);
            _logger.LogInformation("Saved playlist job: {Title} ({Id})", job.SourceTitle, job.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save playlist job");
            throw;
        }
    }

    public async Task DeletePlaylistJobAsync(Guid playlistId)
    {
        try
        {
            await _databaseService.DeletePlaylistJobAsync(playlistId);
            await _databaseService.DeletePlaylistTracksAsync(playlistId);
            _logger.LogInformation("Deleted playlist job: {Id}", playlistId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete playlist job");
            throw;
        }
    }

    // ===== INDEX 3: PlaylistTrack (Relational Index - Database Backed) =====

    public async Task<List<PlaylistTrack>> LoadPlaylistTracksAsync(Guid playlistId)
    {
        try
        {
            var entities = await _databaseService.LoadPlaylistTracksAsync(playlistId);
            return entities.Select(EntityToPlaylistTrack).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load playlist tracks for {PlaylistId}", playlistId);
            return new List<PlaylistTrack>();
        }
    }

    public async Task SavePlaylistTrackAsync(PlaylistTrack track)
    {
        try
        {
            var entity = PlaylistTrackToEntity(track);
            await _databaseService.SavePlaylistTrackAsync(entity);
            _logger.LogDebug("Saved playlist track: {PlaylistId}/{Hash}", track.PlaylistId, track.TrackUniqueHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save playlist track");
            throw;
        }
    }

    public async Task UpdatePlaylistTrackAsync(PlaylistTrack track)
    {
        try
        {
            var entity = PlaylistTrackToEntity(track);
            await _databaseService.SavePlaylistTrackAsync(entity);
            _logger.LogDebug("Updated playlist track status: {Hash} = {Status}", track.TrackUniqueHash, track.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update playlist track");
            throw;
        }
    }

    public async Task SavePlaylistTracksAsync(List<PlaylistTrack> tracks)
    {
        try
        {
            var entities = tracks.Select(PlaylistTrackToEntity).ToList();
            await _databaseService.SavePlaylistTracksAsync(entities);
            _logger.LogInformation("Saved {Count} playlist tracks", tracks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save playlist tracks");
            throw;
        }
    }

    // ===== Legacy / Compatibility Methods =====

    public List<LibraryEntry> LoadDownloadedTracks()
    {
        if (DateTime.UtcNow - _lastLibraryCacheTime < TimeSpan.FromMinutes(5) && _libraryCache.Any())
            return _libraryCache;

        _libraryCache = LoadLibraryIndexFromDisk();
        _lastLibraryCacheTime = DateTime.UtcNow;
        return _libraryCache;
    }

    public async Task<List<LibraryEntry>> LoadDownloadedTracksAsync()
    {
        return await Task.Run(() => LoadDownloadedTracks());
    }

    public async Task AddTrackAsync(Track track, string actualFilePath, Guid sourcePlaylistId)
    {
        try
        {
            var entry = new LibraryEntry
            {
                UniqueHash = track.UniqueHash,
                Artist = track.Artist ?? "Unknown",
                Title = track.Title ?? "Unknown",
                Album = track.Album ?? "Unknown",
                FilePath = actualFilePath,
                Bitrate = track.Bitrate,
                DurationSeconds = track.Length,
                Format = track.Format ?? "Unknown"
            };

            await AddLibraryEntryAsync(entry);
            _logger.LogDebug("Added track to library: {Hash}", entry.UniqueHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add track");
            throw;
        }
    }

    // ===== Helper Conversion Methods =====

    private PlaylistJob EntityToPlaylistJob(PlaylistJobEntity entity)
    {
        return new PlaylistJob
        {
            Id = entity.Id,
            SourceTitle = entity.SourceTitle,
            SourceType = entity.SourceType,
            DestinationFolder = entity.DestinationFolder,
            CreatedAt = entity.CreatedAt,
            OriginalTracks = new ObservableCollection<Track>(),
            PlaylistTracks = entity.Tracks?.Select(EntityToPlaylistTrack).ToList() ?? new()
        };
    }

    private PlaylistTrack EntityToPlaylistTrack(PlaylistTrackEntity entity)
    {
        return new PlaylistTrack
        {
            Id = entity.Id,
            PlaylistId = entity.PlaylistId,
            Artist = entity.Artist,
            Title = entity.Title,
            Album = entity.Album,
            TrackUniqueHash = entity.TrackUniqueHash,
            Status = Enum.TryParse<TrackStatus>(entity.Status, out var status) ? status : TrackStatus.Missing,
            ResolvedFilePath = entity.ResolvedFilePath,
            TrackNumber = entity.TrackNumber,
            AddedAt = entity.AddedAt
        };
    }

    private PlaylistTrackEntity PlaylistTrackToEntity(PlaylistTrack track)
    {
        return new PlaylistTrackEntity
        {
            Id = track.Id,
            PlaylistId = track.PlaylistId,
            Artist = track.Artist,
            Title = track.Title,
            Album = track.Album,
            TrackUniqueHash = track.TrackUniqueHash,
            Status = track.Status.ToString(),
            ResolvedFilePath = track.ResolvedFilePath,
            TrackNumber = track.TrackNumber,
            AddedAt = track.AddedAt
        };
    }

    // ===== Private Helper Methods (JSON - LibraryEntry only) =====

    private List<LibraryEntry> LoadLibraryIndexFromDisk()
    {
        if (!File.Exists(_libraryIndexPath))
            return new List<LibraryEntry>();

        try
        {
            var json = File.ReadAllText(_libraryIndexPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<LibraryEntry>>(json, options) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load library index from {Path}", _libraryIndexPath);
            return new List<LibraryEntry>();
        }
    }

    private void SaveLibraryIndex(List<LibraryEntry> entries)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(entries, options);
            File.WriteAllText(_libraryIndexPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save library index");
            throw;
        }
    }
}
