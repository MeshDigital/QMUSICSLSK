using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using SLSKDONET.Models;

namespace SLSKDONET.ViewModels.Library;

public class HierarchicalLibraryViewModel
{
    private readonly ObservableCollection<AlbumNode> _albums = new();
    public HierarchicalTreeDataGridSource<ILibraryNode> Source { get; }

    public HierarchicalLibraryViewModel()
    {
        Source = new HierarchicalTreeDataGridSource<ILibraryNode>(_albums)
        {
            Columns =
            {
                new TemplateColumn<ILibraryNode>(
                    "🎨",
                    new FuncDataTemplate<object>((item, _) => 
                    {
                        if (item is not ILibraryNode node) return new Panel();

                        return new Border { 
                            Width = 32, Height = 32, CornerRadius = new CornerRadius(4), ClipToBounds = true,
                            Background = Brush.Parse("#2D2D2D"),
                            Margin = new Thickness(4),
                            Child = new Image { 
                                [!Image.SourceProperty] = new Binding(nameof(ILibraryNode.AlbumArtPath))
                                {
                                    Converter = new FuncValueConverter<string?, IImage?>(path => 
                                    {
                                        if (string.IsNullOrEmpty(path)) return null;
                                        try {
                                            if (System.IO.File.Exists(path)) return new Avalonia.Media.Imaging.Bitmap(path);
                                        } catch {} // Ignore errors
                                        return null;
                                    })
                                },
                                Stretch = Stretch.UniformToFill
                            }
                        };
                    }, false), // Disable recycling
                    new GridLength(40)),
                new HierarchicalExpanderColumn<ILibraryNode>(
                    new TextColumn<ILibraryNode, string>("Title", x => x.Title ?? "Unknown"),
                    x => x is AlbumNode album ? album.Tracks : null),
                new TextColumn<ILibraryNode, int>("#", x => x.SortOrder),
                new TextColumn<ILibraryNode, string>("Artist", x => x.Artist ?? string.Empty),
                new TextColumn<ILibraryNode, string>("Album", x => x.Album ?? string.Empty),
                new TextColumn<ILibraryNode, string>("Duration", x => x.Duration ?? string.Empty),
                new TextColumn<ILibraryNode, int>("🔥", x => x.Popularity),
                new TextColumn<ILibraryNode, string>("Bitrate", x => x.Bitrate ?? string.Empty),
                new TextColumn<ILibraryNode, string>("Genres", x => x.Genres ?? string.Empty),
                new TemplateColumn<ILibraryNode>(
                    "Status",
                    new FuncDataTemplate<object>((item, _) => 
                    {
                         if (item is not ILibraryNode node) return new Panel();

                        return new StackPanel {
                            Spacing = 4,
                            VerticalAlignment = VerticalAlignment.Center,
                            Children = {
                                new TextBlock { [!TextBlock.TextProperty] = new Binding(nameof(ILibraryNode.Status)) },
                                new ProgressBar { 
                                    Height = 4, 
                                    Minimum = 0, Maximum = 1,
                                    [!ProgressBar.ValueProperty] = new Binding(nameof(ILibraryNode.Progress)),
                                    [!Visual.IsVisibleProperty] = new Binding(nameof(ILibraryNode.Progress)) { Converter = new FuncValueConverter<double, bool>(v => v > 0) }
                                }
                            }
                        };
                    }, false),
                    new GridLength(100)),
            }
        };
    }

    public void UpdateTracks(IEnumerable<PlaylistTrackViewModel> tracks)
    {
        _albums.Clear();
        var grouped = tracks.GroupBy(t => t.Model.Album ?? "Unknown Album");
        
        foreach (var group in grouped)
        {
            var firstTrack = group.First();
            var albumNode = new AlbumNode(group.Key, firstTrack.Artist)
            {
                AlbumArtPath = firstTrack.AlbumArtPath
            };
            foreach (var track in group)
            {
                albumNode.Tracks.Add(track);
            }
            _albums.Add(albumNode);
        }

        // Auto-expand all albums by default
        for (int i = 0; i < _albums.Count; i++)
        {
            Source.Expand(new IndexPath(i));
        }
    }
}
