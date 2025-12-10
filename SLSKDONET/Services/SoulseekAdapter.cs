using System.IO;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using SLSKDONET.Configuration;
using SLSKDONET.Models;
using Soulseek;
using System.Linq;
using System.Collections.Concurrent;

namespace SLSKDONET.Services;

/// <summary>
/// Real Soulseek.NET adapter for network interactions.
/// </summary>
public class SoulseekAdapter : IDisposable
{
    private readonly ILogger<SoulseekAdapter> _logger;
    private readonly AppConfig _config;
    private SoulseekClient? _client;

    // Events as observables for reactive programming
    public Subject<(string eventType, object data)> EventBus { get; } = new();

    public SoulseekAdapter(ILogger<SoulseekAdapter> logger, AppConfig config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task ConnectAsync(string? password = null, CancellationToken ct = default)
    {
        try
        {
            _client = new SoulseekClient();
            _logger.LogInformation("Connecting to Soulseek as {Username}...", _config.Username);
            
            await _client.ConnectAsync(_config.Username, password, ct);
            
            _logger.LogInformation("Successfully connected to Soulseek as {Username}", _config.Username);
            EventBus.OnNext(("connection_status", new { status = "connected", username = _config.Username }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Soulseek: {Message}", ex.Message);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_client != null)
        {
            _client.Disconnect();
            await Task.CompletedTask;
            _logger.LogInformation("Disconnected from Soulseek");
        }
    }

    public async Task<int> SearchAsync(
        string query,
        IEnumerable<string>? formatFilter,
        (int? Min, int? Max) bitrateFilter,
        DownloadMode mode, // Add DownloadMode parameter
        Action<Track> onTrackFound,
        CancellationToken ct = default)
    {
        if (_client == null)
        {
            throw new InvalidOperationException("Not connected to Soulseek");
        }

        try
        {
            _logger.LogInformation("=== SEARCH STARTING ===");
            _logger.LogInformation("Query: {Query}", query);
            _logger.LogInformation("Mode: {Mode}", mode);
            _logger.LogInformation("Format filter: {Formats}", formatFilter == null ? "NONE" : string.Join(", ", formatFilter));
            _logger.LogInformation("Bitrate filter: Min={Min}, Max={Max}", bitrateFilter.Min, bitrateFilter.Max);
            _logger.LogInformation("Connected: {Connected}", _client != null && _client.State.HasFlag(SoulseekClientStates.Connected));
            
            var searchQuery = Soulseek.SearchQuery.FromText(query);
            var options = new SearchOptions(
                searchTimeout: 30000, // 30 seconds
                responseLimit: 1000,
                fileLimit: 10000
            );

            var resultCount = 0;
            var totalFilesReceived = 0;
            var filteredByFormat = 0;
            var filteredByBitrate = 0;
            var formatSet = formatFilter?.Select(f => f.ToLowerInvariant()).ToHashSet();
            
            // For Album mode, we'll collect directories to evaluate later.
            var directories = new ConcurrentDictionary<string, List<Soulseek.File>>();

            var searchResult = await _client.SearchAsync(
                searchQuery,
                (response) =>
                {
                    _logger.LogDebug("Received response from {User} with {Count} files", response.Username, response.Files.Count());
                    
                    // Process each search response
                    foreach (var file in response.Files)
                    {
                        if (mode == DownloadMode.Album)
                        {
                            var directoryName = Path.GetDirectoryName(file.Filename);
                            if (!string.IsNullOrEmpty(directoryName))
                            {
                                var key = $"{response.Username}@{directoryName}";
                                directories.AddOrUpdate(key, 
                                    _ => new List<Soulseek.File> { file }, 
                                    (_, list) => { list.Add(file); return list; });
                            }
                        }
                        else // Normal mode
                        {
                            totalFilesReceived++;
                            
                            // Apply format filter
                            var extension = Path.GetExtension(file.Filename)?.TrimStart('.').ToLowerInvariant();
                            if (formatSet != null && formatSet.Any() && !formatSet.Contains(extension ?? ""))
                            {
                                filteredByFormat++;
                                if (filteredByFormat <= 3) // Log first 3 filtered items
                                {
                                    _logger.LogInformation("[FILTER] Rejected by format: {File} (extension: {Ext}, allowed: {Formats})", file.Filename, extension, string.Join(", ", formatSet));
                                }
                                continue;
                            }

                            // Parse filename to extract artist, title, album
                            var track = ParseTrackFromFile(file, response.Username);

                            // Apply bitrate filter
                            if (bitrateFilter.Min.HasValue && track.Bitrate < bitrateFilter.Min.Value)
                            {
                                filteredByBitrate++;
                                _logger.LogTrace("Filtered by bitrate (too low): {File} ({Bitrate} < {Min})", file.Filename, track.Bitrate, bitrateFilter.Min.Value);
                                continue;
                            }
                            if (bitrateFilter.Max.HasValue && track.Bitrate > bitrateFilter.Max.Value)
                            {
                                filteredByBitrate++;
                                _logger.LogTrace("Filtered by bitrate (too high): {File} ({Bitrate} > {Max})", file.Filename, track.Bitrate, bitrateFilter.Max.Value);
                                continue;
                            }

                            if (resultCount < 3) // Log first 3 accepted tracks
                            {
                                _logger.LogInformation("[ACCEPT] Track passed filters: {Artist} - {Title} ({Bitrate} kbps, {Ext})", track.Artist, track.Title, track.Bitrate, extension);
                            }
                            onTrackFound(track);
                            resultCount++;
                        }
                    }
                },
                options: options,
                cancellationToken: ct
            );

            if (mode == DownloadMode.Album)
            {
                _logger.LogInformation("Found {Count} potential album directories.", directories.Count);
                // TODO: In a future step, we would rank these directories and create album download jobs.
                // For now, we will just log them.
                resultCount = directories.Count;
            }

            _logger.LogInformation("=== Search Summary ===");
            _logger.LogInformation("Total files received: {Total}", totalFilesReceived);
            _logger.LogInformation("Filtered by format: {Count}", filteredByFormat);
            _logger.LogInformation("Filtered by bitrate: {Count}", filteredByBitrate);
            _logger.LogInformation("Results passed to UI: {Count}", resultCount);
            _logger.LogInformation("=======================");
            
            return resultCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed: {Message}", ex.Message);
            throw;
        }
    }

    private Track ParseTrackFromFile(Soulseek.File file, string username)
    {
        // Extract bitrate and length from file attributes
        var bitrateAttr = file.Attributes?.FirstOrDefault(a => a.Type == FileAttributeType.BitRate);
        var bitrate = bitrateAttr?.Value ?? 0;
        var lengthAttr = file.Attributes?.FirstOrDefault(a => a.Type == FileAttributeType.Length);
        var length = lengthAttr?.Value ?? 0;

        // Parse filename for artist/title (basic implementation)
        var filename = Path.GetFileNameWithoutExtension(file.Filename);
        var parts = filename.Split(new[] { '-', '_' }, 2);
        
        string artist = "Unknown Artist";
        string title = filename;
        string album = "";

        if (parts.Length >= 2)
        {
            artist = parts[0].Trim();
            title = parts[1].Trim();
        }

        // Try to extract album from path
        var pathParts = file.Filename.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (pathParts.Length > 1)
        {
            album = pathParts[^2]; // Second to last part is often the album
        }

        return new Track
        {
            Artist = artist,
            Title = title,
            Album = album,
            Filename = file.Filename,
            Username = username,
            Bitrate = bitrate,
            Size = file.Size,
            Length = length,
            SoulseekFile = file // CRITICAL: Store the original file object for downloads.
        };
    }

    public async Task<bool> DownloadAsync(
        string username,
        string filename,
        string outputPath,
        long? size,
        IProgress<double> progress,
        CancellationToken ct = default)
    {
        if (this._client == null)
        {
            throw new InvalidOperationException("Not connected to Soulseek");
        }

        try
        {
            this._logger.LogInformation("Downloading {Filename} from {Username} to {OutputPath}", filename, username, outputPath);

            var directory = Path.GetDirectoryName(outputPath);
            if (directory != null)
                System.IO.Directory.CreateDirectory(directory);

            // Use Soulseek.NET's DownloadAsync which returns byte array
            // We need to track progress and write to file
            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
            
            var downloadOptions = new TransferOptions(
                stateChanged: (args) =>
                {
                    if (args.Transfer.State.HasFlag(TransferStates.InProgress))
                    {
                        if (size.HasValue && size.Value > 0)
                        {
                            double percentage = (double)args.Transfer.BytesTransferred / size.Value;
                            progress?.Report(percentage);
                        }
                    }
                });

            var data = await this._client.DownloadAsync(
                username: username,
                filename: filename,
                size: size,
                options: downloadOptions,
                cancellationToken: ct);

            // Write the downloaded data to file
            await fileStream.WriteAsync(data, 0, data.Length, ct);
            await fileStream.FlushAsync(ct);

            this._logger.LogInformation("Download completed: {Filename}", filename);
            progress?.Report(1.0);
            this.EventBus.OnNext(("transfer_finished", new { filename, username }));
            return true;
        }
        catch (OperationCanceledException)
        {
            this._logger.LogWarning("Download cancelled: {Filename}", filename);
            this.EventBus.OnNext(("transfer_cancelled", new { filename, username }));
            return false;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Download failed: {Message}", ex.Message);
            this.EventBus.OnNext(("transfer_failed", new { filename, username, error = ex.Message }));
            return false;
        }
    }

    public void Dispose()
    {
        try
        {
            _client?.Disconnect();
            _client?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing SoulseekClient");
        }
        EventBus?.Dispose();
    }

    public bool IsConnected => _client?.State.HasFlag(SoulseekClientStates.Connected) == true;
}
