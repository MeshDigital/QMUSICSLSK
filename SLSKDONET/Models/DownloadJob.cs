using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SLSKDONET.Models;

/// <summary>
/// Represents a download job for a track.
/// </summary>
public class DownloadJob : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public Track Track { get; set; } = null!;

    private DownloadState _state = DownloadState.Pending;
    public DownloadState State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    private double _progress; // 0.0 to 1.0
    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    private long? _bytesDownloaded;
    public long? BytesDownloaded
    {
        get => _bytesDownloaded;
        set => SetProperty(ref _bytesDownloaded, value);
    }

    public string? OutputPath { get; set; }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    private DateTime? _startedAt;
    public DateTime? StartedAt
    {
        get => _startedAt;
        set => SetProperty(ref _startedAt, value);
    }

    private DateTime? _completedAt;
    public DateTime? CompletedAt
    {
        get => _completedAt;
        set => SetProperty(ref _completedAt, value);
    }

    public TimeSpan? ElapsedTime => 
        (CompletedAt ?? DateTime.UtcNow) - (StartedAt ?? CreatedAt);

    public override string ToString()
    {
        return $"[{State}] {Track.Artist} - {Track.Title} ({Progress:P})";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// Represents the state of a download.
/// </summary>
public enum DownloadState
{
    Pending,
    Searching,
    Downloading,
    Completed,
    Failed,
    Cancelled
}
