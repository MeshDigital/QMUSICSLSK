using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SLSKDONET.Models;

namespace SLSKDONET.Data;

/// <summary>
/// Database entity for a track in the persisted queue.
/// </summary>
public class TrackEntity
{
    [Key]
    public string GlobalId { get; set; } = string.Empty; // TrackUniqueHash

    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = "Pending";
    public string Filename { get; set; } = string.Empty;
    public string SoulseekUsername { get; set; } = string.Empty;
    public long Size { get; set; }
    
    // Metadata for re-hydration
    public DateTime AddedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? CoverArtUrl { get; set; } // Added for Album Art
}

/// <summary>
/// Database entity for a playlist/import job header.
/// </summary>
public class PlaylistJobEntity
{
    [Key]
    public Guid Id { get; set; }

    public string SourceTitle { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty; // "Spotify", "CSV", etc.
    public string DestinationFolder { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    // Counts for quick access
    public int TotalTracks { get; set; }
    public int SuccessfulCount { get; set; }
    public int FailedCount { get; set; }

    // Phase 1C: Add Soft Delete Flag
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    
    [InverseProperty(nameof(PlaylistTrackEntity.Job))]
    public ICollection<PlaylistTrackEntity> Tracks { get; set; } = new List<PlaylistTrackEntity>();
}

/// <summary>
/// Database entity for a track within a playlist.
/// </summary>
public class PlaylistTrackEntity
{
    [Key]
    public Guid Id { get; set; }

    [ForeignKey(nameof(Job))]
    public Guid PlaylistId { get; set; }
    
    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string TrackUniqueHash { get; set; } = string.Empty;
    public TrackStatus Status { get; set; } = TrackStatus.Missing; // Changed to enum
    public string ResolvedFilePath { get; set; } = string.Empty;
    public int TrackNumber { get; set; }

    // User engagement
    public int Rating { get; set; } = 0; // 1-5 stars, 0 = not rated
    public bool IsLiked { get; set; } = false;
    public int PlayCount { get; set; } = 0;
    public DateTime? LastPlayedAt { get; set; }

    public DateTime AddedAt { get; set; }
    public int SortOrder { get; set; }
    
    public PlaylistJobEntity? Job { get; set; }
}

/// <summary>
/// Database entity for a unique, downloaded file in the global library.
/// This replaces the old JSON-based LibraryEntry.
/// </summary>
public class LibraryEntryEntity
{
    [Key]
    public string UniqueHash { get; set; } = string.Empty;

    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? OriginalFilePath { get; set; } // Track original path for resolution tracking

    // Audio metadata
    public int Bitrate { get; set; }
    public int? DurationSeconds { get; set; }
    public string Format { get; set; } = string.Empty;

    // Timestamps
    public DateTime AddedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public DateTime? FilePathUpdatedAt { get; set; } // Track when path was last resolved/updated
}
