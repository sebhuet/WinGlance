namespace WinGlance.Models;

/// <summary>
/// Coding agent tool that runs a session.
/// </summary>
internal enum AgentKind
{
    Claude,
    OpenCode,
}

/// <summary>
/// Live state of a coding agent session. Values are ordered by urgency: when several
/// sessions share a window, the highest value is the one shown.
/// </summary>
public enum AgentStatus
{
    /// <summary>The turn is finished; the agent waits for the next prompt.</summary>
    Idle,

    /// <summary>The process is alive but reports no state (OpenCode).</summary>
    Running,

    /// <summary>A turn is in progress.</summary>
    Busy,

    /// <summary>Blocked on the user: permission prompt, question, open dialog.</summary>
    Waiting,
}

/// <summary>
/// A coding agent session discovered on the machine.
/// </summary>
/// <param name="Kind">Tool that runs the session.</param>
/// <param name="ProcessId">PID of the agent process, used to find the window hosting it.</param>
/// <param name="WorkingDirectory">Directory the agent works in; tells apart windows that share one process.</param>
/// <param name="Status">Live state of the session.</param>
/// <param name="WaitingFor">What a waiting session needs, e.g. "permission prompt" (Claude only).</param>
/// <param name="Name">Session display name (Claude only).</param>
internal sealed record AgentSession(
    AgentKind Kind,
    int ProcessId,
    string? WorkingDirectory,
    AgentStatus Status,
    string? WaitingFor = null,
    string? Name = null);
