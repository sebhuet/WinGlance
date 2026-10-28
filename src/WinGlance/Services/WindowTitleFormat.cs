using WinGlance.Models;

namespace WinGlance.Services;

/// <summary>
/// Builds the text shown under a thumbnail from a template set in Settings, e.g.
/// "{project} - {document}". The full window title stays in the tooltip.
/// </summary>
internal static class WindowTitleFormat
{
    /// <summary>
    /// The default template: the project first, so that it stays readable when the text is cut
    /// ("file - Folder - Visual Studio Code" → "Folder - file").
    /// </summary>
    public const string Default = "{project} - {document}";

    // Separators of window titles ("file - Folder - Visual Studio Code")
    private static readonly string[] Separators = [" - ", " — ", " – ", " | "];

    // Characters left alone at either end once a field came out empty ("{project} - {document}" without project)
    private static readonly char[] Dangling = [' ', '·', '-', '|', '—', '–'];

    /// <summary>
    /// Replaces <c>{title}</c>, <c>{project}</c>, <c>{document}</c> and <c>{app}</c> in the
    /// template (case-insensitive). A document equal to the project is not repeated, separators
    /// left at either end are removed, and a blank result falls back to the window title.
    /// </summary>
    public static string Apply(string? template, TrackedWindow window)
    {
        if (string.IsNullOrWhiteSpace(template))
            return window.Title;

        var project = ProjectOf(window);
        var document = DocumentOf(window.Title);
        // A terminal titled after its folder would read "api-server - api-server"
        if (string.Equals(document, project, StringComparison.OrdinalIgnoreCase))
            document = string.Empty;

        var text = template
            .Replace("{title}", window.Title, StringComparison.OrdinalIgnoreCase)
            .Replace("{project}", project, StringComparison.OrdinalIgnoreCase)
            .Replace("{document}", document, StringComparison.OrdinalIgnoreCase)
            .Replace("{app}", window.DisplayName, StringComparison.OrdinalIgnoreCase)
            .Trim(Dangling);

        // A template whose fields are all empty must not leave the thumbnail unnamed
        return text.Length == 0 ? window.Title : text;
    }

    /// <summary>
    /// The project of a window: the folder of its coding agent when there is one, otherwise the
    /// title segment before the application name ("file - Folder - Visual Studio Code" → "Folder").
    /// Empty for a title of one or two segments ("Untitled - Notepad"), which names no project.
    /// </summary>
    public static string ProjectOf(TrackedWindow window)
    {
        if (!string.IsNullOrWhiteSpace(window.Agent?.Project))
            return window.Agent!.Project!;

        var segments = Segments(window.Title);
        return segments.Length >= 3 ? segments[^2] : string.Empty;
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
