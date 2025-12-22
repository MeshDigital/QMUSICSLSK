using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;
using SLSKDONET.Services;
using SLSKDONET.Views;

namespace SLSKDONET.ViewModels.Library;

/// <summary>
/// Manages track-level operations like play, pause, resume, cancel, retry, etc.
/// Handles download operations and playback integration.
/// </summary>
public class TrackOperationsViewModel : INotifyPropertyChanged
{
    private readonly ILogger<TrackOperationsViewModel> _logger;
    private readonly DownloadManager _downloadManager;
    private MainViewModel? _mainViewModel; // Injected post-construction
    private readonly PlayerViewModel _playerViewModel;
    private readonly IFileInteractionService _fileInteractionService;

    public event PropertyChangedEventHandler? PropertyChanged;

    // Commands
    public System.Windows.Input.ICommand PlayTrackCommand { get; }
    public System.Windows.Input.ICommand HardRetryCommand { get; }
    public System.Windows.Input.ICommand PauseCommand { get; }
    public System.Windows.Input.ICommand ResumeCommand { get; }
    public System.Windows.Input.ICommand CancelCommand { get; }
    public System.Windows.Input.ICommand DownloadAlbumCommand { get; }
    public System.Windows.Input.ICommand RemoveTrackCommand { get; }
    public System.Windows.Input.ICommand RetryOfflineTracksCommand { get; }
    public System.Windows.Input.ICommand OpenFolderCommand { get; }

    public TrackOperationsViewModel(
        ILogger<TrackOperationsViewModel> logger,
        DownloadManager downloadManager,
        PlayerViewModel playerViewModel,
        IFileInteractionService fileInteractionService)
    {
        _logger = logger;
        _downloadManager = downloadManager;
        _playerViewModel = playerViewModel;
        _fileInteractionService = fileInteractionService;

        // Initialize commands
        PlayTrackCommand = new RelayCommand<PlaylistTrackViewModel>(ExecutePlayTrack);
        HardRetryCommand = new RelayCommand<PlaylistTrackViewModel>(ExecuteHardRetry);
        PauseCommand = new RelayCommand<PlaylistTrackViewModel>(ExecutePause);
        ResumeCommand = new RelayCommand<PlaylistTrackViewModel>(ExecuteResume);
        CancelCommand = new RelayCommand<PlaylistTrackViewModel>(ExecuteCancel);
        DownloadAlbumCommand = new AsyncRelayCommand<PlaylistTrackViewModel>(ExecuteDownloadAlbum);
        RemoveTrackCommand = new AsyncRelayCommand<PlaylistTrackViewModel>(ExecuteRemoveTrack);
        RetryOfflineTracksCommand = new AsyncRelayCommand(ExecuteRetryOfflineTracks);
        OpenFolderCommand = new RelayCommand<PlaylistTrackViewModel>(ExecuteOpenFolder);
    }

    public void SetMainViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    private void ExecutePlayTrack(PlaylistTrackViewModel? track)
    {
        if (track == null) return;

        var filePath = track.Model?.ResolvedFilePath;
        if (string.IsNullOrEmpty(filePath))
        {
            _logger.LogWarning("Cannot play track - no resolved file path");
            return;
        }

        if (!System.IO.File.Exists(filePath))
        {
            _logger.LogWarning("Cannot play track - file does not exist: {Path}", filePath);
            return;
        }

        _logger.LogInformation("Playing track: {Artist} - {Title}", track.Artist, track.Title);

        // Clear queue and add this track
        _playerViewModel.ClearQueue();
        _playerViewModel.AddToQueue(track);
    }

    private void ExecuteHardRetry(PlaylistTrackViewModel? track)
    {
        if (track == null) return;
        _logger.LogInformation("Hard retry for track: {Title}", track.Title);
        _downloadManager.HardRetryTrack(track.GlobalId);
    }

    private async void ExecutePause(PlaylistTrackViewModel? track)
    {
        if (track == null) return;
        _logger.LogInformation("Pausing track: {Title}", track.Title);
        await _downloadManager.PauseTrackAsync(track.GlobalId);
    }

    private async void ExecuteResume(PlaylistTrackViewModel? track)
    {
        if (track == null) return;
        _logger.LogInformation("Resuming track: {Title}", track.Title);
        await _downloadManager.ResumeTrackAsync(track.GlobalId);
    }

    private void ExecuteCancel(PlaylistTrackViewModel? track)
    {
        if (track == null) return;
        _logger.LogInformation("Cancelling track: {Title}", track.Title);
        _downloadManager.CancelTrack(track.GlobalId);
    }

    private async Task ExecuteDownloadAlbum(PlaylistTrackViewModel? track)
    {
        if (track == null) return;

        _logger.LogInformation("Download album command for track: {Artist} - {Album}", 
            track.Artist, track.Album);

        // TODO: Implement album download logic
        // This would need to:
        // 1. Find all tracks with same album
        // 2. Queue them for download
        // 3. Show progress
        await Task.CompletedTask;
    }

    private async Task ExecuteRemoveTrack(PlaylistTrackViewModel? track)
    {
        if (track == null) return;

        try
        {
            _logger.LogInformation("Removing track: {Title}", track.Title);
            
            // Remove from download manager
            await _downloadManager.DeleteTrackFromDiskAndHistoryAsync(track.GlobalId);
            
            _logger.LogInformation("Track removed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove track");
        }
    }

    private async Task ExecuteRetryOfflineTracks()
    {
        try
        {
            _logger.LogInformation("Retrying all offline tracks");
            
            if (_mainViewModel == null) return;
            
            var offlineTracks = _mainViewModel.AllGlobalTracks
                .Where(t => t.State == PlaylistTrackState.Failed)
                .ToList();

            _logger.LogInformation("Found {Count} failed tracks to retry", offlineTracks.Count);

            foreach (var track in offlineTracks)
            {
                _downloadManager.HardRetryTrack(track.GlobalId);
                await Task.Delay(100); // Small delay to avoid overwhelming the system
            }

            _logger.LogInformation("Retry offline tracks completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry offline tracks");
        }
    }

    private void ExecuteOpenFolder(PlaylistTrackViewModel? track)
    {
        if (track == null) return;

        var filePath = track.Model?.ResolvedFilePath;
        if (string.IsNullOrEmpty(filePath))
        {
            _logger.LogWarning("Cannot open folder - no resolved file path");
            return;
        }

        try
        {
            var directory = System.IO.Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && System.IO.Directory.Exists(directory))
            {
                // Open folder in file explorer
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = directory,
                    UseShellExecute = true,
                    Verb = "open"
                });
                _logger.LogInformation("Opened folder: {Directory}", directory);
              }
            else
            {
                _logger.LogWarning("Directory does not exist: {Directory}", directory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open folder");
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
