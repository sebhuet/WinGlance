namespace WinGlance.Models;

/// <summary>
/// Aggregated coding agent state of one window, shown as a badge under its thumbnail.
/// A record, so that an unchanged badge compares equal and raises no PropertyChanged.
/// </summary>
/// <param name="Status">Most urgent status among the window's sessions.</param>
/// <param name="Text">Short label, e.g. "Claude · working" or "Claude · permission prompt +1".</param>
/// <param name="Details">Tooltip text with one line per session.</param>
public sealed record AgentBadge(AgentStatus Status, string Text, string Details)
{
    /// <summary>
    /// Builds the badge of a window from its sessions, or returns null when there are none.
    /// </summary>
    internal static AgentBadge? FromSessions(IReadOnlyCollection<AgentSession> sessions)
    {
        if (sessions.Count == 0)
            return null;

        // Most urgent first; OrderByDescending is stable, so ties keep discovery order
        var ordered = sessions.OrderByDescending(s => s.Status).ToList();
        var top = ordered[0];

        var text = $"{KindLabel(top.Kind)} · {StatusLabel(top)}";
        if (ordered.Count > 1)
            text += $" +{ordered.Count - 1}";

        var details = string.Join(Environment.NewLine, ordered.Select(DescribeSession));
        return new AgentBadge(top.Status, text, details);
    }

    /// <summary>Human-readable status, using Claude's own reason for a waiting session.</summary>
    internal static string StatusLabel(AgentSession session) => session.Status switch
    {
        AgentStatus.Waiting => string.IsNullOrWhiteSpace(session.WaitingFor) ? "needs input" : session.WaitingFor,
        AgentStatus.Busy => "working",
        AgentStatus.Idle => "idle",
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
