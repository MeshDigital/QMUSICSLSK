using System;
using System.Threading;
using SLSKDONET.Models;

namespace SLSKDONET.Services.Models;

/// <summary>
/// Internal state tracking context for a download managed by DownloadManager.
/// Replaces the use of PlaylistTrackViewModel within the service layer.
/// </summary>
public class DownloadContext
{
    public PlaylistTrack Model { get; }
    public CancellationTokenSource CancellationTokenSource { get; set; } = new();
    
    // Transient State
    public PlaylistTrackState State { get; set; } = PlaylistTrackState.Pending;
    public double Progress { get; set; }
    public string GlobalId => Model.TrackUniqueHash;
    public string? ErrorMessage { get; set; }

    // Reliability (Phase 7: DJ's Studio)
    public int RetryCount { get; set; }
    public DateTime? NextRetryTime { get; set; }

    public DownloadContext(PlaylistTrack model)
    {
        Model = model;
        
        // Map initial state from persistence
        if (model.Status == TrackStatus.Downloaded)
        {
            State = PlaylistTrackState.Completed;
            Progress = 100;
        }
    }

    public bool IsActive => State == PlaylistTrackState.Searching || 
                           State == PlaylistTrackState.Downloading || 
                           State == PlaylistTrackState.Queued;
}
