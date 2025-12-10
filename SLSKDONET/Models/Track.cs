using System.IO;

namespace SLSKDONET.Models;

/// <summary>
/// Represents a music track found on Soulseek.
/// </summary>
public class Track
{
    public string? Filename { get; set; }
    public string? Artist { get; set; }
    public string? Title { get; set; }
    public string? Album { get; set; }
    public long? Size { get; set; }
    public string? Username { get; set; }
    public string? Format { get; set; }
    public int? Length { get; set; } // in seconds
    public int Bitrate { get; set; } // in kbps
    public Dictionary<string, object>? Metadata { get; set; }
    public bool IsSelected { get; set; } = false;
    public Soulseek.File? SoulseekFile { get; set; }

    /// <summary>
    /// Gets the file extension from the filename.
    /// </summary>
    public string GetExtension()
    {
        if (string.IsNullOrEmpty(Filename))
            return "";
        return Path.GetExtension(Filename).TrimStart('.');
    }

    /// <summary>
    /// Gets a user-friendly size representation.
    /// </summary>
    public string GetFormattedSize()
    {
        if (Size == null) return "Unknown";
        
        const long kb = 1024;
        const long mb = kb * 1024;
        const long gb = mb * 1024;

        return Size.Value switch
        {
            >= gb => $"{Size.Value / (double)gb:F2} GB",
            >= mb => $"{Size.Value / (double)mb:F2} MB",
            >= kb => $"{Size.Value / (double)kb:F2} KB",
            _ => $"{Size.Value} B"
        };
    }

    public override string ToString()
    {
        return $"{Artist} - {Title} ({Filename})";
    }
}
