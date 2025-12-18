namespace SLSKDONET.ViewModels.Library;

public interface ILibraryNode
{
    string? Title { get; }
    string? Artist { get; }
    string? Album { get; }
    string? Duration { get; }
    string? Bitrate { get; }
    string? Status { get; }
    int SortOrder { get; }
    int Popularity { get; }
    string? Genres { get; }
    string? AlbumArtPath { get; }
    double Progress { get; }
}
