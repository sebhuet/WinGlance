using System.Diagnostics;
using WinGlance.Models;

namespace WinGlance.Services;

/// <summary>
/// Discovers running coding agents (Claude Code, OpenCode) and maps them to tracked windows.
/// Discovery spawns <c>claude agents --json</c>, so it runs in the background at its own,
/// slower cadence; mapping the latest discovery onto windows is cheap and runs on every poll.
/// </summary>
internal sealed class AgentMonitor
{
    private readonly ClaudeAgentSource _claude;
    private readonly OpenCodeAgentSource _openCode = new();
    private readonly TimeSpan _refreshInterval;

    private Discovery? _discovery;
    private long _lastRefreshTimestamp;
    private int _isRefreshing;

    /// <summary>
    /// Creates a monitor that looks for the claude CLI on PATH.
    /// </summary>
    /// <param name="refreshIntervalMs">Delay between two discoveries, clamped to 1–60 s.</param>
    public AgentMonitor(int refreshIntervalMs)
        : this(refreshIntervalMs, new ClaudeAgentSource(ClaudeAgentSource.FindExecutable()))
    {
    }

    /// <summary>Creates a monitor on a given Claude agent source; the public constructor looks for the claude CLI on PATH.</summary>
    internal AgentMonitor(int refreshIntervalMs, ClaudeAgentSource claude)
    {
        _claude = claude;
        _refreshInterval = TimeSpan.FromMilliseconds(Math.Clamp(refreshIntervalMs, 1000, 60000));
    }

    /// <summary>Delay between two discoveries.</summary>
    public TimeSpan RefreshInterval => _refreshInterval;

    /// <summary>
    /// Returns the agent badge of each window hosting a session, keyed by HWND.
    /// Starts a background discovery when the last one is older than the refresh interval;
    /// its result is used from the next call on. Safe to call from any thread.
    /// </summary>
    public Dictionary<IntPtr, AgentBadge> GetBadges(IReadOnlyList<TrackedWindow> windows)
    {
        StartRefreshIfDue();

        var discovery = Volatile.Read(ref _discovery);
        return discovery is null
            ? []
            : AgentWindowMatcher.Match(windows, discovery.Sessions, discovery.Tree);
    }

    private void StartRefreshIfDue()
    {
        var last = Volatile.Read(ref _lastRefreshTimestamp);
        if (last != 0 && Stopwatch.GetElapsedTime(last) < _refreshInterval)
            return;

        // Only one discovery at a time, even if polls overlap
        if (Interlocked.CompareExchange(ref _isRefreshing, 1, 0) != 0)
            return;

        _ = Task.Run(Refresh);
    }

    private void Refresh()
    {
        try
        {
            var tree = ProcessTree.Capture();
            var sessions = new List<AgentSession>();

            // Spawning the CLI is skipped while no claude.exe runs; an npm install runs as
            // node.exe, so it is always queried
            if (_claude.IsScriptShim
                || tree.Processes.Any(p => p.ExeName.Equals("claude.exe", StringComparison.OrdinalIgnoreCase)))
            {
                // "idle" only means the turn ended: count the commands still running in the background
                sessions.AddRange(_claude.GetSessions().Select(session => session.Status == AgentStatus.Idle
                    ? session with { BackgroundTasks = ClaudeToolShells.CountRunning(tree, session.ProcessId) }
                    : session));
            }

            sessions.AddRange(_openCode.GetSessions(tree));
            Volatile.Write(ref _discovery, new Discovery(tree, sessions));
        }
        finally
        {
            // On failure the previous discovery stays in use until the next interval
            Volatile.Write(ref _lastRefreshTimestamp, Stopwatch.GetTimestamp());
            Volatile.Write(ref _isRefreshing, 0);
        }
    }

    private sealed record Discovery(ProcessTree Tree, List<AgentSession> Sessions);
}
