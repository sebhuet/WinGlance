using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Windows;

namespace WinGlance.Tests.Themes;

/// <summary>
/// The light/dark theme only works if views take their neutral colors from the theme
/// dictionaries: these tests read the XAML sources to keep it that way.
/// </summary>
public partial class ThemeTests
{
    // Colors that mean the same thing in both themes: status badges and the close button
    private static readonly HashSet<string> SemanticColors = new(StringComparer.OrdinalIgnoreCase)
    {
        "#CC555555", "#CC0078D7", "#CCFF8C00", "#CC00897B", "#CCE81123", "#FFE81123", "#FFF1707A",
    };

    [GeneratedRegex(@"\{DynamicResource (\w+)\}")]
    private static partial Regex DynamicResourceKey();

    [GeneratedRegex(@"""(#[0-9A-Fa-f]{6,8})""")]
    private static partial Regex HardcodedColor();

    private static string SourceDirectory([CallerFilePath] string testFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", "WinGlance"));

    private static IEnumerable<string> ViewFiles() =>
        Directory.GetFiles(SourceDirectory(), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Themes{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    private static HashSet<string> ThemeKeys(string themeFile)
    {
        HashSet<string> keys = [];
        RunOnSta(() =>
        {
            var dictionary = (ResourceDictionary)Application.LoadComponent(
                new Uri($"/WinGlance;component/Themes/{themeFile}", UriKind.Relative));
            keys = dictionary.Keys.Cast<string>().ToHashSet();
        });
        return keys;
    }

    [Fact]
    public void LightAndDarkThemes_DefineTheSameKeys()
    {
        Assert.Equal(ThemeKeys("DarkTheme.xaml").Order(), ThemeKeys("LightTheme.xaml").Order());
    }

    [Fact]
    public void EveryDynamicResourceUsedByAView_ExistsInBothThemes()
    {
        var dark = ThemeKeys("DarkTheme.xaml");
        var light = ThemeKeys("LightTheme.xaml");

        var used = ViewFiles()
            .SelectMany(f => DynamicResourceKey().Matches(File.ReadAllText(f)).Select(m => (File: Path.GetFileName(f), Key: m.Groups[1].Value)))
            .ToList();

        Assert.NotEmpty(used);
        Assert.All(used, u =>
        {
            Assert.True(dark.Contains(u.Key), $"{u.File}: '{u.Key}' missing from DarkTheme.xaml");
            Assert.True(light.Contains(u.Key), $"{u.File}: '{u.Key}' missing from LightTheme.xaml");
        });
    }

    [Fact]
    public void Views_HardcodeOnlySemanticColors()
    {
        var hardcoded = ViewFiles()
            .SelectMany(f => HardcodedColor().Matches(File.ReadAllText(f)).Select(m => $"{Path.GetFileName(f)}: {m.Groups[1].Value}"))
            .Where(entry => !SemanticColors.Contains(entry[(entry.IndexOf(": ", StringComparison.Ordinal) + 2)..]))
            .ToList();

        Assert.Empty(hardcoded); // a neutral color here would ignore the light theme
    }

    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }
}
