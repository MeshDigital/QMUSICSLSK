using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;
using SLSKDONET.Services;
using SLSKDONET.Views;

namespace SLSKDONET.ViewModels;

public class LibraryViewModel : INotifyPropertyChanged
{
    private readonly ILogger<LibraryViewModel> _logger;
    private readonly DownloadManager _downloadManager;
    private readonly ILibraryService _libraryService;
    
    // Master/Detail pattern properties
    private ObservableCollection<PlaylistJob> _allProjects = new();
    private PlaylistJob? _selectedProject;
    private ObservableCollection<PlaylistTrackViewModel> _currentProjectTracks = new();
    
    public CollectionViewSource ActiveTracksInit { get; } = new();
    public ICollectionView ActiveTracksView => ActiveTracksInit.View;

    public CollectionViewSource WarehouseTracksInit { get; } = new();
    public ICollectionView WarehouseTracksView => WarehouseTracksInit.View;

    public ICommand HardRetryCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand OpenProjectCommand { get; }
    
    // Master List: All import jobs/projects
    public ObservableCollection<PlaylistJob> AllProjects
    {
        get => _allProjects;
        set { _allProjects = value; OnPropertyChanged(); }
    }
    
    // Selected project
    public PlaylistJob? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (_selectedProject != value)
            {
                _selectedProject = value;
                OnPropertyChanged();
                if (value != null)
                    LoadProjectTracks(value);
            }
        }
    }
    
    // Detail List: Tracks for selected project
    public ObservableCollection<PlaylistTrackViewModel> CurrentProjectTracks
    {
        get => _currentProjectTracks;
        set { _currentProjectTracks = value; OnPropertyChanged(); }
    }

    private bool _isGridView;
    public bool IsGridView
    {
        get => _isGridView;
        set
        {
            if (_isGridView != value)
            {
                _isGridView = value;
                OnPropertyChanged();
            }
        }
    }

    public LibraryViewModel(ILogger<LibraryViewModel> logger, DownloadManager downloadManager, ILibraryService libraryService)
    {
        _logger = logger;
        _downloadManager = downloadManager;
        _libraryService = libraryService;

        // Initialize Active View
        ActiveTracksInit.Source = _downloadManager.AllGlobalTracks;
        ActiveTracksInit.IsLiveFilteringRequested = true;
        ActiveTracksInit.LiveFilteringProperties.Add("State");
        ActiveTracksInit.IsLiveSortingRequested = true;
        ActiveTracksInit.LiveSortingProperties.Add("Progress");
        ActiveTracksInit.Filter += ActiveTracks_Filter;
        ActiveTracksInit.SortDescriptions.Add(new SortDescription("State", ListSortDirection.Ascending));

        // Initialize Warehouse View
        WarehouseTracksInit.Source = _downloadManager.AllGlobalTracks;
        WarehouseTracksInit.IsLiveFilteringRequested = true;
        WarehouseTracksInit.LiveFilteringProperties.Add("State");
        WarehouseTracksInit.IsLiveSortingRequested = true; // Optional for warehouse
        WarehouseTracksInit.LiveSortingProperties.Add("Artist");
        WarehouseTracksInit.Filter += WarehouseTracks_Filter;
        WarehouseTracksInit.SortDescriptions.Add(new SortDescription("SortOrder", ListSortDirection.Ascending)); 
        
        // Commands
        HardRetryCommand = new RelayCommand<PlaylistTrackViewModel>(ExecuteHardRetry);
        PauseCommand = new RelayCommand<PlaylistTrackViewModel>(ExecutePause);
        ResumeCommand = new RelayCommand<PlaylistTrackViewModel>(ExecuteResume);
        CancelCommand = new RelayCommand<PlaylistTrackViewModel>(ExecuteCancel);
        OpenProjectCommand = new RelayCommand<PlaylistJob>(project => SelectedProject = project);
        
        // Subscribe to global track updates for live progress
        _downloadManager.TrackUpdated += OnGlobalTrackUpdated;
        
        // Load projects asynchronously
        _ = LoadProjectsAsync();
    }

    public void ReorderTrack(PlaylistTrackViewModel source, PlaylistTrackViewModel target)
    {
        if (source == null || target == null || source == target) return;

        // Simple implementation: Swap SortOrder
        // Better implementation: Insert
        // Renumbering everything is safest for consistency
        
        // Find current indices in the underlying collection? 
        // We really want to change SortOrder values.
        
        // Let's adopt a "dense rank" approach.
        // First, ensure everyone has a SortOrder. if 0, assign based on current index.
        
        var allTracks = _downloadManager.AllGlobalTracks; // This is the source
        // But we are only reordering within "Warehouse" view ideally. 
        // Mixing active/warehouse reordering is tricky.
        // Assuming we drag pending items.
        
        int oldIndex = source.SortOrder;
        int newIndex = target.SortOrder;
        
        if (oldIndex == newIndex) return;
        
        // Shift items
        foreach (var track in allTracks)
        {
            if (oldIndex < newIndex)
            {
                // Moving down: shift items between old and new UP (-1)
                if (track.SortOrder > oldIndex && track.SortOrder <= newIndex)
                {
                    track.SortOrder--;
                }
            }
            else
            {
                // Moving up: shift items between new and old DOWN (+1)
                if (track.SortOrder >= newIndex && track.SortOrder < oldIndex)
                {
                    track.SortOrder++;
                }
            }
        }
        
        source.SortOrder = newIndex;
        // Verify uniqueness? If we started with unique 0..N, we end with unique 0..N
    }

    private void ActiveTracks_Filter(object sender, FilterEventArgs e)
    {
        if (e.Item is PlaylistTrackViewModel vm)
        {
            // Active: Searching, Downloading, Queued
            e.Accepted = vm.State == PlaylistTrackState.Searching ||
                         vm.State == PlaylistTrackState.Downloading ||
                         vm.State == PlaylistTrackState.Queued;
        }
    }

    private void WarehouseTracks_Filter(object sender, FilterEventArgs e)
    {
        if (e.Item is PlaylistTrackViewModel vm)
        {
            // Warehouse: Pending, Completed, Failed, Cancelled
            // Essentially !Active
            e.Accepted = vm.State == PlaylistTrackState.Pending ||
                         vm.State == PlaylistTrackState.Completed ||
                         vm.State == PlaylistTrackState.Failed ||
                         vm.State == PlaylistTrackState.Cancelled;
        }
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

    private async void LoadProjectTracks(PlaylistJob job)
    {
        try
        {
            _logger.LogInformation("Loading tracks for project: {Name}", job.SourceTitle);
            
            // Load full track data from database
            var playlistTracks = await _libraryService.LoadPlaylistTracksAsync(job.Id);
            
            var tracks = new ObservableCollection<PlaylistTrackViewModel>();
            
            foreach (var track in playlistTracks)
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
                }
                
                tracks.Add(vm);
            }
            
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                CurrentProjectTracks = tracks;
            });
            
            _logger.LogInformation("Loaded {Count} tracks for project {Title}", tracks.Count, job.SourceTitle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tracks for project {Id}", job.Id);
        }
    }
    
    private async Task LoadProjectsAsync()
    {
        try
        {
            _logger.LogInformation("Loading all playlist jobs from database");
            
            var jobs = await _libraryService.LoadAllPlaylistJobsAsync();
            
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                AllProjects.Clear();
                
                // Add jobs ordered by creation date (newest first)
                foreach (var job in jobs.OrderByDescending(j => j.CreatedAt))
                {
                    AllProjects.Add(job);
                }
                
                // Auto-select first project
                if (AllProjects.Count > 0)
                    SelectedProject = AllProjects[0];
            });
            
            _logger.LogInformation("Loaded {Count} playlist jobs for Library", AllProjects.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load playlist jobs");
        }
    }
    
    private void OnGlobalTrackUpdated(object? sender, PlaylistTrackViewModel updatedTrack)
    {
        if (CurrentProjectTracks == null) return;
        
        // Use Dispatcher for UI thread safety
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var localTrack = CurrentProjectTracks
                .FirstOrDefault(t => t.GlobalId == updatedTrack.GlobalId);
            
            if (localTrack != null)
            {
                localTrack.State = updatedTrack.State;
                localTrack.Progress = updatedTrack.Progress;
                localTrack.CurrentSpeed = updatedTrack.CurrentSpeed;
                localTrack.ErrorMessage = updatedTrack.ErrorMessage;
            }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
