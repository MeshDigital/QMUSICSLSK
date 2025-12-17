using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SLSKDONET.Services;

/// <summary>
/// Temporary HTTP server for OAuth callback handling.
/// Listens on localhost for the OAuth redirect and extracts the authorization code.
/// </summary>
public class LocalHttpServer : IDisposable
{
    private readonly ILogger<LocalHttpServer> _logger;
    private HttpListener? _listener;
    private bool _disposed;

    public LocalHttpServer(ILogger<LocalHttpServer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Starts the HTTP server and waits for the OAuth callback.
    /// </summary>
    /// <param name="redirectUri">The redirect URI to listen on (e.g., http://localhost:5000/callback)</param>
    /// <param name="cancellationToken">Cancellation token to stop waiting</param>
    /// <returns>The authorization code from the callback, or null if cancelled/failed</returns>
    public async Task<string?> WaitForCallbackAsync(string redirectUri, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
            throw new ArgumentException("Redirect URI cannot be null or empty", nameof(redirectUri));

        try
        {
            _listener = new HttpListener();
            
            // 1. Ensure the prefix ends with a slash (required by HttpListener)
            var listenerUri = redirectUri;
            if (!listenerUri.EndsWith("/")) 
                listenerUri += "/";
            
            // 2. Add the primary prefix
            _listener.Prefixes.Add(listenerUri);
            
            // 3. Add both localhost and 127.0.0.1 to handle redirect hostname variations
            if (listenerUri.Contains("localhost"))
            {
                _listener.Prefixes.Add(listenerUri.Replace("localhost", "127.0.0.1"));
            }
            else if (listenerUri.Contains("127.0.0.1"))
            {
                _listener.Prefixes.Add(listenerUri.Replace("127.0.0.1", "localhost"));
            }

            _listener.Start();
            _logger.LogInformation("OAuth callback server listening on prefixes: {Prefixes}", 
                string.Join(", ", _listener.Prefixes));

            // Wait for the callback request
            var contextTask = _listener.GetContextAsync();
            var completedTask = await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, cancellationToken));

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("OAuth callback cancelled by user or timeout");
                return null;
            }

            var context = await contextTask;
            var request = context.Request;
            var response = context.Response;

            _logger.LogInformation("Received request from {RemoteEndPoint} for {Url}", 
                request.RemoteEndPoint, request.Url);

            // Extract authorization code or error
            var code = request.QueryString["code"];
            var error = request.QueryString["error"];
            var errorDescription = request.QueryString["error_description"];

            // Send response to browser
            string htmlResponse;
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError("OAuth error: {Error} - {Description}", error, errorDescription);
                htmlResponse = GetErrorHtml(error, errorDescription);
                response.StatusCode = 400;
            }
            else if (!string.IsNullOrEmpty(code))
            {
                _logger.LogInformation("Successfully received authorization code");
                htmlResponse = GetSuccessHtml();
                response.StatusCode = 200;
            }
            else
            {
                _logger.LogWarning("OAuth callback received without code or error");
                htmlResponse = GetErrorHtml("invalid_request", "No authorization code received");
                response.StatusCode = 400;
            }

            // Write response to browser
            var buffer = Encoding.UTF8.GetBytes(htmlResponse);
            response.ContentLength64 = buffer.Length;
            response.ContentType = "text/html; charset=utf-8";
            await response.OutputStream.WriteAsync(buffer, cancellationToken);
            response.Close();

            return !string.IsNullOrEmpty(error) ? null : code;
        }
        catch (HttpListenerException ex)
        {
            _logger.LogError(ex, "Failed to start OAuth callback server. Port may be in use.");
            throw new InvalidOperationException($"Failed to start OAuth callback server: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OAuth callback handling");
            throw;
        }
        finally
        {
            Stop();
        }
    }

    /// <summary>
    /// Stops the HTTP server.
    /// </summary>
    public void Stop()
    {
        if (_listener?.IsListening == true)
        {
            _listener.Stop();
            _logger.LogInformation("OAuth callback server stopped");
        }
    }

    private static string GetSuccessHtml()
    {
        return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Authorization Successful</title>
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
        }
        .container {
            background: white;
            padding: 40px;
            border-radius: 10px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.2);
            text-align: center;
            max-width: 400px;
        }
        h1 {
            color: #1DB954;
            margin-bottom: 20px;
        }
        p {
            color: #666;
            line-height: 1.6;
        }
        .checkmark {
            font-size: 64px;
            color: #1DB954;
            margin-bottom: 20px;
        }
    </style>
</head>
<body>
    <div class='container'>
        <div class='checkmark'>✓</div>
        <h1>Success!</h1>
        <p>You have successfully signed in with Spotify.</p>
        <p>You can close this window and return to QMUSICSLSK.</p>
    </div>
</body>
</html>";
    }

    private static string GetErrorHtml(string error, string? description)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Authorization Failed</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
        }}
        .container {{
            background: white;
            padding: 40px;
            border-radius: 10px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.2);
            text-align: center;
            max-width: 400px;
        }}
        h1 {{
            color: #f5576c;
            margin-bottom: 20px;
        }}
        p {{
            color: #666;
            line-height: 1.6;
        }}
        .error-icon {{
            font-size: 64px;
            color: #f5576c;
            margin-bottom: 20px;
        }}
        .error-details {{
            background: #f8f8f8;
            padding: 15px;
            border-radius: 5px;
            margin-top: 20px;
            font-size: 14px;
            color: #999;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='error-icon'>✗</div>
        <h1>Authorization Failed</h1>
        <p>There was a problem signing in with Spotify.</p>
        <p>Please close this window and try again in QMUSICSLSK.</p>
        <div class='error-details'>
            <strong>Error:</strong> {error}<br>
            {(string.IsNullOrEmpty(description) ? "" : $"<strong>Details:</strong> {description}")}
        </div>
    </div>
</body>
</html>";
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _listener?.Close();
        _disposed = true;
    }
}
