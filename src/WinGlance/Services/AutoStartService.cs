using Microsoft.Win32;

namespace WinGlance.Services;

/// <summary>
/// Manages the Windows auto-start registry entry for WinGlance.
/// Writes to <c>HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run</c>.
/// </summary>
internal static class AutoStartService
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "WinGlance";

    /// <summary>
    /// Enables or disables auto-start by adding/removing the registry entry.
    /// </summary>
    public static void SetAutoStart(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
            return;

        if (enabled)
        {
            var exePath = Environment.ProcessPath;
            if (exePath is not null)
                key.SetValue(AppName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }

    /// <summary>
    /// Returns true if WinGlance is configured to auto-start.
    /// </summary>
    public static bool IsAutoStartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(AppName) is not null;
    }
}
