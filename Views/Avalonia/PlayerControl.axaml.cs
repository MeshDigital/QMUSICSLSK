using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SLSKDONET.Models;
using SLSKDONET.Services;
using SLSKDONET.ViewModels;

namespace SLSKDONET.Views.Avalonia;

public partial class PlayerControl : UserControl
{
    public PlayerControl()
    {
        InitializeComponent();
        
        // Enable drag-drop on the entire player control
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        DragDrop.SetAllowDrop(this, true);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        // Accept tracks or albums from library or queue
        if (e.Data.Contains(DragContext.LibraryTrackFormat) || 
            e.Data.Contains(DragContext.QueueTrackFormat) ||
            e.Data.Contains("ORBIT_LibraryAlbum"))
        {
            e.DragEffects = DragDropEffects.Copy;
            
            // Visual feedback - could add a highlight effect here
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        // Identify the drop zone
        var dropTarget = e.Source as Control;
        bool isPlayNowZone = dropTarget != null && (dropTarget.Name == "PlayNowZone" || dropTarget.FindAncestorOfType<Control>(x => x.Name == "PlayNowZone") != null);
        bool isQueueZone = dropTarget != null && (dropTarget.Name == "QueueZone" || dropTarget.FindAncestorOfType<Control>(x => x.Name == "QueueZone") != null);

        // Get the dragged track or album ID
        string? trackGlobalId = null;
        string? albumIdStr = null;

        if (e.Data.Contains(DragContext.LibraryTrackFormat))
        {
            trackGlobalId = e.Data.Get(DragContext.LibraryTrackFormat) as string;
        }
        else if (e.Data.Contains(DragContext.QueueTrackFormat))
        {
            trackGlobalId = e.Data.Get(DragContext.QueueTrackFormat) as string;
        }
        else if (e.Data.Contains("ORBIT_LibraryAlbum"))
        {
            albumIdStr = e.Data.Get("ORBIT_LibraryAlbum") as string;
        }

        if (string.IsNullOrEmpty(trackGlobalId) && string.IsNullOrEmpty(albumIdStr))
            return;

        if (DataContext is not PlayerViewModel playerViewModel)
            return;

        var mainWindow = this.VisualRoot as MainWindow;
        var mainViewModel = mainWindow?.DataContext as MainViewModel;

        // HANDLE ALBUM DROP
        if (!string.IsNullOrEmpty(albumIdStr) && Guid.TryParse(albumIdStr, out var albumId))
        {
            // We need the tracks for this album.
            // Try to get them from the LibraryViewModel (SelectedProject or global)
            // or just trigger PlayAlbum/DownloadAlbum logic.
            if (mainViewModel?.LibraryViewModel != null)
            {
                var project = await mainViewModel.LibraryViewModel.Projects.AllProjects.FirstOrDefault(p => p.Id == albumId);
                if (project != null)
                {
                    if (isPlayNowZone)
                        await mainViewModel.LibraryViewModel.PlayAlbumCommand.ExecuteAsync(project);
                    else if (isQueueZone)
                        await mainViewModel.LibraryViewModel.DownloadAlbumCommand.ExecuteAsync(project); // Actually need a "Queue Album" command
                }
            }
            return;
        }

        // HANDLE TRACK DROP
        // Find the track - first check queue, then try to find in library via MainViewModel
        var track = playerViewModel.Queue.FirstOrDefault(t => t.GlobalId == trackGlobalId);
        
        if (track == null && mainViewModel != null)
        {
            // Try to find in download manager's global tracks
            track = mainViewModel.AllGlobalTracks
                .FirstOrDefault(t => t.GlobalId == trackGlobalId);
        }

        if (track != null)
        {
            if (isPlayNowZone)
            {
                if (!string.IsNullOrEmpty(track.Model?.ResolvedFilePath))
                {
                    playerViewModel.PlayTrack(
                        track.Model.ResolvedFilePath,
                        track.Title ?? "Unknown",
                        track.Artist ?? "Unknown Artist"
                    );
                }
            }
            else if (isQueueZone)
            {
                playerViewModel.AddToQueue(track);
            }
        }
    }
}
