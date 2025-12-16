using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;
using SLSKDONET.Services;
using SLSKDONET.Views;
using Avalonia.Threading;

namespace SLSKDONET.ViewModels;

/// <summary>
/// Coordinator ViewModel for the Library page.
/// Delegates responsibilities to child ViewModels following Single Responsibility Principle.
/// </summary>
public class LibraryViewModel : INotifyPropertyChanged
{
    private readonly ILogger<LibraryViewModel> _logger;
    private readonly INavigationService _navigationService;
    private readonly ImportHistoryViewModel _importHistoryViewModel;
    private Views.MainViewModel? _mainViewModel;

    // Child ViewModels (Phase 0: ViewModel Refactoring)
    public Library.ProjectListViewModel Projects { get; }
    public Library.TrackListViewModel Tracks { get; }
    public Library.TrackOperationsViewModel Operations { get; }
    public Library.SmartPlaylistViewModel SmartPlaylists { get; }

    // Expose commonly used child properties for backward compatibility
    public PlaylistJob? SelectedProject 
    { 
        get => Projects.SelectedProject;
        set => Projects.SelectedProject = value;
    }
    
    public System.Collections.ObjectModel.ObservableCollection<PlaylistTrackViewModel> CurrentProjectTracks
    {
        get => Tracks.CurrentProjectTracks;
        set => Tracks.CurrentProjectTracks = value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // UI State Properties
    private bool _isEditMode;
    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (_isEditMode != value)
            {
                _isEditMode = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isActiveDownloadsVisible;
    public bool IsActiveDownloadsVisible
    {
        get => _isActiveDownloadsVisible;
        set
        {
            if (_isActiveDownloadsVisible != value)
            {
                _isActiveDownloadsVisible = value;
                OnPropertyChanged();
            }
        }
    }

    // Commands that delegate to child ViewModels or handle coordination
    public System.Windows.Input.ICommand ViewHistoryCommand { get; }
    public System.Windows.Input.ICommand ToggleEditModeCommand { get; }
    public System.Windows.Input.ICommand ToggleActiveDownloadsCommand { get; }

    public LibraryViewModel(
        ILogger<LibraryViewModel> logger,
        Library.ProjectListViewModel projects,
        Library.TrackListViewModel tracks,
        Library.TrackOperationsViewModel operations,
        Library.SmartPlaylistViewModel smartPlaylists,
        INavigationService navigationService,
        ImportHistoryViewModel importHistoryViewModel)
    {
        _logger = logger;
        _navigationService = navigationService;
        _importHistoryViewModel = importHistoryViewModel;
        
        // Assign child ViewModels
        Projects = projects;
        Tracks = tracks;
        Operations = operations;
        SmartPlaylists = smartPlaylists;
        
        // Initialize commands
        ViewHistoryCommand = new AsyncRelayCommand(ExecuteViewHistoryAsync);
        ToggleEditModeCommand = new RelayCommand<object>(_ => IsEditMode = !IsEditMode);
        ToggleActiveDownloadsCommand = new RelayCommand<object>(_ => IsActiveDownloadsVisible = !IsActiveDownloadsVisible);
        
        // Wire up events between child ViewModels
        Projects.ProjectSelected += OnProjectSelected;
        SmartPlaylists.SmartPlaylistSelected += OnSmartPlaylistSelected;
        
        _logger.LogInformation("LibraryViewModel initialized with child ViewModels");
    }

    /// <summary>
    /// Set MainViewModel after construction to avoid circular dependency.
    /// </summary>
    public void SetMainViewModel(Views.MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    /// <summary>
    /// Loads all projects from the database.
    /// Delegates to ProjectListViewModel.
    /// </summary>
    private async void OnProjectUpdated(object? sender, Guid jobId)
    {
        // Fetch the freshest data from DB
        var updatedJob = await _libraryService.FindPlaylistJobAsync(jobId);
        if (updatedJob == null) return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Update the existing object in the list so the UI binding triggers
            var existingJob = AllProjects.FirstOrDefault(j => j.Id == jobId);
            if (existingJob != null)
            {
                existingJob.SuccessfulCount = updatedJob.SuccessfulCount;
                existingJob.FailedCount = updatedJob.FailedCount;
                existingJob.MissingCount = updatedJob.MissingCount;
                
                // Force UI refresh if needed (ProgressPercentage relies on these)
                _logger.LogDebug("Refreshed UI counts for project {Title}: {Succ}/{Total}", existingJob.SourceTitle, existingJob.SuccessfulCount, existingJob.TotalTracks);
            }
        });
    }

    private async void OnProjectDeleted(object? sender, Guid projectId)
    {
        _logger.LogInformation("OnProjectDeleted event received for job {JobId}", projectId);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var jobToRemove = AllProjects.FirstOrDefault(p => p.Id == projectId);
            if (jobToRemove != null)
            {
                AllProjects.Remove(jobToRemove);

                // Auto-select next project if the deleted one was selected
                if (SelectedProject == jobToRemove)
                    SelectedProject = AllProjects.FirstOrDefault();
            }
        });
    }
    
    private async Task ExecuteRefreshAsync()
    {
        // Proceed without WPF MessageBox prompt in Avalonia stub
        
        _logger.LogInformation("Manual refresh requested - reloading projects and resolving file paths");
        
        // Remember the currently selected project ID
        var selectedProjectId = SelectedProject?.Id;
        
        // 1. Scan download folder and resolve missing file paths
        await ResolveMissingFilePathsAsync();
        
        // 2. Reload all projects from database
        await LoadProjectsAsync();
        
        // 3. Restore selection
        if (selectedProjectId.HasValue)
        {
            // Special case: restore "All Tracks" selection
            if (selectedProjectId == Guid.Empty)
            {
                SelectedProject = _allTracksJob;
            }
            else
            {
                var project = AllProjects.FirstOrDefault(p => p.Id == selectedProjectId.Value);
                if (project != null)
                {
                    SelectedProject = project;
                    _logger.LogInformation("Refreshing tracks for selected project: {Title}", project.SourceTitle);
                    await LoadProjectTracksAsync(project);
                }
            }
        }
        
        _logger.LogInformation("Manual refresh completed");
    }
    
    private async Task ResolveMissingFilePathsAsync()
    {
        try
        {
            _logger.LogInformation("Scanning download folder for missing file paths...");
            
            // Load ALL playlist tracks from database (not just global tracks in memory)
            var allPlaylistTracks = await _libraryService.GetAllPlaylistTracksAsync();
            
            // Get tracks that are marked as Downloaded but have no file path
            var tracksNeedingPaths = allPlaylistTracks
                .Where(t => t.Status == TrackStatus.Downloaded && string.IsNullOrEmpty(t.ResolvedFilePath))
                .ToList();
            
            if (!tracksNeedingPaths.Any())
            {
                _logger.LogInformation("No tracks need file path resolution");
                return;
            }
            
            _logger.LogInformation("Found {Count} completed tracks without file paths", tracksNeedingPaths.Count);
            
            // Get download directory from config (NOT hardcoded AppData path!)
            var downloadDir = _downloadManager.DownloadDirectory;
            
            if (string.IsNullOrEmpty(downloadDir))
            {
                _logger.LogWarning("Download directory not configured in settings");
                return;
            }
            
            if (!System.IO.Directory.Exists(downloadDir))
            {
                _logger.LogWarning("Download directory not found: {Dir}", downloadDir);
                return;
            }
            
            // Get all audio files in download directory
            var audioExtensions = new[] { ".mp3", ".flac", ".m4a", ".wav", ".ogg", ".wma" };
            var files = System.IO.Directory.GetFiles(downloadDir, "*.*", System.IO.SearchOption.AllDirectories)
                .Where(f => audioExtensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant()))
                .ToList();
            
            _logger.LogInformation("Found {Count} audio files in download directory", files.Count);
            
            // Log first 5 filenames for debugging
            if (files.Count > 0)
            {
                _logger.LogInformation("Sample filenames in download folder:");
                foreach (var file in files.Take(5))
                {
                    _logger.LogInformation("  - {FileName}", System.IO.Path.GetFileName(file));
                }
            }
            
            var updateTasks = new System.Collections.Concurrent.ConcurrentBag<Task>();
            int resolved = 0;
            
            // Offload the heavy matching logic to a background thread
            await Task.Run(() =>
            {
                foreach (var track in tracksNeedingPaths)
                {
                    // Try multiple matching strategies in order of confidence
                    string? matchingFile = null;
                    
                    // Strategy 1: Artist AND Title match (original logic)
                    matchingFile = files.FirstOrDefault(f =>
                    {
                        var fileName = System.IO.Path.GetFileNameWithoutExtension(f);
                        var artistMatch = !string.IsNullOrEmpty(track.Artist) && 
                                        fileName.Contains(track.Artist, StringComparison.OrdinalIgnoreCase);
                        var titleMatch = !string.IsNullOrEmpty(track.Title) && 
                                       fileName.Contains(track.Title, StringComparison.OrdinalIgnoreCase);
                        return artistMatch && titleMatch;
                    });
                    
                    // Strategy 2: Title-only match (fallback for different artist formats)
                    if (matchingFile == null && !string.IsNullOrEmpty(track.Title))
                    {
                        matchingFile = files.FirstOrDefault(f =>
                        {
                            var fileName = System.IO.Path.GetFileNameWithoutExtension(f);
                            return fileName.Contains(track.Title, StringComparison.OrdinalIgnoreCase);
                        });
                    }
                     
                    // Strategy 3: Normalized matching (remove special chars, underscores, etc.)
                    if (matchingFile == null && !string.IsNullOrEmpty(track.Title))
                    {
                        var normalizedTitle = NormalizeForMatching(track.Title);
                        matchingFile = files.FirstOrDefault(f =>
                        {
                            var fileName = System.IO.Path.GetFileNameWithoutExtension(f);
                            var normalizedFileName = NormalizeForMatching(fileName);
                            return normalizedFileName.Contains(normalizedTitle, StringComparison.OrdinalIgnoreCase);
                        });
                    }
                    
                    if (matchingFile != null)
                    {
                        track.ResolvedFilePath = matchingFile;
                        _logger.LogInformation("✅ Resolved path for {Artist} - {Title}: {Path}", 
                            track.Artist, track.Title, System.IO.Path.GetFileName(matchingFile));
                        
                        System.Threading.Interlocked.Increment(ref resolved);
                        
                        // Add to task list for concurrent execution
                        // We use a local capture to ensure thread safety if needed by the service method (though usually async methods are safe)
                        updateTasks.Add(_libraryService.UpdatePlaylistTrackAsync(track));
                    }
                    else
                    {
                         // Log why match failed (first 5 only to avoid spam)
                        if (resolved < 5)
                        {
                            _logger.LogInformation("❌ No match for '{Artist} - {Title}' (Status: {Status})", 
                                track.Artist, track.Title, track.Status);
                        }
                    }
                }
            });
            
            // Wait for all DB updates to complete
            if (!updateTasks.IsEmpty)
            {
                await Task.WhenAll(updateTasks);
            }

            _logger.LogInformation("Resolved {Resolved}/{Total} missing file paths", resolved, tracksNeedingPaths.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file path resolution");
        }
    }
    
    // Helper method to normalize strings for matching
    private string NormalizeForMatching(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        
        // Remove special characters, convert to lowercase, replace separators with spaces
        return input
            .Replace("_", " ")
            .Replace("-", " ")
            .Replace("&", "and")
            .Replace("(", "")
            .Replace(")", "")
            .Replace("[", "")
            .Replace("]", "")
            .Replace(".", " ")
            .ToLowerInvariant()
            .Trim();
    }
    private async void OnProjectAdded(object? sender, Guid jobId)
    {
        _logger.LogInformation("OnProjectAdded ENTRY for job {JobId}. Current project count: {ProjectCount}, Global track count: {TrackCount}", jobId, AllProjects.Count, _downloadManager.AllGlobalTracks.Count);
        
        // Fetch the job from the database
        var job = await _libraryService.FindPlaylistJobAsync(jobId);
        if (job == null)
        {
            _logger.LogWarning("Project {JobId} not found in database", jobId);
            return;
        }
        
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Check if the job already exists in the collection (race condition safety)
            if (AllProjects.Any(j => j.Id == job.Id))
            {
                _logger.LogWarning("Project {JobId} already exists in AllProjects, skipping add.", job.Id);
                return;
            }

            // Add the new project to the observable collection
            AllProjects.Add(job);

            // Auto-select the newly added project so it shows immediately
            SelectedProject = job;

            _logger.LogInformation("Project '{Title}' added to Library view.", job.SourceTitle);
        });
        _logger.LogInformation("OnProjectAdded EXIT for job {JobId}. New project count: {ProjectCount}", jobId, AllProjects.Count);
    }

    public void ReorderTrack(PlaylistTrackViewModel source, PlaylistTrackViewModel target)
    {
        if (source == null || target == null || source == target) return;
        if (CurrentProjectTracks == null) return;

        // Ensure we are operating on the same collection
        int oldIndex = CurrentProjectTracks.IndexOf(source);
        int newIndex = CurrentProjectTracks.IndexOf(target);

        if (oldIndex < 0 || newIndex < 0) return;

        // 1. Move in ObservableCollection (updates UI immediately)
        CurrentProjectTracks.Move(oldIndex, newIndex);

        // 2. Recalculate SortOrder for the entire collection (Dense Rank)
        // This ensures a clean 0..N sequence regardless of previous state
        for (int i = 0; i < CurrentProjectTracks.Count; i++)
        {
            var track = CurrentProjectTracks[i];
            track.SortOrder = i; 
            // Note: Setter updates Model.SortOrder automatically
        }

        // 3. Persist changes asynchronously
        if (SelectedProject != null)
        {
            try 
            {
                // Extract models to save
                var tracksToSave = CurrentProjectTracks.Select(vm => vm.Model).ToList();
                _ = _libraryService.SaveTrackOrderAsync(SelectedProject.Id, tracksToSave);
                _logger.LogInformation("Persisted new track order for playlist {Id}", SelectedProject.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist track order");
            }
        }
    }

    // Drag & Drop Callbacks
    public DraggingServiceDragEvent OnDragTrack => (DraggingServiceDragEventsArgs args) => {
        // Extract the dragged items (e.g., from the DataGrid selection)
        // Library API uses camelCase 'draggedControls'
        var draggedItems = args.draggedControls
            .Select(c => c.DataContext)
            .OfType<PlaylistTrackViewModel>()
            .ToList();

        if (draggedItems.Any())
        {
            DragContext.Current = draggedItems;
            _logger.LogInformation("Started dragging {Count} tracks", draggedItems.Count);
        }
    };

    public DraggingServiceDropEvent OnDropTrack => (DraggingServiceDropEventsArgs args) => {
        // Library API uses camelCase 'dropTarget'
        var targetPlaylist = args.dropTarget.DataContext as PlaylistJob;
        var droppedTracks = DragContext.Current as List<PlaylistTrackViewModel>;

        if (targetPlaylist != null && droppedTracks != null && droppedTracks.Any())
        {
            _logger.LogInformation("Dropping {Count} tracks onto playlist: {Playlist}", droppedTracks.Count, targetPlaylist.SourceTitle);
            
            // Execute add asynchronously
            Dispatcher.UIThread.Post(async () => {
                foreach (var track in droppedTracks)
                {
                    await ExecuteMoveToPlaylistAsync(targetPlaylist, track);
                }
            });
        }
    };

    private async Task ExecuteMoveToPlaylistAsync(PlaylistJob targetPlaylist, PlaylistTrackViewModel track)
    {
        // TODO: Implement actual move or copy logic here
        // For now we just log it as the user plan didn't specify the exact move logic implementation details
        // beyond "AddToPlaylist".
        // Assuming we want to copy/move the track to the new project.
        _logger.LogInformation("Moving track {Track} to {Playlist}", track.Title, targetPlaylist.SourceTitle);
        // This logic would need to be fleshed out to actually call LibraryService to link the track to the new job.
    }

    private void ExecuteHardRetry(PlaylistTrackViewModel? vm)
    {
        if (vm == null) return;

        _logger.LogInformation("Hard Retry requested for {Artist} - {Title}", vm.Artist, vm.Title);
        _downloadManager.HardRetryTrack(vm.GlobalId);
    }

    private void ExecutePause(PlaylistTrackViewModel? vm)
    {
        if (vm == null) return;

        _logger.LogInformation("Pause requested for {Artist} - {Title}", vm.Artist, vm.Title);
        _downloadManager.PauseTrack(vm.GlobalId);
    }

    private void ExecuteResume(PlaylistTrackViewModel? vm)
    {
        if (vm == null) return;

        _logger.LogInformation("Resume requested for {Artist} - {Title}", vm.Artist, vm.Title);
        _downloadManager.ResumeTrack(vm.GlobalId);
    }

    private void ExecuteCancel(PlaylistTrackViewModel? vm)
    {
        if (vm == null) return;

        _logger.LogInformation("Cancel requested for {Artist} - {Title}", vm.Artist, vm.Title);
        _downloadManager.CancelTrack(vm.GlobalId);
    }

        private void ExecutePlayTrack(PlaylistTrackViewModel? vm)
        {
            if (vm == null)
            {
                _logger.LogWarning("PlayTrack called with null track");
                return;
            }

            // Check if track has been downloaded
            if (string.IsNullOrEmpty(vm.Model?.ResolvedFilePath))
            {
                _logger.LogWarning("Cannot play track {Artist} - {Title}: No file path (track not downloaded)", vm.Artist, vm.Title);
                
                // Show user-friendly message
                if (_mainViewModel != null)
                {
                    _mainViewModel.StatusText = $"Cannot play '{vm.Title}' - track not downloaded yet";
                }
                return;
            }

            // Check if file actually exists
            if (!System.IO.File.Exists(vm.Model.ResolvedFilePath))
            {
                _logger.LogWarning("Cannot play track {Artist} - {Title}: File not found at {Path}", 
                    vm.Artist, vm.Title, vm.Model.ResolvedFilePath);
                
                if (_mainViewModel != null)
                {
                    _mainViewModel.StatusText = $"Cannot play '{vm.Title}' - file not found";
                }
                return;
            }

            // Play the track
            _logger.LogInformation("Playing track: {Title} by {Artist} from {Path}", 
                vm.Title, vm.Artist, vm.Model.ResolvedFilePath);
            
            _playerViewModel.PlayTrack(vm.Model.ResolvedFilePath, vm.Title ?? "Unknown", vm.Artist ?? "Unknown Artist");
            
            // Ensure the player sidebar is visible
            if (_mainViewModel != null)
            {
                _mainViewModel.IsPlayerSidebarVisible = true;
                _mainViewModel.StatusText = $"Now playing: {vm.Artist} - {vm.Title}";
            }
        }

    private void ExecuteDownloadAlbum(PlaylistTrackViewModel? track)
    {
        if (track == null || string.IsNullOrWhiteSpace(track.Album))
        {
            _logger.LogWarning("Cannot download album: track or album name is null");
            return;
        }

        // Find all tracks with the same album name in the current view
        var albumTracks = CurrentProjectTracks
            .Where(t => !string.IsNullOrWhiteSpace(t.Album) && 
                       t.Album.Equals(track.Album, StringComparison.OrdinalIgnoreCase) &&
                       t.State != PlaylistTrackState.Completed)
            .ToList();

        if (!albumTracks.Any())
        {
            _logger.LogInformation("No tracks to download for album '{Album}' (all completed or no matches)", track.Album);
            if (_mainViewModel != null)
            {
                _mainViewModel.StatusText = $"Album '{track.Album}' is already downloaded";
            }
            return;
        }

        _logger.LogInformation("Downloading {Count} tracks from album '{Album}'", albumTracks.Count, track.Album);
        
        // Queue all tracks for download
        var trackModels = albumTracks.Select(t => t.Model).ToList();
        _downloadManager.QueueTracks(trackModels);
        
        if (_mainViewModel != null)
        {
            _mainViewModel.StatusText = $"Queued {albumTracks.Count} tracks from '{track.Album}' for download";
        }
    }

    private void ExecutePauseProject(PlaylistJob? job)
    {
        // Operate on currently visible tracks
        var tracks = CurrentProjectTracks.ToList();
        _logger.LogInformation("Pausing all {Count} tracks in current view", tracks.Count);
        foreach (var t in tracks)
        {
            if (t.CanPause) _downloadManager.PauseTrack(t.GlobalId);
        }
    }

    private void ExecuteRetryOfflineTracks()
    {
        // Find all tracks that failed due to user offline
        var offlineTracks = _downloadManager.AllGlobalTracks
            .Where(t => t.State == PlaylistTrackState.Failed || t.State == PlaylistTrackState.Cancelled)
            .ToList();

        if (!offlineTracks.Any())
        {
            _logger.LogInformation("No offline/failed tracks to retry");
            if (_mainViewModel != null)
            {
                _mainViewModel.StatusText = "No failed tracks to retry";
            }
            return;
        }

        _logger.LogInformation("Retrying {Count} failed/offline tracks", offlineTracks.Count);
        
        // Requeue all failed tracks
        foreach (var track in offlineTracks)
        {
            _downloadManager.HardRetryTrack(track.GlobalId);
        }
        
        if (_mainViewModel != null)
        {
            _mainViewModel.StatusText = $"Retrying {offlineTracks.Count} failed tracks";
        }
    }

    private void ExecuteToggleProjectDownload(PlaylistJob? job)
    {
        if (job == null) return;

        // Check if any tracks from this project are currently downloading
        var projectTracks = _downloadManager.AllGlobalTracks
            .Where(t => t.Model?.PlaylistId == job.Id)
            .ToList();
            
        var hasActiveDownloads = projectTracks.Any(t => 
            t.State == PlaylistTrackState.Downloading || 
            t.State == PlaylistTrackState.Searching);

        if (hasActiveDownloads)
        {
            // Pause active downloads
            ExecutePauseProject(job);
        }
        else
        {
            // Resume/start downloads
            ExecuteResumeProject(job);
        }
    }

    public async void AddToPlaylist(PlaylistJob targetPlaylist, PlaylistTrackViewModel sourceTrack)
    {
        if (targetPlaylist == null || sourceTrack == null || targetPlaylist.Id == Guid.Empty) return;

        _logger.LogInformation("Adding track {Track} to playlist {Playlist}", sourceTrack.Title, targetPlaylist.SourceTitle);
        
        // Get the actual file path - check both Model and live DownloadManager
        string resolvedPath = sourceTrack.Model?.ResolvedFilePath ?? "";
        
        // If Model doesn't have the path, check the live DownloadManager
        if (string.IsNullOrEmpty(resolvedPath))
        {
            var liveTrack = _downloadManager.AllGlobalTracks
                .FirstOrDefault(t => t.GlobalId == sourceTrack.GlobalId);
            
            if (liveTrack != null)
            {
                resolvedPath = liveTrack.Model?.ResolvedFilePath ?? "";
            }
        }
        
        _logger.LogInformation("Source track state: {State}, ResolvedFilePath: {Path}", 
            sourceTrack.State, 
            string.IsNullOrEmpty(resolvedPath) ? "<empty>" : resolvedPath);

        try
        {
            // 1. Create new PlaylistTrack for the target playlist
            var newTrack = new PlaylistTrack
            {
                Id = Guid.NewGuid(),
                PlaylistId = targetPlaylist.Id,
                Artist = sourceTrack.Artist ?? "",
                Title = sourceTrack.Title ?? "",
                Album = sourceTrack.Model?.Album ?? "",
                SortOrder = targetPlaylist.TotalTracks + 1,
                Status = sourceTrack.State == PlaylistTrackState.Completed ? TrackStatus.Downloaded : TrackStatus.Missing,
                ResolvedFilePath = resolvedPath,  // Use the resolved path we found
                TrackUniqueHash = sourceTrack.GlobalId.ToString()
            };

            _logger.LogInformation("New track created with ResolvedFilePath: {Path}, Status: {Status}", 
                string.IsNullOrEmpty(newTrack.ResolvedFilePath) ? "<empty>" : newTrack.ResolvedFilePath, 
                newTrack.Status);

            // 2. Persist to Database
            await _libraryService.SavePlaylistTrackAsync(newTrack);

            // 3. Log Activity
            await _libraryService.LogPlaylistActivityAsync(targetPlaylist.Id, "Add", $"Added track '{sourceTrack.Artist} - {sourceTrack.Title}'");

            // 4. Update Target Playlist Counts/Metadata
            targetPlaylist.TotalTracks++;
            if (sourceTrack.State == PlaylistTrackState.Completed)
            {
                targetPlaylist.SuccessfulCount++;
            }

            _logger.LogInformation("Successfully added track to playlist {Playlist}", targetPlaylist.SourceTitle);
            
            // 5. Reload the playlist view if it's currently selected (AFTER database operations complete)
            if (SelectedProject == targetPlaylist)
            {
                // Reload tracks from database to show the newly added track
                await LoadProjectTracksAsync(targetPlaylist);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add track to playlist");
        }
    }


    private void ExecuteResumeProject(PlaylistJob? job)
    {
        // Use local snapshot to avoid collection modified exception
        var tracks = CurrentProjectTracks?.ToList();
        if (tracks == null || !tracks.Any()) return;

        _logger.LogInformation("Start All: Processing {Count} tracks in current view", tracks.Count);
        
        int resumed = 0, requeued = 0, alreadyActive = 0;
        
        foreach (var t in tracks)
        {
            // 1. Check if track is actually known to the DownloadManager
            var liveTrack = _downloadManager.AllGlobalTracks.FirstOrDefault(x => x.GlobalId == t.GlobalId);
            
            if (liveTrack == null)
            {
                // ORPHAN DETECTED: Track exists in UI but not in Manager
                // Re-queue it unless it's genuinely completed/downloading (unlikely for orphan)
                if (t.State != PlaylistTrackState.Completed && t.State != PlaylistTrackState.Downloading)
                {
                    _downloadManager.QueueTracks(new List<PlaylistTrack> { t.Model });
                    requeued++;
                }
                continue;
            }
            
            // 2. Existing Track Logic
            if (liveTrack.State == PlaylistTrackState.Paused || liveTrack.State == PlaylistTrackState.Failed)
            {
                _downloadManager.ResumeTrack(t.GlobalId);
                resumed++;
            }
            else if (liveTrack.State == PlaylistTrackState.Pending)
            {
                // Already pending in manager. 
                // We'll leave it to the loop, but we won't skip logic if we needed to do something else.
            }
            else if (liveTrack.IsActive)
            {
                alreadyActive++;
            }
        }
        
        _logger.LogInformation("Start All Summary: Resumed {Resumed}, Re-Queued {Requeued}, AlreadyActive {Active}", resumed, requeued, alreadyActive);
    }
        


    private async Task ExecuteAddPlaylistAsync()
    {
        try
        {
            // Simple auto-naming strategy as default
            int count = AllProjects.Count(p => p.SourceTitle.StartsWith("New Playlist"));
            string defaultTitle = count == 0 ? "New Playlist" : $"New Playlist {count + 1}";

            string? title = _userInputService.GetInput("Enter a name for the new playlist:", "New Playlist", defaultTitle);

            if (string.IsNullOrWhiteSpace(title)) return; // User cancelled

            _logger.LogInformation("Creating new playlist: {Title}", title);
            var job = await _libraryService.CreateEmptyPlaylistAsync(title);
            
            // UI Update
            AllProjects.Add(job);
            SelectedProject = job;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create new playlist");
        }
    }

    private async Task ExecuteDeleteProjectAsync(PlaylistJob? job)
    {
        // Fallback to selected project if null (e.g. called from header button without parameter)
        var targetJob = job ?? SelectedProject;
        
        if (targetJob == null || targetJob == _allTracksJob) return;

        _logger.LogInformation("Soft-deleting project: {Title} ({Id})", targetJob.SourceTitle, targetJob.Id);

        try
        {
            // Soft-delete via database service
            await _libraryService.DeletePlaylistJobAsync(targetJob.Id);
            // The UI update will now be handled by the OnProjectDeleted event handler.
            _logger.LogInformation("Deletion request for project {Title} processed. Event will trigger UI update.", targetJob.SourceTitle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete project {Id}", targetJob.Id);
        }
    }

    private async void ExecuteRemoveTrack(PlaylistTrackViewModel? track)
    {
        if (track == null || SelectedProject == null || SelectedProject.Id == Guid.Empty) return;

        // User didn't strictly say it's restricted to Edit Mode, but it's consistent.
        if (!IsEditMode) 
        {
             // Optional: Show message "Enable Edit Mode to remove tracks"
             return; 
        }

        // Proceed without modal prompt in Avalonia migration; consider adding custom dialog if needed.
        var confirmed = true;
        if (!confirmed) return;

        try
        {
            _logger.LogInformation("Removing track {Track} from playlist {Playlist}", track.Title, SelectedProject.SourceTitle);
            
            // 1. Remove from DB
            await _libraryService.DeletePlaylistTrackAsync(track.Id);
            
            // 2. Log Activity
            await _libraryService.LogPlaylistActivityAsync(SelectedProject.Id, "Remove", $"Removed track '{track.Artist} - {track.Title}'");

            // 3. Update In-Memory list
            if (CurrentProjectTracks.Contains(track))
            {
                CurrentProjectTracks.Remove(track);
            }
            
            // 4. Update counts
            SelectedProject.TotalTracks--;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove track");
        }
    }

    private async Task ExecuteViewHistoryAsync()
    {
        if (SelectedProject == null || SelectedProject.Id == Guid.Empty) return;

        _logger.LogInformation("Navigating to history for job: {Title}", SelectedProject.SourceTitle);
        
        // 1. Pre-select in target VM
        await _importHistoryViewModel.SelectJob(SelectedProject.Id);
        
        // 2. Navigate
        _navigationService.NavigateTo("ImportHistory");
    }

    private async Task LoadProjectTracksAsync(PlaylistJob job)
    {
        try
        {
            _logger.LogInformation("Loading tracks for project: {Name}", job.SourceTitle);
            var tracks = new ObservableCollection<PlaylistTrackViewModel>();

            if (job.Id == Guid.Empty || job == _allTracksJob) // All Tracks
            {
                 var globalCount = _downloadManager.AllGlobalTracks.Count;
                 _logger.LogInformation("All Tracks mode: GlobalTracks has {Count} items.", globalCount);
                 
                 // Load ALL global tracks, Sorted by Active first
                 // Run on background thread to avoid UI freeze with large libraries
                 var all = await Task.Run(() => 
                 {
                     return _downloadManager.AllGlobalTracks
                         .OrderByDescending(t => t.IsActive)
                         .ThenBy(t => t.Artist)
                         .ToList();
                 });
                 
                 _logger.LogInformation("All Tracks mode: Adding {Count} tracks to view.", all.Count);
                 foreach (var t in all) tracks.Add(t);
            }
            else
            {
                // IMPORTANT: Reload from database to get fresh data, not cached job.PlaylistTracks
                var freshTracks = await _libraryService.LoadPlaylistTracksAsync(job.Id);
                
                foreach (var track in freshTracks.OrderBy(t => t.TrackNumber))
                {
                    var vm = new PlaylistTrackViewModel(track);

                    // Sync with live DownloadManager state for real-time progress
                    var liveTrack = _downloadManager.AllGlobalTracks
                        .FirstOrDefault(t => t.GlobalId == track.TrackUniqueHash);

                    if (liveTrack != null)
                    {
                        vm.State = liveTrack.State;
                        vm.Progress = liveTrack.Progress;
                        vm.CurrentSpeed = liveTrack.CurrentSpeed;
                        vm.ErrorMessage = liveTrack.ErrorMessage;
                        
                        // CRITICAL FIX: If database doesn't have ResolvedFilePath, get it from live track
                        if (string.IsNullOrEmpty(vm.Model.ResolvedFilePath) && 
                            !string.IsNullOrEmpty(liveTrack.Model?.ResolvedFilePath))
                        {
                            vm.Model.ResolvedFilePath = liveTrack.Model.ResolvedFilePath;
                            
                            // Persist this path back to database for future loads
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await _libraryService.UpdatePlaylistTrackAsync(vm.Model);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to persist ResolvedFilePath for track {Id}", vm.Model.Id);
                                }
                            });
                        }
                    }

                    tracks.Add(vm);
                    
                    // Log if track has resolved path (for debugging)
                    if (!string.IsNullOrEmpty(track.ResolvedFilePath))
                    {
                        _logger.LogDebug("Track {Artist} - {Title} loaded with path: {Path}", 
                            track.Artist, track.Title, track.ResolvedFilePath);
                    }
                }
            }

            // Update UI - Ensure we are on UI thread after await
            Dispatcher.UIThread.Post(() =>
            {
                CurrentProjectTracks = tracks;
                RefreshFilteredTracks(); // Update FilteredTracks so DataGrid displays the tracks
                _logger.LogInformation("Loaded {Count} tracks for project {Title}", tracks.Count, job.SourceTitle);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tracks for project {Id}", job.Id);
        }
    }

>>>>>>> Stashed changes
    public async Task LoadProjectsAsync()
    {
        await Projects.LoadProjectsAsync();
    }

    /// <summary>
    /// Handles project selection event from ProjectListViewModel.
    /// Coordinates loading tracks in TrackListViewModel.
    /// </summary>
    /// <summary>
    /// Handles project selection event from ProjectListViewModel.
    /// Coordinates loading tracks in TrackListViewModel.
    /// </summary>
    private async void OnProjectSelected(object? sender, PlaylistJob? project)
    {
        if (project == null) return;

        _logger.LogInformation("Project selected: {Title}", project.SourceTitle);
        
        // Deselect smart playlist without triggering its "Load" logic if possible.
        // But since we can't easily suppress events without a flag, we just check properties.
        if (SmartPlaylists.SelectedSmartPlaylist != null)
        {
            SmartPlaylists.SelectedSmartPlaylist = null;
        }
        
        // Load tracks for selected project
        await Tracks.LoadProjectTracksAsync(project);
    }

    /// <summary>
    /// Handles smart playlist selection event from SmartPlaylistViewModel.
    /// Coordinates updating track list.
    /// </summary>
    private void OnSmartPlaylistSelected(object? sender, Library.SmartPlaylist? playlist)
    {
        if (playlist == null) return;

        _logger.LogInformation("Smart playlist selected: {Name}", playlist.Name);
        
        // Deselect project
        if (Projects.SelectedProject != null)
        {
            Projects.SelectedProject = null;
        }
        
        // Refresh smart playlist tracks
        var tracks = SmartPlaylists.RefreshSmartPlaylist(playlist);
        Tracks.CurrentProjectTracks = tracks;
    }

    /// <summary>
    /// Opens the import history view.
    /// </summary>
    private async Task ExecuteViewHistoryAsync()
    {
        try
        {
            _logger.LogInformation("Opening import history");
            _navigationService.NavigateTo("ImportHistory");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open import history");
        }
    }

    /// <summary>
    /// Adds selected tracks to a playlist.
    /// Used by drag-drop operations in LibraryPage.
    /// </summary>
    public void AddToPlaylist(PlaylistJob sourcePlaylist, PlaylistTrackViewModel track)
    {
        _logger.LogInformation("AddToPlaylist called: moving track {Title} from playlist {Source}", 
            track?.Title, sourcePlaylist?.SourceTitle);
        // TODO: Implement playlist track addition logic
        // This would need to be coordinated with child ViewModels
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
