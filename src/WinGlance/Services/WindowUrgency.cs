using System.Collections;
using WinGlance.Models;

namespace WinGlance.Services;

/// <summary>
/// How much a window needs the user, for the "sort by urgency" order, the "only windows that
/// need me" filter and the time shown since the last status change.
/// </summary>
internal static class WindowUrgency
{
    /// <summary>An agent is blocked on a permission prompt or a question.</summary>
    public const int AgentWaiting = 0;

    /// <summary>The window is not responding, blocked by a dialog, flashing, or found awaiting action by the LLM.</summary>
    public const int Attention = 1;

    /// <summary>An agent finished its turn while the user was elsewhere.</summary>
    public const int AgentFinished = 2;

    /// <summary>An agent waits for the next prompt, and the user has seen it.</summary>
    public const int AgentIdle = 3;

    /// <summary>An agent is working (or runs in an unknown state, OpenCode).</summary>
    public const int AgentWorking = 4;

    /// <summary>No agent and no attention state.</summary>
    public const int Quiet = 5;

    /// <summary>Urgency rank of a window, from <see cref="AgentWaiting"/> (most urgent) to <see cref="Quiet"/>.</summary>
    public static int Rank(TrackedWindow window) => window switch
    {
        { Agent.Status: AgentStatus.Waiting } => AgentWaiting,
        { IsHung: true } or { IsModalBlocked: true } or { IsFlashing: true } or { LlmVerdict: "awaiting_action" } => Attention,
        { Agent.Status: AgentStatus.Idle, NeedsAgentAttention: true } => AgentFinished,
        { Agent.Status: AgentStatus.Idle } => AgentIdle,
        { Agent: not null } => AgentWorking,
        _ => Quiet,
    };

    /// <summary>
    /// True for the windows the "only windows that need me" filter keeps: everything but
    /// working agents and quiet windows.
    /// </summary>
    public static bool NeedsMe(TrackedWindow window) => Rank(window) <= AgentIdle;

    /// <summary>
    /// The status whose changes reset <see cref="TrackedWindow.StatusSince"/>. For an agent, its
    /// status alone: seeing a finished turn does not reset the time since the agent stopped.
    /// </summary>
    public static string StatusKey(TrackedWindow window) => window switch
    {
        { Agent: { } agent } => "agent:" + agent.Status,
        { IsHung: true } => "hung",
        { IsModalBlocked: true } => "modal",
        { IsFlashing: true } => "flashing",
        { LlmVerdict: "awaiting_action" } => "awaiting_action",
        _ => "quiet",
    };

    /// <summary>Compact duration shown under a thumbnail: "&lt; 1 min", "12 min", "1 h 05", "2 d".</summary>
    public static string FormatAge(TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1))
            return "< 1 min";
        if (age < TimeSpan.FromHours(1))
            return $"{(int)age.TotalMinutes} min";
        if (age < TimeSpan.FromDays(1))
            return $"{(int)age.TotalHours} h {age.Minutes:00}";
        return $"{(int)age.TotalDays} d";
    }

    /// <summary>
    /// Most urgent first; within a rank, the window in that status for the longest time first.
    /// Non-generic too, for <see cref="System.Windows.Data.ListCollectionView.CustomSort"/>.
    /// </summary>
    public sealed class Comparer : IComparer<TrackedWindow>, IComparer
    {
        /// <summary>The shared instance.</summary>
        public static readonly Comparer Instance = new();

        /// <inheritdoc />
        public int Compare(TrackedWindow? x, TrackedWindow? y)
        {
            if (ReferenceEquals(x, y))
                return 0;
            if (x is null)
                return 1;
            if (y is null)
                return -1;

            var byRank = Rank(x).CompareTo(Rank(y));
            if (byRank != 0)
                return byRank;
            var bySince = x.StatusSince.CompareTo(y.StatusSince);
            // The handle only makes the order total, so that equal windows never swap places
            return bySince != 0 ? bySince : x.Hwnd.ToInt64().CompareTo(y.Hwnd.ToInt64());
        }

        int IComparer.Compare(object? x, object? y) => Compare(x as TrackedWindow, y as TrackedWindow);
    }
}
