namespace WinGlance.Models;

/// <summary>
/// Aggregated coding agent state of one window, shown as a badge under its thumbnail.
/// A record, so that an unchanged badge compares equal and raises no PropertyChanged.
/// </summary>
/// <param name="Status">Most urgent status among the window's sessions.</param>
/// <param name="Text">Short label, e.g. "Claude · working" or "Claude · permission prompt +1".</param>
/// <param name="Details">Tooltip text with one line per session.</param>
/// <param name="Project">Folder name of the most urgent session (e.g. "BookAmPlus"), or null.</param>
/// <param name="AgentName">Tool of the most urgent session ("Claude", "OpenCode"), or null.</param>
/// <param name="StatusText">Status of the most urgent session ("permission prompt", "idle"…), or null.</param>
public sealed record AgentBadge(
    AgentStatus Status,
    string Text,
    string Details,
    string? Project = null,
    string? AgentName = null,
    string? StatusText = null)
{
    /// <summary>
    /// Builds the badge of a window from its sessions, or returns null when there are none.
    /// </summary>
    internal static AgentBadge? FromSessions(IReadOnlyCollection<AgentSession> sessions)
    {
        if (sessions.Count == 0)
            return null;

        // Most urgent first; OrderByDescending is stable, so ties keep discovery order
        var ordered = sessions.OrderByDescending(s => s.EffectiveStatus).ToList();
        var top = ordered[0];

        var text = $"{KindLabel(top.Kind)} · {StatusLabel(top)}";
        if (ordered.Count > 1)
            text += $" +{ordered.Count - 1}";

        var details = string.Join(Environment.NewLine, ordered.Select(DescribeSession));
        return new AgentBadge(
            top.EffectiveStatus, text, details,
            ProjectName(top.WorkingDirectory), KindLabel(top.Kind), StatusLabel(top));
    }

    /// <summary>Last folder of a working directory ("w:\00_DEV\BookAmPlus" → "BookAmPlus").</summary>
    internal static string? ProjectName(string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
            return null;

        var name = System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(workingDirectory.Trim()));
        return string.IsNullOrEmpty(name) ? null : name;
    }

    /// <summary>Human-readable status, using Claude's own reason for a waiting session.</summary>
    internal static string StatusLabel(AgentSession session) => session switch
    {
        { Status: AgentStatus.Waiting } => string.IsNullOrWhiteSpace(session.WaitingFor) ? "needs input" : session.WaitingFor,
        { Status: AgentStatus.Busy } => "working",
        { Status: AgentStatus.Idle, BackgroundTasks: 1 } => "1 background task",
        { Status: AgentStatus.Idle, BackgroundTasks: > 1 } => $"{session.BackgroundTasks} background tasks",
        { Status: AgentStatus.Idle } => "idle",
        _ => "running",
    };

    private static string KindLabel(AgentKind kind) => kind switch
    {
        AgentKind.Claude => "Claude",
        AgentKind.OpenCode => "OpenCode",
        _ => kind.ToString(),
    };

    private static string DescribeSession(AgentSession session)
    {
        var line = $"{KindLabel(session.Kind)} · {StatusLabel(session)}";
        if (!string.IsNullOrWhiteSpace(session.Name))
            line += $" — {session.Name}";
        if (!string.IsNullOrWhiteSpace(session.WorkingDirectory))
            line += $" ({session.WorkingDirectory})";
        return line;
    }
}
