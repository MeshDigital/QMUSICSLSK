using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;
using SLSKDONET.Services;
using SLSKDONET.Services.InputParsers;
using SLSKDONET.Views;

namespace SLSKDONET.ViewModels;

public class SpotifyImportViewModel : INotifyPropertyChanged
{
    private readonly ILogger<SpotifyImportViewModel> _logger;
    private readonly SpotifyAuthService _authService;
    
    // Dependencies
    private readonly SpotifyInputSource? _spotifyApiService;
    private readonly SpotifyScraperInputSource _spotifyScraperService;
    private readonly DownloadManager _downloadManager;
    
    // Properties
    public ObservableCollection<SelectableTrack> Tracks { get; } = new();
    
    private string _playlistUrl = "";
    public string PlaylistUrl
    {
        get => _playlistUrl;
        set { _playlistUrl = value; OnPropertyChanged(); }
    }

    private string _playlistTitle = "Spotify Playlist";
    public string PlaylistTitle
    {
        get => _playlistTitle;
        set { _playlistTitle = value; OnPropertyChanged(); }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public int TrackCount => Tracks.Count;
    public int SelectedCount => Tracks.Count(t => t.IsSelected);

    // User Playlists
    public ObservableCollection<SpotifyPlaylistViewModel> UserPlaylists { get; } = new();
    
    private bool _isAuthenticated;
    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set 
        { 
            _isAuthenticated = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(ShowLoginButton));
            OnPropertyChanged(nameof(ShowPlaylists));
        }
    }
    
    public bool ShowLoginButton => !IsAuthenticated;
    public bool ShowPlaylists => IsAuthenticated;

    public ICommand ConnectCommand { get; }
    public ICommand RefreshPlaylistsCommand { get; }
    public ICommand ImportPlaylistCommand { get; }
    public ICommand LoadPlaylistCommand { get; }
    public ICommand DownloadCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand CancelCommand { get; }

    public SpotifyImportViewModel(
        ILogger<SpotifyImportViewModel> logger,
        SpotifyInputSource? spotifyApiService,
        SpotifyScraperInputSource spotifyScraperService,
        DownloadManager downloadManager,
        SpotifyAuthService authService)
    {
        _logger = logger;
        _spotifyApiService = spotifyApiService;
        _spotifyScraperService = spotifyScraperService;
        _downloadManager = downloadManager;
        _authService = authService;
        
        // Subscribe to auth changes
        _authService.AuthenticationChanged += (s, e) => 
        {
            IsAuthenticated = e;
            if (e) _ = RefreshPlaylistsAsync();
        };

        // Initial check
        Task.Run(async () => IsAuthenticated = await _authService.IsAuthenticatedAsync());

        LoadPlaylistCommand = new AsyncRelayCommand(LoadPlaylistAsync);
        DownloadCommand = new AsyncRelayCommand(DownloadSelectedAsync, () => SelectedCount > 0);
        SelectAllCommand = new RelayCommand(SelectAll);
        DeselectAllCommand = new RelayCommand(DeselectAll);
        CancelCommand = new RelayCommand(Cancel);
        
        ConnectCommand = new AsyncRelayCommand(ConnectSpotifyAsync);
        RefreshPlaylistsCommand = new AsyncRelayCommand(RefreshPlaylistsAsync);
        ImportPlaylistCommand = new AsyncRelayCommand<SpotifyPlaylistViewModel>(ImportUserPlaylistAsync);
    }

    public async Task LoadPlaylistAsync()
    {
        if (string.IsNullOrWhiteSpace(PlaylistUrl))
        {
            StatusMessage = "Please enter a Spotify playlist URL";
            return;
        }

        IsLoading = true;
        StatusMessage = "Loading playlist...";
        Tracks.Clear();

        try
        {
            _logger.LogInformation("Loading Spotify playlist: {Url}", PlaylistUrl);

            List<SearchQuery> queries;

            // Try API first if configured
            if (_spotifyApiService?.IsConfigured == true)
            {
                _logger.LogInformation("Using Spotify API");
                StatusMessage = "Fetching from Spotify API...";
                queries = await _spotifyApiService.ParseAsync(PlaylistUrl);
            }
            else
            {
                _logger.LogInformation("Using Spotify web scraper");
                StatusMessage = "Scraping Spotify web page...";
                queries = await _spotifyScraperService.ParseAsync(PlaylistUrl);
            }

            if (!queries.Any())
            {
                StatusMessage = "No tracks found. Check the URL or playlist privacy settings.";
                return;
            }

            // Set playlist metadata
            PlaylistTitle = queries.FirstOrDefault()?.SourceTitle ?? "Spotify Playlist";
            StatusMessage = $"Loaded {queries.Count} tracks";

            // Convert to SelectableTrack
            int trackNum = 1;
            foreach (var query in queries)
            {
                var track = new Track
                {
                    Title = query.Title,
                    Artist = query.Artist,
                    Album = query.Album,
                    Length = 0, // Will be populated during search
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

                Tracks.Add(new SelectableTrack(track, trackNum++));
            }

            _logger.LogInformation("Successfully loaded {Count} tracks", Tracks.Count);
            OnPropertyChanged(nameof(TrackCount));
            OnPropertyChanged(nameof(SelectedCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Spotify playlist");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DownloadSelectedAsync()
    {
        var selectedTracks = Tracks.Where(t => t.IsSelected).ToList();

        if (!selectedTracks.Any())
        {
            StatusMessage = "No tracks selected";
            return;
        }

        _logger.LogInformation("Downloading {Count} selected tracks from Spotify playlist", selectedTracks.Count);

        // Create PlaylistJob
        var job = new PlaylistJob
        {
            Id = Guid.NewGuid(),
            SourceTitle = PlaylistTitle,
            SourceType = "Spotify (UI)",
            CreatedAt = DateTime.Now,
            DestinationFolder = "" // Will use default from config
        };

        job.OriginalTracks = new ObservableCollection<Track>(selectedTracks.Select(t => t.Track));

        // Convert SelectableTracks to PlaylistTracks and attach the PlaylistId
        foreach (var selectable in selectedTracks)
        {
            var playlistTrack = new PlaylistTrack
            {
                Id = Guid.NewGuid(),
                PlaylistId = job.Id,
                Title = selectable.Title ?? "Unknown Title",
                Artist = selectable.Artist ?? "Unknown Artist",
                Album = selectable.Album ?? "Unknown Album",
                TrackUniqueHash = $"{selectable.Artist}|{selectable.Title}".ToLowerInvariant(),
                Status = TrackStatus.Missing,
                AddedAt = DateTime.Now,
                TrackNumber = selectable.TrackNumber,
                // Fix: Map metadata from source track
                SpotifyTrackId = selectable.Track.SpotifyTrackId,
                SpotifyAlbumId = selectable.Track.SpotifyAlbumId,
                SpotifyArtistId = selectable.Track.SpotifyArtistId,
                AlbumArtUrl = selectable.Track.AlbumArtUrl,
                ArtistImageUrl = selectable.Track.ArtistImageUrl,
                Genres = selectable.Track.Genres,
                Popularity = selectable.Track.Popularity,
                CanonicalDuration = selectable.Track.CanonicalDuration,
                ReleaseDate = selectable.Track.ReleaseDate
            };

            job.PlaylistTracks.Add(playlistTrack);
        }

        job.RefreshStatusCounts();

        // Use the new DownloadManager overload which persists the PlaylistJob and queues the tracks
        await _downloadManager.QueueProject(job);

        StatusMessage = $"Queued {selectedTracks.Count} tracks for download";
        
        // TODO: Navigate back to Library and show new project
        _logger.LogInformation("Spotify import complete: {Count} tracks queued", selectedTracks.Count);
    }

    private void SelectAll()
    {
        foreach (var track in Tracks)
        {
            track.IsSelected = true;
        }
        OnPropertyChanged(nameof(SelectedCount));
    }

    private void DeselectAll()
    {
        foreach (var track in Tracks)
        {
            track.IsSelected = false;
        }
        OnPropertyChanged(nameof(SelectedCount));
    }

    private void Cancel()
    {
        // TODO: Navigate back or close window
        _logger.LogInformation("Spotify import cancelled");
    }

    public void ReorderTrack(SelectableTrack source, SelectableTrack target)
    {
        if (source == null || target == null || source == target)
            return;

        int oldIndex = Tracks.IndexOf(source);
        int newIndex = Tracks.IndexOf(target);

        if (oldIndex == -1 || newIndex == -1)
            return;

        Tracks.Move(oldIndex, newIndex);

        // Renumber all tracks
        for (int i = 0; i < Tracks.Count; i++)
        {
            Tracks[i].TrackNumber = i + 1;
        }

        _logger.LogDebug("Reordered track from position {Old} to {New}", oldIndex + 1, newIndex + 1);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async Task ConnectSpotifyAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Connecting to Spotify...";
            await _authService.StartAuthorizationAsync();
            // AuthenticationChanged event will handle the rest
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Spotify");
            StatusMessage = "Connection failed: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshPlaylistsAsync()
    {
        if (!IsAuthenticated) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Fetching your playlists...";
            UserPlaylists.Clear();

            var client = await _authService.GetAuthenticatedClientAsync();
            var page = await client.Playlists.CurrentUsers();
            
            await foreach (var playlist in client.Paginate(page))
            {
                UserPlaylists.Add(new SpotifyPlaylistViewModel
                {
                    Id = playlist.Id,
                    Name = playlist.Name,
                    ImageUrl = playlist.Images?.FirstOrDefault()?.Url ?? "",
                    TrackCount = playlist.Tracks?.Total ?? 0,
                    Owner = playlist.Owner?.DisplayName ?? "Unknown",
                    Url = playlist.ExternalUrls.ContainsKey("spotify") ? playlist.ExternalUrls["spotify"] : ""
                });
            }

            // Also try to get "My Library" (Liked Songs)
            // Note: Liked songs is a separate endpoint "Library.GetTracks", not a playlist.
            // We can add a fake "Liked Songs" entry if we want, handled by specific provider.
            UserPlaylists.Insert(0, new SpotifyPlaylistViewModel
            {
                Id = "me/tracks",
                Name = "Liked Songs",
                ImageUrl = "https://t.scdn.co/images/3099b3803ad9496896c43f22fe9be8c4.png", // Generic heart icon
                TrackCount = 0, // Hard to get total without a call
                Owner = "You",
                Url = "spotify:user:me:collection" // Special case handled by provider?
            });

            StatusMessage = $"Found {UserPlaylists.Count} playlists";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch playlists");
            StatusMessage = "Failed to load playlists";
            
            // If it's an auth error, we might want to reset auth
            if (ex.Message.Contains("Unauthorized"))
            {
                await _authService.SignOutAsync();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ImportUserPlaylistAsync(SpotifyPlaylistViewModel? playlist)
    {
        if (playlist == null) return;

        PlaylistUrl = playlist.Url;
        
        // Special handling for Liked Songs if we implement that provider
        if (playlist.Id == "me/tracks")
        {
             // TODO: Ensure a provider can handle "spotify:user:me:collection" or similar ID
             // For now, let's treat it as a URL that the LikedSongsProvider can handle
             // or assume the user wants to import that specific collection.
             // We can check if PlaylistUrl is empty or special.
        }

        await LoadPlaylistAsync();
    }
}

public class SpotifyPlaylistViewModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public int TrackCount { get; set; }
    public string Owner { get; set; } = "";
    public string Url { get; set; } = "";
    public string Description => $"{TrackCount} tracks • by {Owner}";
}
