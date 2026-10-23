using WinGlance.Models;

namespace WinGlance.Services;

/// <summary>
/// Builds the text shown under a thumbnail from a template set in Settings, e.g.
/// "{project} · {document}". The full window title stays in the tooltip.
/// </summary>
internal static class WindowTitleFormat
{
    /// <summary>The default template: the window title, as before templates existed.</summary>
    public const string Default = "{title}";

    // Separators of window titles ("file - Folder - Visual Studio Code")
    private static readonly string[] Separators = [" - ", " — ", " – ", " | "];

    /// <summary>
    /// Replaces <c>{title}</c>, <c>{project}</c>, <c>{document}</c> and <c>{app}</c> in the
    /// template (case-insensitive). Falls back to the window title when the result is blank.
    /// </summary>
    public static string Apply(string? template, TrackedWindow window)
    {
        if (string.IsNullOrWhiteSpace(template))
            return window.Title;

        var text = template
            .Replace("{title}", window.Title, StringComparison.OrdinalIgnoreCase)
            .Replace("{project}", ProjectOf(window), StringComparison.OrdinalIgnoreCase)
            .Replace("{document}", DocumentOf(window.Title), StringComparison.OrdinalIgnoreCase)
            .Replace("{app}", window.DisplayName, StringComparison.OrdinalIgnoreCase)
            .Trim();

        // A template whose fields are all empty must not leave the thumbnail unnamed
        return text.Trim(' ', '·', '-', '|', '—', '–').Length == 0 ? window.Title : text;
    }

    /// <summary>
    /// The project of a window: the folder of its coding agent when there is one, otherwise the
    /// title segment before the application name ("file - Folder - Visual Studio Code" → "Folder"),
    /// or the whole title when it has a single segment.
    /// </summary>
    public static string ProjectOf(TrackedWindow window)
    {
        if (!string.IsNullOrWhiteSpace(window.Agent?.Project))
            return window.Agent!.Project!;

        var segments = Segments(window.Title);
        return segments.Length switch
        {
            0 => string.Empty,
            1 => segments[0],
            2 => segments[0],
            _ => segments[^2],
        };
    }

    /// <summary>The first segment of a title: the open file or document ("Readme.md - Folder - App" → "Readme.md").</summary>
    public static string DocumentOf(string title)
    {
        var segments = Segments(title);
        return segments.Length == 0 ? string.Empty : segments[0];
    }

    private static string[] Segments(string title) =>
        title.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.TrimStart('●', ' ')) // VS Code's unsaved-changes dot
            .Where(s => s.Length > 0)
            .ToArray();
}
