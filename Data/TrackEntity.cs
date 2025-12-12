using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
    public string Status { get; set; } = "Missing"; // Downloaded, Missing, Failed, etc.
    public string ResolvedFilePath { get; set; } = string.Empty;
    public int TrackNumber { get; set; }
    public DateTime AddedAt { get; set; }
    
    public PlaylistJobEntity? Job { get; set; }
}
