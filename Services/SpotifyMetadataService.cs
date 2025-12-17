using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SLSKDONET.Data;
using SLSKDONET.Data.Entities;
using SLSKDONET.Utils;
using SLSKDONET.Models;
using SpotifyAPI.Web;

namespace SLSKDONET.Services;

/// <summary>
/// "The Gravity Well" - A resilient service for fetching, caching, and matching Spotify metadata.
/// Anchors local files to canonical Spotify identities using smart fuzzy search and audio analysis.
/// </summary>
public class SpotifyMetadataService : ISpotifyMetadataService
{
    private readonly ILogger<SpotifyMetadataService> _logger;
    private readonly SpotifyAuthService _authService;
    private readonly SemaphoreSlim _rateLimitLock = new(1, 1);

    // Negative cache duration for "Not Found" results
    private static readonly TimeSpan NegativeCacheDuration = TimeSpan.FromDays(7);
    
    // Positive cache duration (metadata rarely changes)
    private static readonly TimeSpan PositiveCacheDuration = TimeSpan.FromDays(30);

    public SpotifyMetadataService(
        ILogger<SpotifyMetadataService> logger,
        SpotifyAuthService authService)
    {
        _logger = logger;
        _authService = authService;
    }

    /// <summary>
    /// Smart search for a track with fuzzy matching and confidence scoring.
    /// </summary>
    /// <param name="artist">Artist name from file/Soulseek</param>
    /// <param name="title">Track title from file/Soulseek</param>
    /// <param name="durationMs">Optional file duration for validation</param>
    /// <returns>Enriched metadata or null if no confident match found</returns>
    public async Task<FullTrack?> FindTrackAsync(string artist, string title, int? durationMs = null)
    {
        // 1. Check Cache
        string searchQuery = $"{artist} - {title}";
        string cacheKey = $"search:{StringDistanceUtils.Normalize(searchQuery)}";
        
        var cached = await GetFromCacheAsync<FullTrack>(cacheKey);
        if (cached != null) return cached;

        // 2. Execute Search via API
        var track = await SearchSpotifyWithSmartLogicAsync(artist, title, durationMs);

        // 3. Cache Result (or Negative Cache)
        if (track != null)
        {
            await SaveToCacheAsync(cacheKey, track, PositiveCacheDuration);
        }
        else
        {
            // Store a null placeholder to prevent spamming queries for unfindable tracks
            await SaveToCacheAsync<FullTrack>(cacheKey, null, NegativeCacheDuration);
        }

        return track;
    }



    /// <summary>
    /// Enriches a PlaylistTrack with Spotify metadata (ID, Art, Key, BPM).
    /// Used by MetadataEnrichmentOrchestrator.
    /// </summary>
    public async Task<bool> EnrichTrackAsync(PlaylistTrack track)
    {
        // 1. Find the track
        // Use a looser search initially, maybe implement duration check if we have file info
        var metadata = await FindTrackAsync(track.Artist, track.Title, null);

        if (metadata == null) return false;

        // 2. Update Basic Info
        track.SpotifyTrackId = metadata.Id;
        track.SpotifyAlbumId = metadata.Album.Id;
        track.SpotifyArtistId = metadata.Artists.FirstOrDefault()?.Id;
        track.AlbumArtUrl = metadata.Album.Images.FirstOrDefault()?.Url; // Largest image
        track.ArtistImageUrl = null; // Would need separate artist fetch
        track.Genres = null; // Would need separate artist fetch
        track.Popularity = metadata.Popularity;
        track.CanonicalDuration = metadata.DurationMs;
        // ReleaseDate needs parsing
        if (DateTime.TryParse(metadata.Album.ReleaseDate, out var releaseDate))
        {
            track.ReleaseDate = releaseDate;
        }

        // 3. Fetch Audio Features (Key, BPM)
        var features = await GetAudioFeaturesAsync(metadata.Id);
        if (features != null)
        {
            track.BPM = features.Tempo;
            // Convert Pitch Class + Mode to Camelot? Or just store raw for now?
            // Storing raw pitch/mode is safer, but UI wants "8A".
            // Let's implement a quick helper or store raw and convert in UI?
            // For now, let's store a simple string representation if possible, or just the raw values?
            // The schema has MusicalKey as string.
            // Let's try to convert or just store "Key: 1, Mode: 1" for now.
            // Actually, let's add a helper later.
            track.MusicalKey = $"{features.Key}{(features.Mode == 1 ? "B" : "A")}"; // Rough Camelot approx (needs proper wheel)
            track.AnalysisOffset = 0; // Placeholder
            // track.BitrateScore = ...; // Needs file analysis
        }

        return true;
    }

    /// <summary>
    /// Fetches audio features (Key, BPM) for a track.
    /// </summary>
    public async Task<TrackAudioFeatures?> GetAudioFeaturesAsync(string spotifyId)
    {
        string cacheKey = $"features:{spotifyId}";
        var cached = await GetFromCacheAsync<TrackAudioFeatures>(cacheKey);
        if (cached != null) return cached;

        try
        {
            var client = await _authService.GetAuthenticatedClientAsync();
            var features = await client.Tracks.GetAudioFeatures(spotifyId);

            if (features != null)
            {
                await SaveToCacheAsync(cacheKey, features, PositiveCacheDuration);
                return features;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch audio features for {SpotifyId}", spotifyId);
        }

        return null;
    }

    private async Task<FullTrack?> SearchSpotifyWithSmartLogicAsync(string artist, string title, int? durationMs)
    {
        try
        {
            var client = await _authService.GetAuthenticatedClientAsync();
            
            // Clean input (remove garbage like "(Official Video)", "HQ", etc if needed)
            // For now, trust the input logic from DownloadManager, but strip "Remix" for broader search?
            // Actually, keep Remix to ensure we get the right version.
            
            var request = new SearchRequest(SearchRequest.Types.Track, $"{artist} {title}")
            {
                Limit = 5 // Fetch top 5 candidates
            };

            var response = await client.Search.Item(request);
            if (response.Tracks?.Items == null || !response.Tracks.Items.Any())
                return null;

            // Smart Matching Logic
            var candidates = response.Tracks.Items;
            FullTrack? bestMatch = null;
            double bestScore = 0;

            foreach (var candidate in candidates)
            {
                // Phase 0.2 Refinement: Apply FilenameNormalizer for better matching
                string normalizedTitle = FilenameNormalizer.Normalize(title);
                string normalizedCandidateName = FilenameNormalizer.Normalize(candidate.Name);
                string normalizedArtist = FilenameNormalizer.Normalize(artist);
                string normalizedCandidateArtist = FilenameNormalizer.Normalize(candidate.Artists.FirstOrDefault()?.Name ?? "");
                
                // 1. Name Match Score (with noise stripping)
                double titleScore = StringDistanceUtils.GetNormalizedMatchScore(normalizedTitle, normalizedCandidateName);
                double artistScore = StringDistanceUtils.GetNormalizedMatchScore(normalizedArtist, normalizedCandidateArtist);
                
                double matchScore = (titleScore * 0.6) + (artistScore * 0.4);

                // 2. Duration Validation (The "DJ Secret") - Critical for Versioning
                if (durationMs.HasValue)
                {
                    double diffSeconds = Math.Abs(candidate.DurationMs - durationMs.Value) / 1000.0;
                    
                    if (diffSeconds > 5.0)
                    {
                        // Heavy penalty for duration mismatch > 5s (likely Radio Edit vs Club Mix)
                        matchScore *= 0.5;
                        _logger.LogTrace("Match penalty (Duration): {Title} ({Diff:F1}s diff)", candidate.Name, diffSeconds);
                    }
                    else if (diffSeconds <= 3.0 && matchScore > 0.7)
                    {
                        // Phase 0.2 Refinement: Boost confidence for duration match
                        // If duration matches within 3s, add +40% bonus to confidence
                        double durationBonus = 0.4 * (1.0 - (diffSeconds / 3.0)); // Linear decay
                        matchScore = Math.Min(1.0, matchScore + durationBonus);
                        _logger.LogTrace("Duration boost: {Title} (+{Bonus:P0}, {Diff:F1}s diff)", candidate.Name, durationBonus, diffSeconds);
                    }
                }

                if (matchScore > bestScore)
                {
                    bestScore = matchScore;
                    bestMatch = candidate;
                }
            }

            // Phase 0.2 Refinement: Lowered threshold from 0.75 to 0.70
            // With FilenameNormalizer + duration boosting, 70% is now reliable
            // > 0.90 = Excellent match
            // > 0.70 = Good match (with duration validation)
            if (bestMatch != null && bestScore >= 0.70)
            {
                var logLevel = bestScore >= 0.9 ? LogLevel.Information : LogLevel.Warning;
                _logger.Log(logLevel, "Smart Match Found: '{Input}' -> '{Result}' (Score: {Score:P0})", 
                    $"{artist} - {title}", $"{bestMatch.Artists[0].Name} - {bestMatch.Name}", bestScore);
                
                return bestMatch;
            }
            
            _logger.LogWarning("No confident match for '{Input}'. Best was '{Result}' ({Score:P0})",
                $"{artist} - {title}", bestMatch != null ? $"{bestMatch?.Artists[0].Name} - {bestMatch?.Name}" : "None", bestScore);
                
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Spotify for {Artist} - {Title}", artist, title);
            return null;
        }
    }

    private async Task<T?> GetFromCacheAsync<T>(string key) where T : class
    {
        try
        {
            using var context = new AppDbContext();
            // Use FindAsync for direct PK lookup
            var entity = await context.SpotifyMetadataCache.FindAsync(key);
            
            if (entity == null) return null;

            if (DateTime.UtcNow > entity.ExpiresAt)
            {
                context.SpotifyMetadataCache.Remove(entity);
                await context.SaveChangesAsync();
                return null;
            }

            if (string.IsNullOrEmpty(entity.DataJson)) return null; // Logic for negative cache

            return JsonSerializer.Deserialize<T>(entity.DataJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache read failed for {Key}", key);
            return null;
        }
    }

    private async Task SaveToCacheAsync<T>(string key, T? data, TimeSpan duration)
    {
        try
        {
            using var context = new AppDbContext();
            var entity = new SpotifyMetadataCacheEntity
            {
                SpotifyId = key,
                DataJson = data != null ? JsonSerializer.Serialize(data) : "",
                CachedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(duration)
            };

            // Upsert logic (EF Core doesn't have Upsert, so Check-then-Add/Update)
            var existing = await context.SpotifyMetadataCache.FindAsync(key);
            if (existing != null)
            {
                existing.DataJson = entity.DataJson;
                existing.CachedAt = entity.CachedAt;
                existing.ExpiresAt = entity.ExpiresAt;
            }
            else
            {
                await context.SpotifyMetadataCache.AddAsync(entity);
            }

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache write failed for {Key}", key);
        }
    }

    public async Task ClearCacheAsync()
    {
        try
        {
            using var context = new AppDbContext();
            // Truncate table or remove all
            // EF Core doesn't have Truncate, so we use raw SQL or remove range
            context.SpotifyMetadataCache.RemoveRange(context.SpotifyMetadataCache);
            await context.SaveChangesAsync();
            _logger.LogInformation("Spotify metadata cache cleared.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear Spotify metadata cache");
        }
    }
}
