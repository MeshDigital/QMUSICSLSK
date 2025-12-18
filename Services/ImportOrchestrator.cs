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

    // Track current import to avoid duplicate event subscriptions in older logic
    // private bool _isHandlingImport; // REMOVED: Unused

    public ImportOrchestrator(
        ILogger<ImportOrchestrator> logger,
        ImportPreviewViewModel previewViewModel,
        DownloadManager downloadManager,
        INavigationService navigationService,
        Views.INotificationService notificationService)
    {
        _logger = logger;
        _previewViewModel = previewViewModel;
        _downloadManager = downloadManager;
        _navigationService = navigationService;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Import with preview screen - allows user to select tracks.
    /// </summary>
    public async Task StartImportWithPreviewAsync(IImportProvider provider, string input)
    {
        try
        {
            _logger.LogInformation("Starting import with preview from {Provider}: {Input}", 
                provider.Name, input);

            var result = await provider.ImportAsync(input);

            if (!result.Success)
            {
                _notificationService.Show(
                    "Import Failed",
                    result.ErrorMessage ?? "Unknown error occurred",
                    Views.NotificationType.Error,
                    TimeSpan.FromSeconds(5));
                return;
            }

            // Phase 7: Diff-based update (skip existing)
            var originalCount = result.Tracks.Count;
            result.Tracks = result.Tracks.Where(t => !_downloadManager.IsTrackAlreadyQueued(t.SpotifyTrackId, t.Artist, t.Title)).ToList();
            var skippedCount = originalCount - result.Tracks.Count;

            if (skippedCount > 0)
            {
                _logger.LogInformation("Skipped {Count} tracks already in library", skippedCount);
            }

            if (!result.Tracks.Any())
            {
                _notificationService.Show(
                    "No Tracks Found",
                    $"No tracks were found in the {provider.Name} source",
                    Views.NotificationType.Warning,
                    TimeSpan.FromSeconds(4));
                return;
            }

            // Initialize preview screen
            await _previewViewModel.InitializePreviewAsync(
                result.SourceTitle,
                provider.Name,
                result.Tracks);

            // Set up callbacks for this import session
            SetupPreviewCallbacks();

            // Navigate to preview
            _navigationService.NavigateTo("ImportPreview");

            _logger.LogInformation("Preview initialized with {Count} tracks from {Provider}",
                result.Tracks.Count, provider.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start import from {Provider}", provider.Name);
            _notificationService.Show(
                "Import Error",
                $"Failed to import: {ex.Message}",
                Views.NotificationType.Error,
                TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>
    /// Import all tracks directly without preview - "Add All" button flow.
    /// </summary>
    public async Task ImportAllDirectlyAsync(IImportProvider provider, string input)
    {
        try
        {
            _logger.LogInformation("Starting direct import (all tracks) from {Provider}: {Input}",
                provider.Name, input);

            var result = await provider.ImportAsync(input);

            if (!result.Success)
            {
                _notificationService.Show(
                    "Import Failed",
                    result.ErrorMessage ?? "Unknown error occurred",
                    Views.NotificationType.Error,
                    TimeSpan.FromSeconds(5));
                return;
            }

            // Phase 7: Diff-based update (skip existing)
            var originalCount = result.Tracks.Count;
            result.Tracks = result.Tracks.Where(t => !_downloadManager.IsTrackAlreadyQueued(t.SpotifyTrackId, t.Artist, t.Title)).ToList();
            var skippedCount = originalCount - result.Tracks.Count;

            if (skippedCount > 0)
            {
                _logger.LogInformation("Skipped {Count} tracks already in library during direct import", skippedCount);
            }

            if (!result.Tracks.Any())
            {
                _notificationService.Show(
                    "No Tracks Found",
                    $"No tracks were found in the {provider.Name} source",
                    Views.NotificationType.Warning,
                    TimeSpan.FromSeconds(4));
                return;
            }

            // Create job directly without preview
            var job = CreatePlaylistJob(result.SourceTitle, provider.Name, result.Tracks);

            // Queue for download
            await _downloadManager.QueueProject(job);

            // Show success notification
            _notificationService.Show(
                "Import Complete",
                $"✓ {result.Tracks.Count} tracks from '{result.SourceTitle}' added to library",
                Views.NotificationType.Success,
                TimeSpan.FromSeconds(4));

            // Navigate to library to show the new job
            _navigationService.NavigateTo("Library");

            _logger.LogInformation("Direct import completed: {Count} tracks from {Provider}",
                result.Tracks.Count, provider.Name);
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
                SourceTitle = sourceTitle
            };
            job.OriginalTracks.Add(track);
        }

        return job;
    }
}
