using System;
using System.Security.Cryptography;
using System.Text;

namespace SLSKDONET.Services;

/// <summary>
/// Provides methods to encrypt and decrypt data using the Windows Data Protection API (DPAPI).
/// The data is encrypted for the current user account, so only this user can decrypt it.
/// </summary>
public class ProtectedDataService
{
    // Optional: A salt to make the encryption more secure.
    private static readonly byte[] s_entropy = Encoding.Unicode.GetBytes("SLSKDONET_Salt_Value");

    public string? Protect(string? data)
    {
        if (string.IsNullOrEmpty(data))
            return null;

        byte[] dataBytes = Encoding.Unicode.GetBytes(data);
        byte[] encryptedBytes = ProtectedData.Protect(dataBytes, s_entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encryptedBytes);
    }

    public string? Unprotect(string? encryptedData)
    {
        if (string.IsNullOrEmpty(encryptedData))
            return null;

        byte[] encryptedBytes = Convert.FromBase64String(encryptedData);
        byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, s_entropy, DataProtectionScope.CurrentUser);
        return Encoding.Unicode.GetString(decryptedBytes);
    }
}