using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Configuration;
using SLSKDONET.Models;
using SpotifyAPI.Web;

namespace SLSKDONET.Services.InputParsers;

/// <summary>
/// Spotify API-based playlist/album fetcher (Client Credentials flow).
/// </summary>
public class SpotifyInputSource : IInputSource
{
	private readonly ILogger<SpotifyInputSource> _logger;
	private readonly AppConfig _config;
	private readonly SpotifyAuthService _spotifyAuth;

	public InputType InputType => InputType.Spotify;

	public bool IsConfigured => 
		_spotifyAuth.IsAuthenticated || 
		(!string.IsNullOrWhiteSpace(_config.SpotifyClientId) && !string.IsNullOrWhiteSpace(_config.SpotifyClientSecret));

	public SpotifyInputSource(ILogger<SpotifyInputSource> logger, AppConfig config, SpotifyAuthService spotifyAuth)
	{
		_logger = logger;
		_config = config;
		_spotifyAuth = spotifyAuth;
	}

	public async Task<List<SearchQuery>> ParseAsync(string url)
	{
		if (!IsConfigured)
			throw new InvalidOperationException("Spotify API is not configured or authenticated.");

		_logger.LogInformation("Spotify API: parsing {Url}", url);

		var client = await GetClientAsync();
		var queries = new List<SearchQuery>();

		if (url.Equals("liked", StringComparison.OrdinalIgnoreCase) || url.Contains("liked-songs"))
		{
			queries = await FetchLikedSongsAsync(client);
		}
		else
		{
			var playlistId = ExtractPlaylistId(url);
			if (string.IsNullOrEmpty(playlistId))
				throw new InvalidOperationException("Invalid Spotify playlist URL.");

			queries = await FetchPlaylistTracksAsync(client, playlistId);
		}

		_logger.LogInformation("Spotify API: extracted {Count} tracks", queries.Count);
		return queries;
	}

	private async Task<List<SearchQuery>> FetchLikedSongsAsync(SpotifyClient client)
	{
		var queries = new List<SearchQuery>();
		var response = await client.Library.GetTracks();
		var total = response.Total ?? 0;

		await foreach (var item in client.Paginate(response))
		{
			if (item.Track != null)
			{
				queries.Add(MapToSearchQuery(item.Track, "Liked Songs", total));
			}
		}

		return queries;
	}

	private async Task<List<SearchQuery>> FetchPlaylistTracksAsync(SpotifyClient client, string playlistId)
	{
		var queries = new List<SearchQuery>();
		var playlist = await client.Playlists.Get(playlistId);
		var total = playlist.Tracks?.Total ?? 0;

		if (playlist.Tracks != null)
		{
			await foreach (var item in client.Paginate(playlist.Tracks))
			{
				if (item.Track is FullTrack track)
				{
					queries.Add(MapToSearchQuery(track, playlist.Name ?? "Spotify Playlist", total));
				}
			}
		}

		return queries;
	}

	private SearchQuery MapToSearchQuery(FullTrack track, string sourceTitle, int total)
	{
		var artist = track.Artists?.FirstOrDefault()?.Name ?? "Unknown Artist";
		var title = track.Name ?? "Unknown Track";
		
		return new SearchQuery
		{
			Artist = artist,
			Title = title,
			Album = track.Album?.Name,
			SourceTitle = sourceTitle,
			TotalTracks = total,
			DownloadMode = DownloadMode.Normal,
			SpotifyTrackId = track.Id,
			SpotifyAlbumId = track.Album?.Id,
			SpotifyArtistId = track.Artists?.FirstOrDefault()?.Id,
			AlbumArtUrl = track.Album?.Images?.FirstOrDefault()?.Url,
			Popularity = track.Popularity,
			CanonicalDuration = track.DurationMs,
			ReleaseDate = DateTime.TryParse(track.Album?.ReleaseDate, out var rd) ? rd : null
		};
	}

	private async Task<SpotifyClient> GetClientAsync()
	{
		if (_spotifyAuth.IsAuthenticated)
		{
			return await _spotifyAuth.GetAuthenticatedClientAsync();
		}
		
		_logger.LogWarning("Spotify is not authenticated with User OAuth, falling back to Client Credentials...");
		var config = SpotifyClientConfig.CreateDefault();
		var request = new ClientCredentialsRequest(_config.SpotifyClientId!, _config.SpotifyClientSecret!);
		var response = await new OAuthClient(config).RequestToken(request);
		return new SpotifyClient(config.WithToken(response.AccessToken));
	}

	public static string? ExtractPlaylistId(string url)
	{
		if (string.IsNullOrWhiteSpace(url)) return null;

		if (url.StartsWith("spotify:playlist:", StringComparison.OrdinalIgnoreCase))
			return url.Replace("spotify:playlist:", "");

		if (url.Contains("spotify.com") && url.Contains("/playlist/"))
		{
			var parts = url.Split('/');
			var idx = Array.IndexOf(parts, "playlist");
			if (idx >= 0 && idx + 1 < parts.Length)
			{
				var id = parts[idx + 1].Split('?')[0];
				return string.IsNullOrEmpty(id) ? null : id;
			}
		}

		return null;
	}
}
