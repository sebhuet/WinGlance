namespace WinGlance.Models;

/// <summary>
/// Change of a window's agent state that deserves telling the user about.
/// </summary>
public enum AgentTransition
{
    /// <summary>Nothing new for the user.</summary>
    None,

    /// <summary>The agent finished its turn (busy → idle).</summary>
    Finished,

    /// <summary>The agent started waiting on the user (permission prompt, question, dialog).</summary>
    NeedsInput,
}

/// <summary>
/// Wording of the notification shown for an agent transition.
/// </summary>
internal static class AgentNotification
{
    /// <summary>
    /// Title and message, e.g. "Claude finished · BookAmPlus" / the window title.
    /// </summary>
    public static (string Title, string Message) Describe(TrackedWindow window, AgentTransition transition)
    {
        var badge = window.Agent;
        var agent = badge?.AgentName ?? "Agent";
        var what = transition == AgentTransition.NeedsInput
            ? $"{agent} needs you"
            : $"{agent} finished";
        var title = badge?.Project is { } project ? $"{what} · {project}" : what;

        // For a waiting agent the status says why ("permission prompt"); the window title says where
        var reason = transition == AgentTransition.NeedsInput && badge?.StatusText is { } status
            ? status + Environment.NewLine
            : string.Empty;
        return (title, reason + window.Title);
    }
}
