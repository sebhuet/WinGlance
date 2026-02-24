using System.Windows;
using Microsoft.Win32;

namespace WinGlance.Services;

/// <summary>
/// Detects the Windows theme (light/dark) and applies the matching
/// <see cref="ResourceDictionary"/> to the application. Listens for
/// real-time theme changes via <see cref="SystemEvents.UserPreferenceChanged"/>.
/// </summary>
internal sealed class ThemeService : IDisposable
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string RegistryKey = "AppsUseLightTheme";

    private static readonly Uri DarkThemeUri = new("Themes/DarkTheme.xaml", UriKind.Relative);
    private static readonly Uri LightThemeUri = new("Themes/LightTheme.xaml", UriKind.Relative);

    private ResourceDictionary? _currentTheme;

    /// <summary>
    /// Initializes the theme service: detects current theme and subscribes to changes.
    /// </summary>
    public void Initialize()
    {
        ApplyTheme(IsLightTheme());
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    /// <summary>
    /// Returns true if the current Windows theme is "light".
    /// </summary>
    internal static bool IsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            var value = key?.GetValue(RegistryKey);
            return value is int i && i == 1;
        }
        catch
        {
            return false; // default to dark
        }
    }

    private void ApplyTheme(bool isLight)
    {
        var uri = isLight ? LightThemeUri : DarkThemeUri;
        var newTheme = new ResourceDictionary { Source = uri };

        var mergedDictionaries = Application.Current.Resources.MergedDictionaries;
        if (_currentTheme is not null)
            mergedDictionaries.Remove(_currentTheme);

        mergedDictionaries.Add(newTheme);
        _currentTheme = newTheme;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            Application.Current.Dispatcher.Invoke(() => ApplyTheme(IsLightTheme()));
        }
    }
}
