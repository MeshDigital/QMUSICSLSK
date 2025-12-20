using System.Collections.Generic;
using System.Threading.Tasks;
using SLSKDONET.Models;
using SpotifyAPI.Web;

namespace SLSKDONET.Services;

public interface ISpotifyMetadataService
{
    /// <summary>
    /// Smart search for a track with fuzzy matching and confidence scoring.
    /// </summary>
    Task<FullTrack?> FindTrackAsync(string artist, string title, int? durationMs = null);

    /// <summary>
    /// Fetches audio features (Key, BPM) for a track.
    /// </summary>
    Task<TrackAudioFeatures?> GetAudioFeaturesAsync(string spotifyId);

    /// <summary>
    /// Fetches audio features (Key, BPM) for a batch of tracks.
    /// </summary>
    Task<Dictionary<string, TrackAudioFeatures?>> GetAudioFeaturesBatchAsync(IEnumerable<string> spotifyIds);

    /// <summary>
    /// Enriches a PlaylistTrack with Spotify metadata (ID, Art, Key, BPM).
    /// Used by MetadataEnrichmentOrchestrator.
    /// </summary>
    Task<bool> EnrichTrackAsync(PlaylistTrack track);

    /// <summary>
    /// Clears the internal metadata cache.
    /// </summary>
    Task ClearCacheAsync();
}

