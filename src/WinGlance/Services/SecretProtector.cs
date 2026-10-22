using System.Text;
using WinGlance.NativeApi;

namespace WinGlance.Services;

/// <summary>
/// Encrypts secrets (the LLM API key) for storage in config.json with DPAPI, so that only
/// the same Windows user on the same machine can read them back.
/// </summary>
internal static class SecretProtector
{
    /// <summary>Encrypts a secret to base64; an empty secret gives an empty string.</summary>
    public static string Protect(string secret)
    {
        if (string.IsNullOrEmpty(secret))
            return string.Empty;

        var encrypted = NativeMethods.ProtectForCurrentUser(Encoding.UTF8.GetBytes(secret));
        return encrypted is null ? string.Empty : Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// Decrypts a value from <see cref="Protect"/>. Returns null when it is not valid base64 or
    /// was encrypted by another user or machine (config.json copied with the portable exe).
    /// </summary>
    public static string? Unprotect(string protectedSecret)
    {
        if (string.IsNullOrEmpty(protectedSecret))
            return string.Empty;

        try
        {
            var decrypted = NativeMethods.UnprotectForCurrentUser(Convert.FromBase64String(protectedSecret));
            return decrypted is null ? null : Encoding.UTF8.GetString(decrypted);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
