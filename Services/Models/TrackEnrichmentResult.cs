namespace SLSKDONET.Services.Models;

public class TrackEnrichmentResult
{
    public string SpotifyId { get; set; } = string.Empty;
    public string OfficialArtist { get; set; } = string.Empty;
    public string OfficialTitle { get; set; } = string.Empty;
    public string AlbumArtUrl { get; set; } = string.Empty;
    
    // Audio Features
    public double? Bpm { get; set; }
    public double? Energy { get; set; }
    public double? Valence { get; set; }
    public double? Danceability { get; set; }
    
    public bool Success { get; set; }
    public string? Error { get; set; }
}
