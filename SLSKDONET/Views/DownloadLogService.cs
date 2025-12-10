using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SLSKDONET.Configuration;
using SLSKDONET.Models;

namespace SLSKDONET.Services;

/// <summary>
/// Manages a persistent log of downloaded tracks.
/// </summary>
public class DownloadLogService
{
    private readonly ILogger<DownloadLogService> _logger;
    private readonly string _logFilePath;
    private List<Track> _logEntries;

    public DownloadLogService(ILogger<DownloadLogService> logger)
    {
        _logger = logger;
        var configDir = Path.GetDirectoryName(ConfigManager.GetDefaultConfigPath());
        _logFilePath = Path.Combine(configDir ?? AppContext.BaseDirectory, "download_log.json");
        _logEntries = LoadLog();
    }

    public List<Track> GetEntries() => _logEntries.ToList();

    public void AddEntry(Track track)
    {
        if (!_logEntries.Any(t => t.Filename == track.Filename && t.Username == track.Username))
        {
            _logEntries.Add(track);
            SaveLog();
            _logger.LogInformation("Added '{Filename}' to download log.", track.Filename);
        }
    }

    public void RemoveEntries(IEnumerable<Track> tracks)
    {
        int count = 0;
        foreach (var track in tracks)
        {
            var entryToRemove = _logEntries.FirstOrDefault(t => t.Filename == track.Filename && t.Username == track.Username);
            if (entryToRemove != null)
            {
                _logEntries.Remove(entryToRemove);
                count++;
            }
        }
        if (count > 0)
        {
            SaveLog();
            _logger.LogInformation("Removed {Count} entries from download log.", count);
        }
    }

    private List<Track> LoadLog()
    {
        try
        {
            if (!File.Exists(_logFilePath)) return new List<Track>();
            var json = File.ReadAllText(_logFilePath);
            return JsonSerializer.Deserialize<List<Track>>(json) ?? new List<Track>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load download log from {LogFilePath}", _logFilePath);
            return new List<Track>();
        }
    }

    private void SaveLog()
    {
        var json = JsonSerializer.Serialize(_logEntries, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_logFilePath, json);
    }
}