using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using SLSKDONET.Configuration;
using SLSKDONET.Models;
using SpotifyAPI.Web;

namespace SLSKDONET.Services.InputParsers;

/// <summary>
/// Parses a Spotify playlist URL to extract a list of tracks to search for.
/// </summary>
public class SpotifyInputSource
{
    private readonly AppConfig _config;
    public bool IsConfigured { get; }

    public SpotifyInputSource(AppConfig config)
    {
        _config = config;
        // Considered configured if the client ID and secret are present in the config.
        IsConfigured = !string.IsNullOrEmpty(config.SpotifyClientId) && !string.IsNullOrEmpty(config.SpotifyClientSecret);
    }

    public async Task<List<SearchQuery>> ParseAsync(string url)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Spotify is not configured. Please add Client ID and Secret in config.ini.");
        }

        var spotifyConfig = SpotifyClientConfig.CreateDefault()
            .WithAuthenticator(new ClientCredentialsAuthenticator(_config.SpotifyClientId!, _config.SpotifyClientSecret!));

        var spotify = new SpotifyClient(spotifyConfig);
        var queries = new List<SearchQuery>();

        try
        {
            var playlistId = url.Split('/').Last().Split('?').First();
            var playlistItems = await spotify.Playlists.GetItems(playlistId);

            if (playlistItems == null) return queries;

            await foreach (var item in spotify.Paginate(playlistItems))
            {
                if (item.Track is FullTrack track)
                {
                    queries.Add(new SearchQuery
                    {
                        Artist = track.Artists.FirstOrDefault()?.Name,
                        Title = track.Name,
                        Album = track.Album.Name
                    });
                }
            }
        }
        catch (APIException ex)
        {
            throw new InvalidOperationException($"Failed to fetch Spotify playlist. Please check the URL and your API credentials. Error: {ex.Message}");
        }

        return queries;
    }
}
