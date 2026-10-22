using WinGlance.NativeApi;

namespace WinGlance.Services;

/// <summary>
/// Recognizes the shells that Claude Code's Bash and PowerShell tools start for each command,
/// to tell whether an idle session still runs background commands. <c>claude agents --json</c>
/// reports a session as idle as soon as its turn ends, even while commands it started in the
/// background (a dev server, a browser automation) keep running.
/// </summary>
/// <remarks>
/// Heuristic: the markers are internal to Claude Code, not a documented interface. If they
/// change, nothing is counted and badges fall back to the status Claude reports.
/// </remarks>
internal static class ClaudeToolShells
{
    // Bash tool:       bash.exe -c "source ~/.claude/shell-snapshots/snapshot-bash-….sh …"
    // PowerShell tool: cmd.exe /d /s /c "chcp 65001 & powershell … $__claudeCodeScript = $env:CLAUDE_CODE_SHELL_LAUNCHER_SCRIPT …"
    // MCP servers, also children of claude.exe, carry neither.
    private static readonly string[] Markers = ["shell-snapshots", "__claudeCodeScript", "CLAUDE_CODE_SHELL_LAUNCHER_SCRIPT"];

    /// <summary>
    /// Counts the tool commands a Claude process is running: its direct children whose
    /// command line carries a tool-shell marker.
    /// </summary>
    /// <param name="getCommandLine">Reads a process command line; null when unreadable.</param>
    public static int CountRunning(ProcessTree tree, int claudeProcessId, Func<int, string?> getCommandLine) =>
        tree.GetChildren(claudeProcessId).Count(child => IsToolShell(getCommandLine(child.ProcessId)));

    /// <summary>Counts with command lines read from the live processes.</summary>
    public static int CountRunning(ProcessTree tree, int claudeProcessId) =>
        CountRunning(tree, claudeProcessId, NativeMethods.TryGetProcessCommandLine);

    /// <summary>True if the command line is one of a Claude Code tool shell.</summary>
    internal static bool IsToolShell(string? commandLine) =>
        commandLine is not null && Markers.Any(marker => commandLine.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
