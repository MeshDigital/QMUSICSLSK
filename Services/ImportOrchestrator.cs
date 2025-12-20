using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;
using SLSKDONET.ViewModels;

namespace SLSKDONET.Services;

/// <summary>
/// Centralized orchestrator for all import operations.
/// Handles the entire import pipeline from source parsing to library persistence.
/// </summary>
public class ImportOrchestrator
{
    private readonly ILogger<ImportOrchestrator> _logger;
    private readonly ImportPreviewViewModel _previewViewModel;
    private readonly DownloadManager _downloadManager;
    private readonly INavigationService _navigationService;
    private readonly Views.INotificationService _notificationService;
    private readonly ILibraryService _libraryService;

    // Track current import to avoid duplicate event subscriptions in older logic
    // private bool _isHandlingImport; // REMOVED: Unused

    public ImportOrchestrator(
        ILogger<ImportOrchestrator> logger,
        ImportPreviewViewModel previewViewModel,
        DownloadManager downloadManager,
        INavigationService navigationService,
        Views.INotificationService notificationService,
        ILibraryService libraryService)
    {
        _logger = logger;
        _previewViewModel = previewViewModel;
        _downloadManager = downloadManager;
        _navigationService = navigationService;
        _notificationService = notificationService;
        _libraryService = libraryService;
    }

    /// <summary>
    /// Import with preview screen - allows user to select tracks.
    /// </summary>
    /// <summary>
    /// Import with preview screen - allows user to select tracks.
    /// Supports streaming for immediate UI feedback.
    /// </summary>
    public async Task StartImportWithPreviewAsync(IImportProvider provider, string input)
    {
        try
        {
            _logger.LogInformation("Starting import with preview from {Provider}: {Input}", 
                provider.Name, input);

            if (provider is IStreamingImportProvider streamProvider)
            {
                 // Phase 7: Deterministic ID / Deduplication
                 // Generate ID based on the input URL/Source
                 var newJobId = Utils.GuidGenerator.CreateFromUrl(input);
                 
                 // Check if it already exists
                 // We need to inject ILibraryService to check this.
                 // Assuming _libraryService is available (need to add to constructor if not).
                 // Ah, constructor needs updating.
                 
                 // Streaming Mode
                 var existingJob = await _libraryService.FindPlaylistJobAsync(newJobId);
                 
                 _previewViewModel.InitializeStreamingPreview(provider.Name, provider.Name, newJobId, input, existingJob);
                 
                 // 2. Set up callbacks
                 SetupPreviewCallbacks();

                 // 3. Navigate immediately
                 _navigationService.NavigateTo("ImportPreview");
                 
                 // 4. Start streaming in background
                 _ = Task.Run(async () => await StreamPreviewAsync(streamProvider, input));
            }
            else
            {
                // Legacy Mode
                var result = await provider.ImportAsync(input);

                if (!result.Success)
                {
                    _notificationService.Show("Import Failed", result.ErrorMessage ?? "Unknown error", Views.NotificationType.Error);
                    return;
                }

                if (!result.Tracks.Any())
                {
                    _notificationService.Show("No Tracks Found", $"No tracks found in {provider.Name}", Views.NotificationType.Warning);
                    return;
                }

                await _previewViewModel.InitializePreviewAsync(result.SourceTitle, provider.Name, result.Tracks);
                SetupPreviewCallbacks();
                _navigationService.NavigateTo("ImportPreview");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start import from {Provider}", provider.Name);
            _notificationService.Show("Import Error", $"Failed to import: {ex.Message}", Views.NotificationType.Error);
        }
    }

    private async Task StreamPreviewAsync(IStreamingImportProvider provider, string input)
    {
        try
        {
            await foreach (var batch in provider.ImportStreamAsync(input))
            {
                 // Update Source Title from first batch if needed
                 if (!string.IsNullOrEmpty(batch.SourceTitle) && _previewViewModel.SourceTitle == provider.Name)
                 {
                     await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => 
                     {
                         _previewViewModel.SourceTitle = batch.SourceTitle;
                     });
                 }
                 
                 await _previewViewModel.AddTracksToPreviewAsync(batch.Tracks);
            }
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error during streaming preview");
             await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => _previewViewModel.StatusMessage = "Stream error: " + ex.Message);
        }
        finally
        {
             await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => _previewViewModel.IsLoading = false);
        }
    }

    /// <summary>
    /// Import all tracks directly without preview - "Add All" button flow.
    /// </summary>
    /// <summary>
    /// Import all tracks directly without preview - "Add All" button flow.
    /// Supports streaming for immediate UI feedback.
    /// </summary>
    public async Task ImportAllDirectlyAsync(IImportProvider provider, string input)
    {
        try
        {
            _logger.LogInformation("Starting direct import from {Provider}: {Input}", provider.Name, input);

            if (provider is IStreamingImportProvider streamProvider)
            {
                await ImportStreamInternalAsync(streamProvider, input);
            }
            else
            {
                // Legacy blocking fallback
                await ImportBlockingInternalAsync(provider, input);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import all tracks from {Provider}", provider.Name);
            _notificationService.Show(
                "Import Error",
                $"Failed to import: {ex.Message}",
                Views.NotificationType.Error,
                TimeSpan.FromSeconds(5));
        }
    }

    private async Task ImportStreamInternalAsync(IStreamingImportProvider provider, string input)
    {
        PlaylistJob? job = null;
        bool isFirstBatch = true;
        
        await foreach (var batch in provider.ImportStreamAsync(input))
        {
            if (!batch.Tracks.Any()) continue;

            if (isFirstBatch)
            {
                // Create job immediately
                job = CreatePlaylistJob(batch.SourceTitle, provider.Name, new List<SearchQuery>());
                job.TotalTracks = batch.TotalEstimated; // Set approximate total for progress bar
                
                // Save empty job to DB to get ID
                // Note: We need a way to save the header first.
                // Assuming SavePlaylistJobAsync exists in LibraryService, but here we depend on DownloadManager.
                // DownloadManager.QueueProject handles header creation? Yes.
                // But we want to queue tracks incrementally.
                
                // Step 1: Initialize Job in DownloadManager (Header Only)
                // We'll pass empty tracks first just to register the job?
                // Actually DownloadManager.QueueProject takes the job with tracks.
                // We likely need a new method: InitializeImportJob(PlaylistJob job)
                // For now, let's just queue the first batch standard way to start.
                
                isFirstBatch = false;
                
                // Add first batch tracks
                var tracks = MapQueriesToTracks(batch.Tracks, batch.SourceTitle);
                foreach(var t in tracks) job.OriginalTracks.Add(t);
                
                await _downloadManager.QueueProject(job);
                
                _notificationService.Show("Import Started", $"Streaming from {batch.SourceTitle}...", Views.NotificationType.Success, TimeSpan.FromSeconds(2));
                _navigationService.NavigateTo("Library");
            }
            else
            {
                if (job == null) continue;

                // Process subsequent batches
                var tracks = MapQueriesToTracks(batch.Tracks, batch.SourceTitle);
                var playlistTracks = new System.Collections.Generic.List<PlaylistTrack>();
                
                // Convert to PlaylistTrack for DB insertion
                // We need the Job ID
                int maxTrackNum = job.TotalTracks; // Approximate logic, ideally fetch max from DB
                // Just append based on count so far?
                int currentCount = job.OriginalTracks.Count;
                
                foreach (var t in tracks)
                {
                    job.OriginalTracks.Add(t);
                    
                    var pt = new PlaylistTrack
                    {
                         Id = Guid.NewGuid(),
                         PlaylistId = job.Id,
                         Artist = t.Artist,
                         Title = t.Title,
                         Album = t.Album,
                         TrackUniqueHash = t.SpotifyTrackId ?? Guid.NewGuid().ToString(),
                         Status = TrackStatus.Missing,
                         TrackNumber = ++currentCount,
                         SpotifyTrackId = t.SpotifyTrackId,
                         AlbumArtUrl = t.AlbumArtUrl,
                         AddedAt = DateTime.UtcNow
                    };
                    playlistTracks.Add(pt);
                }
                
                // Save new tracks to DB directly to bypass DownloadManager overhead if needed,
                // BUT we need DownloadManager to orchestrate downloads.
                // Use DownloadManager.QueueTracks(List<PlaylistTrack> tracks)
                // We need to implement QueueTracks if it doesn't exist or expose it.
                // Checking ProjectListViewModel usage: _downloadManager.QueueTracks exists!
                
                _downloadManager.QueueTracks(playlistTracks);
            }
        }
        
        if (job != null)
        {
             _logger.LogInformation("Stream import complete. Total tracks: {Count}", job.OriginalTracks.Count);
        }
    }

    private async Task ImportBlockingInternalAsync(IImportProvider provider, string input)
    {
        var result = await provider.ImportAsync(input);

        if (!result.Success)
        {
            _notificationService.Show("Import Failed", result.ErrorMessage ?? "Unknown error", Views.NotificationType.Error);
            return;
        }

        if (!result.Tracks.Any())
        {
            _notificationService.Show("No Tracks Found", "No tracks found.", Views.NotificationType.Warning);
            return;
        }

        var job = CreatePlaylistJob(result.SourceTitle, provider.Name, result.Tracks);
        await _downloadManager.QueueProject(job);

        _notificationService.Show("Import Complete", $"{result.Tracks.Count} tracks added.", Views.NotificationType.Success);
        _navigationService.NavigateTo("Library");
    }

    private List<Track> MapQueriesToTracks(List<SearchQuery> queries, string sourceTitle)
    {
        return queries.Select(query => new Track
        {
            Artist = query.Artist,
            Title = query.Title,
            Album = query.Album ?? sourceTitle,
            Length = query.Length,
            SourceTitle = sourceTitle,
            SpotifyTrackId = query.SpotifyTrackId,
            SpotifyAlbumId = query.SpotifyAlbumId,
            SpotifyArtistId = query.SpotifyArtistId,
            AlbumArtUrl = query.AlbumArtUrl,
            ArtistImageUrl = query.ArtistImageUrl,
            Genres = query.Genres,
            Popularity = query.Popularity,
            CanonicalDuration = query.CanonicalDuration,
            ReleaseDate = query.ReleaseDate
        }).ToList();
    }

    /// <summary>
    /// Set up event handlers for preview screen callbacks.
    /// </summary>
    private void SetupPreviewCallbacks()
    {
        // Always clean up any existing subscriptions first to avoid doubles
        _logger.LogInformation("Setting up ImportPreviewViewModel event callbacks");
        _previewViewModel.AddedToLibrary -= OnPreviewConfirmed;
        _previewViewModel.Cancelled -= OnPreviewCancelled;

        // Subscribe
        _previewViewModel.AddedToLibrary += OnPreviewConfirmed;
        _previewViewModel.Cancelled += OnPreviewCancelled;
    }

    /// <summary>
    /// Handle when user confirms tracks in preview screen.
    /// </summary>
    private void OnPreviewConfirmed(object? sender, PlaylistJob job)
    {
        try
        {
            _logger.LogInformation("Preview confirmed: {Title} with {Count} tracks",
                job.SourceTitle, job.OriginalTracks.Count);

            // Queue project (already done in ImportPreviewViewModel, but keeping for clarity)
            // Note: ImportPreviewViewModel.AddToLibraryAsync already calls QueueProject
            // so we don't need to call it again here

            // Navigate to library
            _navigationService.NavigateTo("Library");

            _logger.LogInformation("Import completed and navigated to Library");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle preview confirmation");
        }
        finally
        {
            CleanupCallbacks();
        }
    }

    /// <summary>
    /// Handle when user cancels preview.
    /// </summary>
    private void OnPreviewCancelled(object? sender, EventArgs e)
    {
        _logger.LogInformation("Import preview cancelled");
        _navigationService.GoBack();
        CleanupCallbacks();
    }

    /// <summary>
    /// Remove event handlers after import completes.
    /// </summary>
    private void CleanupCallbacks()
    {
        _previewViewModel.AddedToLibrary -= OnPreviewConfirmed;
        _previewViewModel.Cancelled -= OnPreviewCancelled;
    }

    /// <summary>
    /// Create a PlaylistJob from import results.
    /// NOTE: All tracks in a playlist are treated as belonging to the same "album" 
    /// for grouping and download purposes.
    /// </summary>
    private PlaylistJob CreatePlaylistJob(string sourceTitle, string sourceType, System.Collections.Generic.List<SearchQuery> queries)
    {
        var job = new PlaylistJob
        {
            Id = Guid.NewGuid(),
            SourceTitle = sourceTitle,
            SourceType = sourceType,
            CreatedAt = DateTime.UtcNow,
            DestinationFolder = string.Empty // Will use default from config
        };

        // Convert queries to Track objects
        foreach (var query in queries)
        {
            var track = new Track
            {
                Artist = query.Artist,
                Title = query.Title,
                Album = query.Album ?? sourceTitle, // Use playlist name as album if not specified
                Length = query.Length,
                SourceTitle = sourceTitle,
                // Fix: Map metadata fields
                SpotifyTrackId = query.SpotifyTrackId,
                SpotifyAlbumId = query.SpotifyAlbumId,
                SpotifyArtistId = query.SpotifyArtistId,
                AlbumArtUrl = query.AlbumArtUrl,
                ArtistImageUrl = query.ArtistImageUrl,
                Genres = query.Genres,
                Popularity = query.Popularity,
                CanonicalDuration = query.CanonicalDuration,
                ReleaseDate = query.ReleaseDate
            };
            job.OriginalTracks.Add(track);
        }

        return job;
    }
}
