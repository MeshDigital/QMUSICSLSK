using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using SLSKDONET.Models;

namespace SLSKDONET.ViewModels.Library;

public class AlbumNode : ILibraryNode, INotifyPropertyChanged
{
    public string? AlbumTitle { get; set; }
    public string? Artist { get; set; }
    public string? Title => AlbumTitle;
    public string? Album => AlbumTitle;
    public string? Duration => string.Empty;
    public string? Bitrate => string.Empty;
    public string? Status => string.Empty;
    public int SortOrder => 0;
    public int Popularity => 0;
    public string? Genres => string.Empty;
    public string? AlbumArtPath { get; set; }

    public double Progress
    {
        get
        {
            if (Tracks == null || !Tracks.Any()) return 0;
            // Only count tracks that have started or are downloading
            var tracksWithProgress = Tracks.Where(t => t.Progress > 0).ToList();
            if (!tracksWithProgress.Any()) return 0;
            
            return tracksWithProgress.Average(t => t.Progress);
        }
    }

    public ObservableCollection<PlaylistTrackViewModel> Tracks { get; } = new();

    public AlbumNode(string? albumTitle, string? artist)
    {
        AlbumTitle = albumTitle;
        Artist = artist;
        Tracks.CollectionChanged += (s, e) => {
            if (e.NewItems != null)
            {
                foreach (PlaylistTrackViewModel item in e.NewItems)
                    item.PropertyChanged += OnTrackPropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (PlaylistTrackViewModel item in e.OldItems)
                    item.PropertyChanged -= OnTrackPropertyChanged;
            }
            OnPropertyChanged(nameof(Progress));
        };
    }

    private void OnTrackPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaylistTrackViewModel.Progress))
        {
            OnPropertyChanged(nameof(Progress));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
