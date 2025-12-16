using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Configuration;
using SLSKDONET.Utils;
using SpotifyAPI.Web;

namespace SLSKDONET.Services;

/// <summary>
/// Manages Spotify OAuth authentication using PKCE flow.
/// Handles authorization, token exchange, refresh, and secure storage.
/// </summary>
public class SpotifyAuthService
{
    private readonly ILogger<SpotifyAuthService> _logger;
    private readonly AppConfig _config;
    private readonly LocalHttpServer _httpServer;
    private readonly ISecureTokenStorage _tokenStorage;

    private string? _currentCodeVerifier;
    private SpotifyClient? _authenticatedClient;
    private PKCETokenResponse? _currentTokenResponse;
    private DateTime _tokenExpiresAt;

    public SpotifyAuthService(
        ILogger<SpotifyAuthService> logger,
        AppConfig config,
        LocalHttpServer httpServer,
        ISecureTokenStorage tokenStorage)
    {
        _logger = logger;
        _config = config;
        _httpServer = httpServer;
        _tokenStorage = tokenStorage;
    }

    /// <summary>
    /// Checks if the user is currently authenticated with Spotify.
    /// </summary>
    public async Task<bool> IsAuthenticatedAsync()
    {
        // Check if we have a stored refresh token
        var refreshToken = await _tokenStorage.LoadRefreshTokenAsync();
        if (string.IsNullOrEmpty(refreshToken))
            return false;

        // Try to refresh the token to verify it's still valid
        try
        {
            await RefreshAccessTokenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Starts the OAuth authorization flow.
    /// Opens the browser for user consent and waits for the callback.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if authorization succeeded, false otherwise</returns>
    public async Task<bool> StartAuthorizationAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_config.SpotifyClientId))
        {
            _logger.LogError("Spotify Client ID is not configured");
            throw new InvalidOperationException("Spotify Client ID must be configured in Settings");
        }

        try
        {
            // Generate PKCE parameters
            _currentCodeVerifier = PKCEHelper.GenerateCodeVerifier();
            var codeChallenge = PKCEHelper.GenerateCodeChallenge(_currentCodeVerifier);

            _logger.LogInformation("Generated PKCE code verifier and challenge");

            // Build authorization URL
            var scopes = new[]
            {
                Scopes.UserReadPrivate,
                Scopes.UserReadEmail,
                Scopes.PlaylistReadPrivate,
                Scopes.PlaylistReadCollaborative,
                Scopes.UserLibraryRead,
                Scopes.UserFollowRead
            };

            var loginRequest = new LoginRequest(
                new Uri(_config.SpotifyRedirectUri),
                _config.SpotifyClientId,
                LoginRequest.ResponseType.Code)
            {
                CodeChallengeMethod = "S256",
                CodeChallenge = codeChallenge,
                Scope = scopes
            };

            var authUrl = loginRequest.ToUri();
            _logger.LogInformation("Opening browser for Spotify authorization: {Url}", authUrl);

            // Open browser
            OpenBrowser(authUrl.ToString());

            // Wait for callback
            var authCode = await _httpServer.WaitForCallbackAsync(_config.SpotifyRedirectUri, cancellationToken);

            if (string.IsNullOrEmpty(authCode))
            {
                _logger.LogWarning("Authorization failed or was cancelled");
                return false;
            }

            // Exchange code for tokens
            await ExchangeCodeForTokensAsync(authCode);

            _logger.LogInformation("Successfully completed Spotify OAuth flow");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Spotify authorization");
            throw;
        }
    }

    /// <summary>
    /// Exchanges the authorization code for access and refresh tokens.
    /// </summary>
    private async Task ExchangeCodeForTokensAsync(string authCode)
    {
        if (string.IsNullOrEmpty(_currentCodeVerifier))
            throw new InvalidOperationException("Code verifier not set. Call StartAuthorizationAsync first.");

        if (string.IsNullOrEmpty(_config.SpotifyClientId))
            throw new InvalidOperationException("Spotify Client ID is not configured.");

        var tokenRequest = new PKCETokenRequest(_config.SpotifyClientId, authCode, new Uri(_config.SpotifyRedirectUri), _currentCodeVerifier);

        var config = SpotifyClientConfig.CreateDefault();
        var tokenResponse = await new OAuthClient(config).RequestToken(tokenRequest);

        _currentTokenResponse = tokenResponse;
        _tokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

        // Store refresh token securely
        if (_config.SpotifyRememberAuth && !string.IsNullOrEmpty(tokenResponse.RefreshToken))
        {
            await _tokenStorage.SaveRefreshTokenAsync(tokenResponse.RefreshToken);
            _logger.LogInformation("Refresh token stored securely");
        }

        // Create authenticated client
        _authenticatedClient = new SpotifyClient(tokenResponse.AccessToken);

        _logger.LogInformation("Successfully exchanged authorization code for tokens");
    }

    /// <summary>
    /// Refreshes the access token using the stored refresh token.
    /// </summary>
    public async Task RefreshAccessTokenAsync()
    {
        var refreshToken = await _tokenStorage.LoadRefreshTokenAsync();
        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogWarning("No refresh token available");
            throw new InvalidOperationException("No refresh token available. Please sign in again.");
        }

        try
        {
            if (string.IsNullOrEmpty(_config.SpotifyClientId))
                throw new InvalidOperationException("Spotify Client ID is not configured.");

            var tokenRequest = new PKCETokenRefreshRequest(_config.SpotifyClientId, refreshToken);

            var config = SpotifyClientConfig.CreateDefault();
            var tokenResponse = await new OAuthClient(config).RequestToken(tokenRequest);

            _currentTokenResponse = tokenResponse;
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            // Update stored refresh token if a new one was provided
            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                await _tokenStorage.SaveRefreshTokenAsync(tokenResponse.RefreshToken);
            }

            // Create new authenticated client
            _authenticatedClient = new SpotifyClient(tokenResponse.AccessToken);

            _logger.LogInformation("Successfully refreshed access token");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh access token");
            throw new InvalidOperationException("Failed to refresh access token. Please sign in again.", ex);
        }
    }

    /// <summary>
    /// Gets an authenticated Spotify client, refreshing the token if necessary.
    /// </summary>
    /// <returns>Authenticated SpotifyClient</returns>
    public async Task<SpotifyClient> GetAuthenticatedClientAsync()
    {
        // Check if we need to refresh the token
        if (_authenticatedClient == null || DateTime.UtcNow >= _tokenExpiresAt.AddMinutes(-5))
        {
            _logger.LogInformation("Access token expired or about to expire, refreshing...");
            await RefreshAccessTokenAsync();
        }

        if (_authenticatedClient == null)
            throw new InvalidOperationException("Not authenticated. Please sign in first.");

        return _authenticatedClient;
    }

    /// <summary>
    /// Signs out the user and clears stored tokens.
    /// </summary>
    public async Task SignOutAsync()
    {
        await _tokenStorage.DeleteRefreshTokenAsync();
        _authenticatedClient = null;
        _currentTokenResponse = null;
        _currentCodeVerifier = null;

        _logger.LogInformation("User signed out, tokens cleared");
    }

    /// <summary>
    /// Gets the current user's profile information.
    /// </summary>
    public async Task<PrivateUser> GetCurrentUserAsync()
    {
        var client = await GetAuthenticatedClientAsync();
        return await client.UserProfile.Current();
    }

    /// <summary>
    /// Opens the default browser to the specified URL.
    /// </summary>
    private void OpenBrowser(string url)
    {
        try
        {
            // Cross-platform browser opening
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", url);
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", url);
            }
            else
            {
                _logger.LogWarning("Unsupported platform for opening browser");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open browser");
            throw new InvalidOperationException($"Failed to open browser. Please manually navigate to: {url}", ex);
        }
    }
}
