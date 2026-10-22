using System.IO;
using WinGlance.Models;

namespace WinGlance.Services;

/// <summary>
/// Associates coding agent sessions with the tracked windows that host them.
/// </summary>
internal static class AgentWindowMatcher
{
    // Separators used in window titles ("file - Folder - Visual Studio Code")
    private static readonly string[] TitleSeparators = [" - ", " — ", " – ", " | "];

    private const string WorkspaceSuffix = " (Workspace)";

    /// <summary>
    /// Returns the badge of every window hosting at least one session, keyed by HWND.
    /// </summary>
    public static Dictionary<IntPtr, AgentBadge> Match(
        IReadOnlyList<TrackedWindow> windows,
        IReadOnlyList<AgentSession> sessions,
        ProcessTree tree)
    {
        var windowsByProcess = windows
            .Where(w => w.ProcessId != 0)
            .GroupBy(w => (int)w.ProcessId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var sessionsByWindow = new Dictionary<IntPtr, List<AgentSession>>();
        foreach (var session in sessions)
        {
            var host = FindHostWindow(session, tree, windowsByProcess);
            if (host is null)
                continue;

            if (!sessionsByWindow.TryGetValue(host.Hwnd, out var hosted))
                sessionsByWindow[host.Hwnd] = hosted = [];
            hosted.Add(session);
        }

        return sessionsByWindow.ToDictionary(kv => kv.Key, kv => AgentBadge.FromSessions(kv.Value)!);
    }

    /// <summary>
    /// Walks the session's process lineage up to the first process owning a tracked window.
    /// When several windows share that process (one VS Code or Windows Terminal process serves
    /// all its windows), the session's working directory is matched against their titles;
    /// an unresolved tie leaves the session unassigned rather than guessing.
    /// </summary>
    internal static TrackedWindow? FindHostWindow(
        AgentSession session,
        ProcessTree tree,
        IReadOnlyDictionary<int, List<TrackedWindow>> windowsByProcess)
    {
        foreach (var processId in tree.GetLineage(session.ProcessId))
        {
            if (!windowsByProcess.TryGetValue(processId, out var candidates))
                continue;

            return candidates.Count == 1
                ? candidates[0]
                : PickByWorkingDirectory(candidates, session.WorkingDirectory);
        }

        return null;
    }

    /// <summary>
    /// Returns the window whose title best designates the directory, or null on no match or a tie.
    /// </summary>
    internal static TrackedWindow? PickByWorkingDirectory(
        IReadOnlyList<TrackedWindow> candidates,
        string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
            return null;

        TrackedWindow? best = null;
        var bestScore = 0;
        var isTie = false;

        foreach (var window in candidates)
        {
            var score = ScoreTitle(window.Title, workingDirectory);
            if (score > bestScore)
            {
                best = window;
                bestScore = score;
                isTie = false;
            }
            else if (score == bestScore && score > 0)
            {
                isTie = true;
            }
        }

        return isTie ? null : best;
    }

    /// <summary>
    /// Scores how well a window title designates a directory. The full path scores highest
    /// (terminal titles often show it), then the folder name as a title segment (VS Code shows
    /// "file - Folder - Visual Studio Code"); parent folders score lower, so that an agent
    /// started in a subfolder still finds its workspace window. 0 means no match.
    /// </summary>
    internal static int ScoreTitle(string title, string workingDirectory)
    {
        var path = Path.TrimEndingDirectorySeparator(workingDirectory.Trim());
        if (path.Length == 0 || string.IsNullOrWhiteSpace(title))
            return 0;

        if (ContainsPath(title, path))
            return 1000;

        var segments = title
            .Split(TitleSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeTitleSegment)
            .ToList();

        var folders = path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);

        // From the leaf folder up; index 0 (drive or UNC server) never names a project
        for (int i = folders.Length - 1, depth = 0; i >= 1; i--, depth++)
        {
            var folder = folders[i];
            if (segments.Any(s => s.Equals(folder, StringComparison.OrdinalIgnoreCase)))
                return Math.Max(100 - (depth * 10), 1);
        }

        return 0;
    }

    /// <summary>
    /// True if the title contains the path followed by a boundary, so that "C:\proj"
    /// does not match a title showing "C:\project".
    /// </summary>
    private static bool ContainsPath(string title, string path)
    {
        var index = title.IndexOf(path, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            var end = index + path.Length;
            if (end == title.Length || !IsPathNameChar(title[end]))
                return true;
            index = title.IndexOf(path, index + 1, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsPathNameChar(char c) => char.IsLetterOrDigit(c) || c is '-' or '_' or '.';

    /// <summary>Strips VS Code decorations: the unsaved-changes dot and the "(Workspace)" suffix.</summary>
    private static string NormalizeTitleSegment(string segment)
    {
        var normalized = segment.TrimStart('●', ' ');
        return normalized.EndsWith(WorkspaceSuffix, StringComparison.OrdinalIgnoreCase)
            ? normalized[..^WorkspaceSuffix.Length]
            : normalized;
    }
}
