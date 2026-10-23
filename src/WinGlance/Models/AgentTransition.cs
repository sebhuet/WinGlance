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

    /// <summary>The agent has been waiting on the user for longer than the reminder delay.</summary>
    Reminder,
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
        var what = transition switch
        {
            AgentTransition.NeedsInput => $"{agent} needs you",
            AgentTransition.Reminder => $"{agent} still needs you",
            _ => $"{agent} finished",
        };
        var title = badge?.Project is { } project ? $"{what} · {project}" : what;

        // For a waiting agent the status says why ("permission prompt"); the window title says where
        var waiting = transition is AgentTransition.NeedsInput or AgentTransition.Reminder;
        var reason = waiting && badge?.StatusText is { } status
            ? status + Environment.NewLine
            : string.Empty;
        // A reminder also says for how long: the reason to look now
        if (transition == AgentTransition.Reminder)
            reason = $"Waiting for {Services.WindowUrgency.FormatAge(DateTime.UtcNow - window.StatusSince)}" + Environment.NewLine + reason;
        return (title, reason + window.Title);
    }
}
