using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using SLSKDONET.Services;
using SLSKDONET.ViewModels;
using SLSKDONET.Models; // Added for PlaylistJob
using System.Linq;

namespace SLSKDONET.Views.Avalonia.Controls;

/// <summary>
/// Phase 6A: Album card component with glassmorphism design.
/// Displays album artwork, title, track count, and download progress.
/// </summary>
public partial class AlbumCard : UserControl
{
    public AlbumCard()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        
        // Phase 6D: Drag initiation for entire albums
        this.PointerPressed += OnPointerPressed;
        this.PointerMoved += OnPointerMoved;
    }

    private Point? _dragStartPoint;

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _dragStartPoint = e.GetPosition(this);
        }
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragStartPoint.HasValue && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var currentPoint = e.GetPosition(this);
            var diff = currentPoint - _dragStartPoint.Value;

            if (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5)
            {
                if (DataContext is PlaylistJob album)
                {
                    var data = new DataObject();
                    data.Set("ORBIT_LibraryAlbum", album.Id.ToString());
                    
                    _dragStartPoint = null;
                    await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
                }
            }
        }
        else
        {
            _dragStartPoint = null;
        }
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
         if (e.Handled) return;

         // Find LibraryViewModel and execute commands
         var libraryPage = this.FindAncestorOfType<LibraryPage>();
         if (libraryPage?.DataContext is LibraryViewModel libraryVM && DataContext is PlaylistJob job)
         {
             if (libraryVM.Projects.OpenProjectCommand.CanExecute(job))
             {
                 libraryVM.Projects.OpenProjectCommand.Execute(job);
                 e.Handled = true;
             }
         }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        // Get the target project (PlaylistJob)
        if (DataContext is not PlaylistJob targetProject)
            return;

        // Get the dragged track GlobalId
        string? trackGlobalId = null;
        if (e.Data.Contains(DragContext.LibraryTrackFormat))
        {
            trackGlobalId = e.Data.Get(DragContext.LibraryTrackFormat) as string;
        }

        if (string.IsNullOrEmpty(trackGlobalId))
            return;

        // Find the LibraryViewModel in the visual tree
        var libraryPage = this.FindAncestorOfType<LibraryPage>();
        if (libraryPage?.DataContext is LibraryViewModel libraryViewModel)
        {
            await libraryViewModel.UpdateTrackProjectAsync(trackGlobalId, targetProject.Id);
        }
    }
}
