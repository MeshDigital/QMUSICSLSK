using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using SLSKDONET.Configuration;

namespace SLSKDONET.Services;

/// <summary>
/// Provides centralized path management with filesystem-safe slug generation.
/// Prevents "Invalid character" crashes on Windows/Linux by sanitizing artist/album names.
/// Future-proof for "Move Library" and "Rename Pattern" features.
/// </summary>
public class PathProviderService
{
    private readonly AppConfig _config;
    private readonly ILogger<PathProviderService> _logger;

    public PathProviderService(AppConfig config, ILogger<PathProviderService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Gets the full path for a track with artist/album folder structure.
    /// Example: C:\Music\Artist Name\Album Name\Track.mp3
    /// </summary>
    /// <param name="artist">Artist name (will be slugified)</param>
    /// <param name="album">Album name (will be slugified)</param>
    /// <param name="title">Track title (will be slugified)</param>
    /// <param name="extension">File extension without dot (e.g., "mp3", "flac")</param>
    /// <returns>Full filesystem path to the track</returns>
    public string GetTrackPath(string artist, string album, string title, string extension)
    {
        var safeArtist = Slugify(artist);
        var safeAlbum = Slugify(album);
        var safeTitle = Slugify(title);

        var folderPath = Path.Combine(
            _config.DownloadDirectory ?? "Downloads",
            safeArtist,
            safeAlbum
        );

        // Ensure directory exists
        Directory.CreateDirectory(folderPath);

        return Path.Combine(folderPath, $"{safeTitle}.{extension}");
    }

    /// <summary>
    /// Removes invalid filesystem characters and replaces with safe alternatives.
    /// Prevents crashes on Windows/Linux from characters like ?, :, *, <, >, |
    /// </summary>
    /// <param name="input">Raw string (artist, album, or title)</param>
    /// <returns>Filesystem-safe string</returns>
    private string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "Unknown";

        // Invalid characters for Windows/Linux filesystems
        var invalidChars = new[] { '?', ':', '*', '<', '>', '|', '"', '/', '\\' };
        
        var result = input;
        foreach (var c in invalidChars)
        {
            result = result.Replace(c, '_');
        }

        // Trim and limit length to prevent path length issues
        result = result.Trim();
        if (result.Length > 200)
        {
            result = result.Substring(0, 200);
            _logger.LogWarning("Truncated long filename from {Original} to {Truncated}", input.Length, 200);
        }
        
        return string.IsNullOrWhiteSpace(result) ? "Unknown" : result;
    }

    /// <summary>
    /// Scans download directory for orphaned .part files.
    /// Used by "Clean Up Temp Files" feature in Settings.
    /// </summary>
    /// <returns>List of full paths to .part files</returns>
    public List<string> FindOrphanedPartFiles()
    {
        var downloadDir = _config.DownloadDirectory ?? "Downloads";
        if (!Directory.Exists(downloadDir))
        {
            _logger.LogWarning("Download directory does not exist: {Dir}", downloadDir);
            return new List<string>();
        }

        try
        {
            var partFiles = Directory.GetFiles(downloadDir, "*.part", SearchOption.AllDirectories).ToList();
            _logger.LogInformation("Found {Count} .part files in download directory", partFiles.Count);
            return partFiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan for .part files");
            return new List<string>();
        }
    }

    /// <summary>
    /// Gets the directory path for an album (without creating it).
    /// Used for checking if an album folder already exists.
    /// </summary>
    public string GetAlbumDirectoryPath(string artist, string album)
    {
        var safeArtist = Slugify(artist);
        var safeAlbum = Slugify(album);

        return Path.Combine(
            _config.DownloadDirectory ?? "Downloads",
            safeArtist,
            safeAlbum
        );
    }
}
