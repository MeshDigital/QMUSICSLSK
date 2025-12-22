using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Data;

namespace SLSKDONET.Services;

/// <summary>
/// Background worker that periodically scans the library for tracks missing Spotify metadata
/// and enriches them using the SpotifyEnrichmentService.
/// Runs in background loop with rate limiting.
/// </summary>
public class LibraryEnrichmentWorker : IDisposable
{
    private readonly ILogger<LibraryEnrichmentWorker> _logger;
    private readonly DatabaseService _databaseService;
    private readonly SpotifyEnrichmentService _enrichmentService;
    private CancellationTokenSource? _cts;
    private Task? _workerTask;
    
    // Configurable settings
    private const int BatchSize = 5;
    private const int RateLimitDelayMs = 2500; // 2.5s delay to be safe against Spotify API limits
    private const int IdleDelayMinutes = 5;

    public LibraryEnrichmentWorker(
        ILogger<LibraryEnrichmentWorker> logger,
        DatabaseService databaseService,
        SpotifyEnrichmentService enrichmentService)
    {
        _logger = logger;
        _databaseService = databaseService;
        _enrichmentService = enrichmentService;
    }

    public void Start()
    {
        if (_workerTask != null && !_workerTask.IsCompleted)
            return;

        _cts = new CancellationTokenSource();
        _workerTask = Task.Run(EnrichmentLoopAsync, _cts.Token);
        _logger.LogInformation("LibraryEnrichmentWorker started.");
    }

    public async Task StopAsync()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            try 
            {
                if (_workerTask != null) await _workerTask;
            }
            catch (OperationCanceledException) { }
        }
        _logger.LogInformation("LibraryEnrichmentWorker stopped.");
    }

    private async Task EnrichmentLoopAsync()
    {
        try
        {
            // Initial delay to let app stabilize
            await Task.Delay(TimeSpan.FromSeconds(30), _cts.Token);
            _logger.LogInformation("LibraryEnrichmentWorker loop active.");

            while (!_cts.Token.IsCancellationRequested)
            {
                try 
                {
                    bool workDone = await ProcessBatchAsync();
                    
                    if (!workDone)
                    {
                        // Wait if no work was found
                        await Task.Delay(TimeSpan.FromMinutes(IdleDelayMinutes), _cts.Token);
                    }
                    else 
                    {
                         // Brief pause between batches
                         await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in EnrichmentLoop");
                    await Task.Delay(TimeSpan.FromMinutes(1), _cts.Token);
                }
            }
        }
        catch (OperationCanceledException) { /* Graceful shutdown */ }
    }

    private async Task<bool> ProcessBatchAsync()
    {
        bool didWork = false;

        // --- PASS 1: Identification (Stage 1) ---
        // Find tracks with NO Spotify ID (e.g. from CSV)
        var unidentified = await _databaseService.GetLibraryEntriesNeedingEnrichmentAsync(BatchSize);
        
        if (unidentified.Any())
        {
            _logger.LogInformation("Enrichment Pass 1: Identification for {Count} tracks", unidentified.Count);
            
            foreach (var track in unidentified)
            {
                if (_cts.Token.IsCancellationRequested) break;
                
                // Rate limit for search API
                await Task.Delay(RateLimitDelayMs, _cts.Token); 

                try 
                {
                    var result = await _enrichmentService.IdentifyTrackAsync(track.Artist, track.Title);
                    await _databaseService.UpdateLibraryEntryEnrichmentAsync(track.UniqueHash, result);
                    
                    if (result.Success)
                         _logger.LogDebug("Identified: {Artist} - {Title} => {SpotifyId}", track.Artist, track.Title, result.SpotifyId);
                    else
                         _logger.LogDebug("Identification failed: {Artist} - {Title}", track.Artist, track.Title);
                }
                catch (Exception ex)
                {
                     _logger.LogError(ex, "Pass 1 failed for track {Hash}", track.UniqueHash);
                }
            }
            didWork = true;
        }

        // --- PASS 2: Musical Intelligence (Stage 2) ---
        // Find tracks WITH Spotify ID but NO Features (IsEnriched = false)
        // Can process up to 100 at once efficiently
        var needingFeatures = await _databaseService.GetLibraryEntriesNeedingFeaturesAsync(50); // Get 50 to benefit from batching
        
        if (needingFeatures.Any())
        {
             _logger.LogInformation("Enrichment Pass 2: Batch Features for {Count} tracks", needingFeatures.Count);
             
             // Extract IDs
             var ids = needingFeatures
                .Select(t => t.SpotifyTrackId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Select(id => id!)
                .ToList();
             
             if (ids.Any())
             {
                 try 
                 {
                     // Single API call for up to 50 tracks
                     var featuresMap = await _enrichmentService.GetAudioFeaturesBatchAsync(ids);
                     
                     // Batch DB update
                     await _databaseService.UpdateLibraryEntriesFeaturesAsync(featuresMap);
                     
                     _logger.LogInformation("Pass 2 Complete: Enriched {Count} tracks with audio features", featuresMap.Count);
                 }
                 catch (Exception ex)
                 {
                     _logger.LogError(ex, "Pass 2 Batch failed");
                 }
             }
             didWork = true;
        }

        return didWork;
    }

    public void Dispose()
    {
        _cts?.Cancel();
    }
}
