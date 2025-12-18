using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Configuration;
using SLSKDONET.Models;
using SLSKDONET.Services;

namespace SLSKDONET.Services;

/// <summary>
/// Service for identifying library upgrade candidates (Self-Healing Library).
/// Scans for low-quality or faked files.
/// </summary>
public class LibraryUpgradeScout
{
    private readonly ILogger<LibraryUpgradeScout> _logger;
    private readonly DatabaseService _databaseService;
    private readonly AppConfig _config;

    public LibraryUpgradeScout(
        ILogger<LibraryUpgradeScout> logger,
        DatabaseService databaseService,
        AppConfig config)
    {
        _logger = logger;
        _databaseService = databaseService;
        _config = config;
    }

    /// <summary>
    /// Scans the entire database for tracks that meet the user-defined upgrade criteria.
    /// </summary>
    public async Task<List<Data.TrackEntity>> GetUpgradeCandidatesAsync(CancellationToken token = default)
    {
        try
        {
            _logger.LogInformation("Scouting library for upgrade candidates (Bitrate < {Threshold}kbps)...", _config.UpgradeMinBitrateThreshold);
            
            // 1. Fetch all tracks from DB
            var allTracks = await _databaseService.LoadAllTracksAsync();
            
            // 2. Filter for upgrade candidates
            var candidates = allTracks.Where(t => IsUpgradeCandidate(t)).ToList();
            
            _logger.LogInformation("Found {Count} potential upgrade candidates.", candidates.Count);
            return candidates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan library for upgrade candidates.");
            return new List<Data.TrackEntity>();
        }
    }

    private bool IsUpgradeCandidate(Data.TrackEntity track)
    {
        // Don't propose tracks that are already in the middle of being upgraded/downloaded
        if (track.State == "Downloading" || track.State == "Searching") return false;
        
        // Don't propose if no file exists (that's a regular missing track)
        if (string.IsNullOrEmpty(track.Filename)) return false;

        // Condition A: Bitrate is below threshold
        bool lowBitrate = track.Bitrate.HasValue && track.Bitrate < _config.UpgradeMinBitrateThreshold;
        
        // Condition B: Flagged as untrustworthy (fake)
        bool untrustworthy = track.IsTrustworthy == false;

        return lowBitrate || untrustworthy;
    }
}
