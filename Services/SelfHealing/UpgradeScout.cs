using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Soulseek;
using SLSKDONET.Services.SelfHealing;

namespace SLSKDONET.Services.SelfHealing;

/// <summary>
/// Searches P2P network for higher quality versions of tracks.
/// Uses ±2s duration matching to prevent downloading wrong versions (radio edits, extended mixes).
/// </summary>
public class UpgradeScout
{
    private readonly ILogger<UpgradeScout> _logger;
    private readonly ISoulseekClient _soulseekClient;
    
    private const int DURATION_TOLERANCE_SECONDS = 2; // ±2s matching
    private const int MAX_CANDIDATES_PER_TRACK = 3;   // Return top 3 results
    private const int SEARCH_TIMEOUT_MS = 15000;       // 15 second search timeout
    
    public UpgradeScout(ILogger<UpgradeScout> logger, ISoulseekClient soulseekClient)
    {
        _logger = logger;
        _soulseekClient = soulseekClient;
    }
    
    /// <summary>
    /// Finds upgrade candidates for a given track.
    /// </summary>
    public async Task<List<UpgradeSearchResult>> FindUpgradesAsync(
        UpgradeCandidate candidate,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Searching for upgrades: {Artist} - {Title} (Current: {Bitrate}kbps {Format})",
            candidate.Artist, candidate.Title, candidate.CurrentBitrate, candidate.CurrentFormat);
        
        try
        {
            // Build search query
            var query = $"{candidate.Artist} {candidate.Title}";
            
            // Execute Soulseek search
            var searchResponse = await _soulseekClient.SearchAsync(
                SearchQuery.FromText(query),
                cancellationToken: ct
            );
            
            // Extract all files from all responses
            var allFiles = searchResponse.Responses
                .SelectMany(r => r.Files.Select(f => new { Response = r, File = f }))
                .ToList();
            
            _logger.LogDebug("Search returned {Count} total files", allFiles.Count);
            
            // Apply filters and scoring
            var scoredCandidates = allFiles
                .Where(item => PassesDurationFilter(item.File, candidate))
                .Where(item => PassesQualityFilter(item.File, candidate))
                .Where(item => PassesMetadataFilter(item.File, candidate))
                .Select(item => new UpgradeSearchResult
                {
                    Username = item.Response.Username,
                    Filename = item.File.Filename,
                    Size = item.File.Size,
                    BitRate = item.File.BitRate ?? 0,
                    Duration = item.File.Length ?? 0,
                    HasFreeSlot = item.Response.HasFreeUploadSlot,
                    QueueLength = item.Response.QueueLength,
                    UploadSpeed = item.Response.UploadSpeed,
                    QualityScore = CalculateQualityScore(item.File, item.Response, candidate)
                })
                .OrderByDescending(r => r.QualityScore)
                .Take(MAX_CANDIDATES_PER_TRACK)
                .ToList();
            
            _logger.LogInformation("Found {Count} upgrade candidates for {Track}",
                scoredCandidates.Count, candidate.Title);
            
            return scoredCandidates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search for upgrades for {Track}", candidate.Title);
            return new List<UpgradeSearchResult>();
        }
    }
    
    /// <summary>
    /// Duration matching with ±2s tolerance.
    /// Prevents downloading radio edits (3:30) when looking for full version (7:00).
    /// </summary>
    private bool PassesDurationFilter(Soulseek.File file, UpgradeCandidate candidate)
    {
        if (candidate.DurationSeconds == 0) return true; // No duration data available
        if (!file.Length.HasValue) return true; // File has no duration metadata
        
        var fileDuration = file.Length.Value;
        var secondsDiff = Math.Abs(fileDuration - candidate.DurationSeconds);
        
        if (secondsDiff > DURATION_TOLERANCE_SECONDS)
        {
            _logger.LogDebug("Duration mismatch: {File} ({FileDuration}s) vs expected ({ExpectedDuration}s)",
                file.Filename, fileDuration, candidate.DurationSeconds);
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Quality filter - must be better than current.
    /// </summary>
    private bool PassesQualityFilter(Soulseek.File file, UpgradeCandidate candidate)
    {
        var fileBitrate = file.BitRate ?? 0;
        
        // Must be better quality
        if (fileBitrate <= candidate.CurrentBitrate)
        {
            _logger.LogDebug("Quality too low: {Bitrate}kbps <= {CurrentBitrate}kbps",
                fileBitrate, candidate.CurrentBitrate);
            return false;
        }
        
        // FLAC-only mode
        if (candidate.TargetFormat == "flac")
        {
            var fileFormat = System.IO.Path.GetExtension(file.Filename)?.ToLowerInvariant();
            if (fileFormat != ".flac")
            {
                _logger.LogDebug("Not FLAC: {Format}", fileFormat);
                return false;
            }
        }
        
        // Minimum quality gain check
        var improvement = fileBitrate - candidate.CurrentBitrate;
        if (improvement < candidate.MinimumTargetBitrate - candidate.CurrentBitrate)
        {
            _logger.LogDebug("Insufficient quality gain: {Improvement}kbps", improvement);
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Fuzzy metadata matching (80% similarity).
    /// </summary>
    private bool PassesMetadataFilter(Soulseek.File file, UpgradeCandidate candidate)
    {
        var filename = System.IO.Path.GetFileNameWithoutExtension(file.Filename).ToLowerInvariant();
        var artist = candidate.Artist.ToLowerInvariant();
        var title = candidate.Title.ToLowerInvariant();
        
        // Simple contains check for now
        // TODO: Implement proper Levenshtein distance for 80% threshold
        var hasArtist = filename.Contains(artist);
        var hasTitle = filename.Contains(title);
        
        if (!hasArtist && !hasTitle)
        {
            _logger.LogDebug("Metadata mismatch: {Filename}", file.Filename);
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Calculates quality score: (Quality_Improvement * Peer_Confidence).
    /// </summary>
    private int CalculateQualityScore(Soulseek.File file, SearchResponse response, UpgradeCandidate candidate)
    {
        var score = 0;
        var fileBitrate = file.BitRate ?? 0;
        
        // 1. Bitrate improvement (primary factor)
        var bitrateImprovement = fileBitrate - candidate.CurrentBitrate;
        score += bitrateImprovement;
        
        // 2. FLAC bonus (if target is FLAC)
        if (candidate.TargetFormat == "flac")
        {
            var format = System.IO.Path.GetExtension(file.Filename)?.ToLowerInvariant();
            if (format == ".flac")
            {
                score += 500; // Large bonus for FLAC
            }
        }
        
        // 3. Exact duration match bonus
        if (file.Length.HasValue && file.Length.Value == candidate.DurationSeconds)
        {
            score += 100;
        }
        
        // 4. Free upload slot bonus (peer confidence)
        if (response.HasFreeUploadSlot)
        {
            score += 50;
        }
        
        // 5. Upload speed bonus (higher speed = higher confidence)
        if (response.UploadSpeed > 0)
        {
            score += Math.Min(response.UploadSpeed / 10000, 25); // Cap at 25 points
        }
        
        // 6. Queue length penalty
        score -= response.QueueLength * 2;
        
        return Math.Max(0, score);
    }
}

/// <summary>
/// Represents a search result for an upgrade candidate.
/// </summary>
public class UpgradeSearchResult
{
    public string Username { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public long Size { get; set; }
    public int BitRate { get; set; }
    public int Duration { get; set; }
    public bool HasFreeSlot { get; set; }
    public int QueueLength { get; set; }
    public int UploadSpeed { get; set; }
    public int QualityScore { get; set; }
    
    public override string ToString() =>
        $"{Filename} ({BitRate}kbps) from {Username} [Score: {QualityScore}]";
}
