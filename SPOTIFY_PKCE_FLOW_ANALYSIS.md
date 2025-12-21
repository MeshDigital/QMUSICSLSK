# Spotify PKCE Flow Analysis - End-to-End

## Current State Assessment
**Status:** ❌ **BLOCKED** - Authentication flow is getting stuck

## The Complete Flow (As Designed)

### 1. User Clicks "Connect with Spotify"
```
SettingsPage.axaml → ConnectSpotifyCommand → SettingsViewModel.ConnectSpotifyAsync()
```

**What happens:**
- Sets `IsAuthenticating = true` (shows progress bar)
- Saves config
- Calls `SpotifyAuthService.StartAuthorizationAsync()`

### 2. PKCE Parameter Generation
```csharp
_currentCodeVerifier = PKCEHelper.GenerateCodeVerifier();  // 128 chars, base64url
var codeChallenge = PKCEHelper.GenerateCodeChallenge(_currentCodeVerifier);  // SHA256 hash
```

✅ **This is correct** - Generates cryptographically secure 96-byte random verifier

### 3. Authorization URL Construction
```csharp
var loginRequest = new LoginRequest(
    new Uri(_config.SpotifyRedirectUri),  // http://127.0.0.1:5000/callback
    _config.SpotifyClientId,
    LoginRequest.ResponseType.Code)
{
    CodeChallengeMethod = "S256",
    CodeChallenge = codeChallenge,
    Scope = scopes  // user-read-private, user-read-email, etc.
};
```

✅ **This is correct** - Proper PKCE challenge with S256 method

### 4. Browser Opens
```csharp
OpenBrowser(authUrl.ToString());
```

✅ **This works** - Browser opens to Spotify login

### 5. LocalHttpServer Starts Listening
```csharp
await _httpServer.WaitForCallbackAsync(_config.SpotifyRedirectUri, timeoutCts.Token);
```

**Current Implementation:**
```csharp
var prefix = $"http://localhost:{port}/";
var prefixIp = $"http://127.0.0.1:{port}/";
_listener.Prefixes.Add(prefix);
_listener.Prefixes.Add(prefixIp);
```

⚠️ **POTENTIAL ISSUE #1: Redirect URI Mismatch**
- We send Spotify: `http://127.0.0.1:5000/callback`
- We listen on: `http://localhost:5000/` AND `http://127.0.0.1:5000/`
- This should work, but...

### 6. Callback Path Validation
```csharp
var path = request.Url?.AbsolutePath?.TrimEnd('/') ?? string.Empty;
var isCallback = path.Equals("/callback", StringComparison.OrdinalIgnoreCase) 
    || path.EndsWith("/callback", StringComparison.OrdinalIgnoreCase);

if (!isCallback && string.IsNullOrEmpty(request.QueryString["code"]))
{
    response.StatusCode = 404;
    response.Close();
    continue;  // ⚠️ LOOP CONTINUES, NEVER RETURNS
}
```

🔴 **CRITICAL BUG #1: The 404 Path**
If the path doesn't match AND there's no code parameter, we return 404 but **continue the loop** instead of breaking out. This means:
- Browser shows 404
- App keeps waiting for another request that never comes
- User sees "Authentication Active..." forever
- Timeout eventually fires after 2 minutes

### 7. Authorization Code Extraction
```csharp
var code = request.QueryString["code"];
var error = request.QueryString["error"];
```

✅ **This is correct**

### 8. Token Exchange
```csharp
var tokenRequest = new PKCETokenRequest(
    _config.SpotifyClientId, 
    authCode, 
    new Uri(_config.SpotifyRedirectUri),  // Must match exactly
    _currentCodeVerifier  // Must match the challenge
);
var tokenResponse = await oauthClient.RequestToken(tokenRequest);
```

⚠️ **POTENTIAL ISSUE #2: Redirect URI in Token Exchange**
- Spotify requires the redirect_uri in the token request to **exactly match** what was sent in the auth request
- We're using `_config.SpotifyRedirectUri` which is `http://127.0.0.1:5000/callback`
- If Spotify normalized this to `http://localhost:5000/callback` during auth, the token exchange will fail with `redirect_uri_mismatch`

### 9. Success/Failure Response to Browser
```csharp
var buffer = Encoding.UTF8.GetBytes(htmlResponse);
response.ContentLength64 = buffer.Length;
response.ContentType = "text/html; charset=utf-8";
await response.OutputStream.WriteAsync(buffer, cancellationToken);
response.Close();

return !string.IsNullOrEmpty(error) ? null : code;
```

✅ **This is correct**

---

## Identified Blocking Points

### 🔴 BLOCK #1: Timeout in Wrong Place
```csharp
// In LocalHttpServer.WaitForCallbackAsync:
using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
timeoutCts.CancelAfter(TimeSpan.FromMinutes(2));

while (!timeoutCts.IsCancellationRequested)
{
    var contextTask = _listener.GetContextAsync();
    var completedTask = await Task.WhenAny(contextTask, Task.Delay(-1, timeoutCts.Token));
    
    if (timeoutCts.IsCancellationRequested)
    {
        return null;  // ⚠️ Returns null, but doesn't throw
    }
}
```

**Problem:** If timeout fires, we return `null`, which makes `StartAuthorizationAsync` think the user cancelled rather than it timing out.

### 🔴 BLOCK #2: Multiple HttpListener Instances
```csharp
// Each call to ConnectSpotifyAsync creates a NEW LocalHttpServer
// If previous instance is still listening on port 5000:
_listener.Start();  // ❌ HttpListenerException: Address already in use
```

**Problem:** If auth fails and user clicks "Restart auth", the old listener might still be running.

### 🔴 BLOCK #3: IsAuthenticating Never Resets on Crash
```csharp
private async Task ConnectSpotifyAsync()
{
    try
    {
        IsAuthenticating = true;
        var success = await _spotifyAuthService.StartAuthorizationAsync();
        // ...
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Spotify connection failed");
        // ❌ No user notification, just logged
    }
    finally
    {
        IsAuthenticating = false;  // ✅ This does reset
    }
}
```

**Problem:** If an exception occurs in `StartAuthorizationAsync`, the user sees the error in logs but UI just says "Authentication Active..." disappeared.

### 🔴 BLOCK #4: Port Conflict on Restart
```csharp
// AppConfig default:
public string SpotifyRedirectUri { get; set; } = "http://127.0.0.1:5000/callback";
public int SpotifyCallbackPort { get; set; } = 5000;
```

**Problem:** If another process uses port 5000, auth will fail immediately. No fallback ports.

---

## Recommended Fixes (Priority Order)

### FIX #1: Add Proper Timeout Handling
```csharp
// In LocalHttpServer.WaitForCallbackAsync
if (timeoutCts.IsCancellationRequested)
{
    _logger.LogError("OAuth callback timed out after 2 minutes");
    throw new TimeoutException("No callback received from Spotify. Please try again.");
}
```

### FIX #2: Ensure Listener Cleanup
```csharp
// In LocalHttpServer
public void Stop()
{
    if (_listener?.IsListening == true)
    {
        try
        {
            _listener.Stop();
            _listener.Close();  // ✅ Add explicit close
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping listener");
        }
        finally
        {
            _listener = null;  // ✅ Clear reference
        }
    }
}
```

### FIX #3: Single HttpListener Instance (Singleton Pattern)
Make `LocalHttpServer` a singleton service that can be reused across auth attempts:
```csharp
// Register in DI:
services.AddSingleton<LocalHttpServer>();
```

### FIX #4: Better Error Feedback
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Spotify connection failed");
    SpotifyDisplayName = $"Error: {ex.Message}";  // ✅ Show in UI
    IsSpotifyConnected = false;
}
```

### FIX #5: Port Fallback Strategy
```csharp
private async Task<int> FindAvailablePortAsync(int preferredPort)
{
    var ports = new[] { preferredPort, 5000, 5001, 8080, 8888 };
    foreach (var port in ports)
    {
        try
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();
            listener.Stop();
            return port;  // ✅ Port is available
        }
        catch (HttpListenerException)
        {
            continue;  // Try next port
        }
    }
    throw new InvalidOperationException("No available ports for OAuth callback");
}
```

---

## Critical Question: Spotify Redirect URI Whitelist

❓ **Have you registered the redirect URI in Spotify Dashboard?**

Go to: https://developer.spotify.com/dashboard
1. Select your app
2. Click "Edit Settings"
3. Check "Redirect URIs" section
4. Must contain **exactly**: `http://127.0.0.1:5000/callback`
   - ❌ NOT `http://localhost:5000/callback`
   - ❌ NOT `http://127.0.0.1:5000`
   - ❌ NOT `http://127.0.0.1:5000/callback/`

If the URI doesn't match **character-for-character**, Spotify will show an error page instead of redirecting.

---

## Testing Checklist

- [ ] Verify Spotify Dashboard has exact redirect URI
- [ ] Test with port 5000 free (close all other apps)
- [ ] Clear cached tokens before testing
- [ ] Watch console logs during auth flow
- [ ] Test clicking "Restart auth" during active auth
- [ ] Test what happens when browser is closed without completing auth
- [ ] Verify LocalHttpServer stops after success/failure

---

## Current Flow Status

```
User clicks "Connect" 
  → IsAuthenticating = true ✅
  → Browser opens ✅
  → Spotify login page shows ✅
  → User approves ✅
  → Spotify redirects to http://127.0.0.1:5000/callback?code=xxx ❓
  → LocalHttpServer receives request ❓
    → Path matches "/callback" ❓
    → Extracts code ❓
    → Returns code to StartAuthorizationAsync ❓
  → Token exchange happens ❓
  → IsAuthenticating = false ❓
  → UI updates to "Connected" ❓
```

**Next step:** Run the app with verbose logging and watch exactly where it stops.
