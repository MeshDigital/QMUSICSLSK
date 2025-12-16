using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SLSKDONET.Services;

/// <summary>
/// Service for downloading and caching album artwork locally.
/// Downloads images from Spotify URLs and stores them in %AppData%/SLSKDONET/artwork/
/// </summary>
public class ArtworkCacheService
{
    private readonly ILogger<ArtworkCacheService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly string _placeholderPath;

    public ArtworkCacheService(ILogger<ArtworkCacheService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        
        // Set cache directory to %AppData%/SLSKDONET/artwork/
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _cacheDirectory = Path.Combine(appData, "SLSKDONET", "artwork");
        Directory.CreateDirectory(_cacheDirectory);
        
        // Placeholder path for missing artwork
        _placeholderPath = Path.Combine(_cacheDirectory, "placeholder.png");
        
        _logger.LogInformation("Artwork cache initialized at: {CacheDirectory}", _cacheDirectory);
    }

    /// <summary>
    /// Gets the local file path for album artwork, downloading it if necessary.
    /// </summary>
    /// <param name="albumArtUrl">Spotify album art URL</param>
    /// <param name="spotifyAlbumId">Spotify album ID (used as cache key)</param>
    /// <returns>Local file path to the artwork, or placeholder if unavailable</returns>
    public async Task<string> GetArtworkPathAsync(string? albumArtUrl, string? spotifyAlbumId)
    {
        // If no URL or ID provided, return placeholder
        if (string.IsNullOrWhiteSpace(albumArtUrl) || string.IsNullOrWhiteSpace(spotifyAlbumId))
        {
            return await GetPlaceholderPathAsync();
        }

        try
        {
            // Generate cache file path
            var cacheFileName = $"{spotifyAlbumId}.jpg";
            var cachePath = Path.Combine(_cacheDirectory, cacheFileName);

            // If already cached, return immediately
            if (File.Exists(cachePath))
            {
                _logger.LogDebug("Artwork cache hit for album {AlbumId}", spotifyAlbumId);
                return cachePath;
            }

            // Download artwork
            _logger.LogInformation("Downloading artwork for album {AlbumId} from {Url}", spotifyAlbumId, albumArtUrl);
            var imageBytes = await _httpClient.GetByteArrayAsync(albumArtUrl);
            
            // Save to cache
            await File.WriteAllBytesAsync(cachePath, imageBytes);
            _logger.LogInformation("Cached artwork for album {AlbumId} ({Size} bytes)", spotifyAlbumId, imageBytes.Length);
            
            return cachePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download artwork for album {AlbumId} from {Url}", spotifyAlbumId, albumArtUrl);
            return await GetPlaceholderPathAsync();
        }
    }

    /// <summary>
    /// Gets the path to the placeholder image, creating it if necessary.
    /// </summary>
    private async Task<string> GetPlaceholderPathAsync()
    {
        if (File.Exists(_placeholderPath))
            return _placeholderPath;

        try
        {
            // Create a simple 1x1 transparent PNG as placeholder
            // PNG header + IHDR + IDAT (empty) + IEND
            var placeholderBytes = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
            
            await File.WriteAllBytesAsync(_placeholderPath, placeholderBytes);
            _logger.LogInformation("Created placeholder image at {Path}", _placeholderPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create placeholder image");
        }

        return _placeholderPath;
    }

    /// <summary>
    /// Clears all cached artwork files.
    /// </summary>
    public async Task ClearCacheAsync()
    {
        try
        {
            var files = Directory.GetFiles(_cacheDirectory, "*.jpg");
            foreach (var file in files)
            {
                File.Delete(file);
            }
            
            _logger.LogInformation("Cleared {Count} cached artwork files", files.Length);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear artwork cache");
        }
    }

    /// <summary>
    /// Gets the total size of the artwork cache in bytes.
    /// </summary>
    public async Task<long> GetCacheSizeAsync()
    {
        try
        {
            var files = Directory.GetFiles(_cacheDirectory, "*.jpg");
            long totalSize = 0;
            
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                totalSize += fileInfo.Length;
            }
            
            _logger.LogDebug("Artwork cache size: {Size} bytes ({Count} files)", totalSize, files.Length);
            return await Task.FromResult(totalSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate cache size");
            return 0;
        }
    }

    /// <summary>
    /// Removes orphaned artwork files that are no longer referenced in the database.
    /// </summary>
    public async Task CleanupOrphanedArtworkAsync(HashSet<string> activeAlbumIds)
    {
        try
        {
            var files = Directory.GetFiles(_cacheDirectory, "*.jpg");
            int removedCount = 0;
            
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (!activeAlbumIds.Contains(fileName))
                {
                    File.Delete(file);
                    removedCount++;
                }
            }
            
            _logger.LogInformation("Removed {Count} orphaned artwork files", removedCount);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup orphaned artwork");
        }
    }
}
