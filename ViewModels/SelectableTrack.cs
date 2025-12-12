using System.ComponentModel;
using System.Runtime.CompilerServices;
using SLSKDONET.Models;

namespace SLSKDONET.ViewModels;

/// <summary>
/// Wrapper for Track with selection state for Spotify import UI.
/// </summary>
public class SelectableTrack : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private int _trackNumber;

    public Track Track { get; set; }
    
    public int TrackNumber
    {
        get => _trackNumber;
        set
        {
            if (_trackNumber != value)
            {
                _trackNumber = value;
                OnPropertyChanged();
            }
        }
    }
    
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    // Convenience properties for UI binding
    public string Title => Track?.Title ?? "";
    public string Artist => Track?.Artist ?? "";
    public string Album => Track?.Album ?? "";
    public string AlbumArt => ""; // TODO: Add album art support to Track model
    public int Duration => Track?.Length ?? 0;
    
    public string DurationFormatted => Duration > 0 
        ? TimeSpan.FromSeconds(Duration).ToString(@"mm\:ss") 
        : "--:--";

    public SelectableTrack(Track track, int trackNumber = 1)
    {
        Track = track;
        TrackNumber = trackNumber;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
