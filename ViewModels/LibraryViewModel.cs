
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using SLSKDONET.Services;
using SLSKDONET.Models;
using SLSKDONET.Views; // For RelayCommand if needed, or stick to CommunityToolkit if avail? Using RelayCommand from Views based on existing code.

namespace SLSKDONET.ViewModels;

public class LibraryViewModel : INotifyPropertyChanged
{
    private readonly ILogger<LibraryViewModel> _logger;
    private readonly DownloadManager _downloadManager;
    
    // Master/Detail pattern properties
    private ObservableCollection<PlaylistJob> _allProjects = new();
    private PlaylistJob? _selectedProject;
    private ObservableCollection<PlaylistTrackViewModel> _currentProjectTracks = new();
    
    public CollectionViewSource ActiveTracksInit { get; } = new();
    public ICollectionView ActiveTracksView => ActiveTracksInit.View;

    public CollectionViewSource WarehouseTracksInit { get; } = new();
    public ICollectionView WarehouseTracksView => WarehouseTracksInit.View;

    public ICommand HardRetryCommand { get; }
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

    public LibraryViewModel(ILogger<LibraryViewModel> logger, DownloadManager downloadManager)
    {
        _logger = logger;
        _downloadManager = downloadManager;

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
        
        HardRetryCommand = new RelayCommand<PlaylistTrackViewModel>(ExecuteHardRetry);
        OpenProjectCommand = new RelayCommand<PlaylistJob>(job => SelectedProject = job);
        
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

    private void LoadProjectTracks(PlaylistJob job)
    {
        _logger.LogInformation("Loading tracks for project: {Name}", job.SourceTitle);
        
        var tracks = new ObservableCollection<PlaylistTrackViewModel>();
        
        foreach (var track in job.PlaylistTracks)
        {
            var vm = new PlaylistTrackViewModel(track);
            
            // Sync with live DownloadManager state
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
        
        CurrentProjectTracks = tracks;
    }
    
    private async Task LoadProjectsAsync()
    {
        // TODO: Load from database service when implemented
        // For now, create a mock "All Tracks" project
        var allTracksProject = new PlaylistJob
        {
            Id = Guid.Empty,
            SourceTitle = "All Downloads",
            SourceType = "Global",
            CreatedAt = DateTime.Now,
            PlaylistTracks = _downloadManager.AllGlobalTracks
                .Select(vm => vm.Model)
                .Where(m => m != null)
                .ToList()!
        };
        
        AllProjects.Add(allTracksProject);
        
        // Auto-select first project
        if (AllProjects.Count > 0)
            SelectedProject = AllProjects[0];
        
        await Task.CompletedTask;
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
