using System;
using System.IO;

namespace SLSKDONET.Utils;

/// <summary>
/// Utility methods for file naming and formatting.
/// </summary>
public static class FileFormattingUtils
{
    /// <summary>
    /// Sanitizes a filename to remove invalid characters.
    /// </summary>
    public static string SanitizeFilename(string filename)
    {
        var invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        foreach (var c in invalid)
            filename = filename.Replace(c.ToString(), "_");

        // Remove multiple consecutive underscores
        while (filename.Contains("__"))
            filename = filename.Replace("__", "_");

        return filename.Trim('_', ' ');
    }

    /// <summary>
    /// Formats a file size in bytes to a human-readable string.
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        const long kb = 1024;
        const long mb = kb * 1024;
        const long gb = mb * 1024;

        return bytes switch
        {
            >= gb => $"{bytes / (double)gb:F2} GB",
            >= mb => $"{bytes / (double)mb:F2} MB",
            >= kb => $"{bytes / (double)kb:F2} KB",
            _ => $"{bytes} B"
        };
    }

    /// <summary>
    /// Formats a duration in seconds to HH:MM:SS.
    /// </summary>
    public static string FormatDuration(int? seconds)
    {
        if (seconds == null || seconds <= 0)
            return "--:--";

        var ts = TimeSpan.FromSeconds(seconds.Value);
        return ts.Hours > 0
            ? $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}
