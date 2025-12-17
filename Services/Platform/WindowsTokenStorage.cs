using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SLSKDONET.Services.Platform;

/// <summary>
/// Windows-specific secure token storage using DPAPI (Data Protection API).
/// Encrypts tokens using the current user's credentials.
/// </summary>
public class WindowsTokenStorage : ISecureTokenStorage
{
    private readonly ILogger<WindowsTokenStorage> _logger;
    private readonly string _tokenFilePath;

    public WindowsTokenStorage(ILogger<WindowsTokenStorage> logger)
    {
        _logger = logger;
        
        // Store in user's AppData\Local folder
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, "ORBIT");
        Directory.CreateDirectory(appFolder);
        
        _tokenFilePath = Path.Combine(appFolder, "spotify_token.dat");
    }

    public async Task SaveRefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException("Refresh token cannot be null or empty", nameof(refreshToken));

        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Encrypt using DPAPI
                var tokenBytes = Encoding.UTF8.GetBytes(refreshToken);
                var encryptedBytes = ProtectedData.Protect(
                    tokenBytes,
                    null, // optionalEntropy
                    DataProtectionScope.CurrentUser
                );

                // Write to file
                await File.WriteAllBytesAsync(_tokenFilePath, encryptedBytes);
                _logger.LogInformation("Refresh token saved securely using DPAPI");
            }
            else
            {
                 _logger.LogWarning("Secure storage not implemented for this platform yet");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save refresh token");
            throw new InvalidOperationException("Failed to save refresh token securely", ex);
        }
    }

    public async Task<string?> LoadRefreshTokenAsync()
    {
        if (!File.Exists(_tokenFilePath))
        {
            _logger.LogDebug("No stored refresh token found");
            return null;
        }

        try
        {
            // Read encrypted data
            var encryptedBytes = await File.ReadAllBytesAsync(_tokenFilePath);

            string refreshToken;
            
            if (OperatingSystem.IsWindows())
            {
                // Decrypt using DPAPI
                var tokenBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null, // optionalEntropy
                    DataProtectionScope.CurrentUser
                );
                refreshToken = Encoding.UTF8.GetString(tokenBytes);
            }
            else
            {
                // TODO: Implement non-Windows decryption
                 _logger.LogWarning("Secure storage not implemented for this platform yet");
                 return null;
            }
            
            _logger.LogInformation("Refresh token loaded successfully");
            return refreshToken;
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to decrypt refresh token (may be corrupted or from different user)");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load refresh token");
            return null;
        }
    }

    public async Task DeleteRefreshTokenAsync()
    {
        try
        {
            if (File.Exists(_tokenFilePath))
            {
                File.Delete(_tokenFilePath);
                _logger.LogInformation("Refresh token deleted");
            }
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete refresh token");
            throw new InvalidOperationException("Failed to delete refresh token", ex);
        }
    }
}
