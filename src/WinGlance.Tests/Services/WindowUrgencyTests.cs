using WinGlance.Models;
using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class WindowUrgencyTests
{
    private static readonly DateTime Noon = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

    private static TrackedWindow Window(int hwnd = 1, AgentStatus? agent = null, bool unseen = false) =>
        new((IntPtr)hwnd, "Code", "VS Code")
        {
            Agent = agent is null ? null : new AgentBadge(agent.Value, agent.Value.ToString(), ""),
            NeedsAgentAttention = unseen,
        };

    // ── Rank ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(AgentStatus.Waiting, false, WindowUrgency.AgentWaiting)]
    [InlineData(AgentStatus.Idle, true, WindowUrgency.AgentFinished)]
    [InlineData(AgentStatus.Idle, false, WindowUrgency.AgentIdle)]
    [InlineData(AgentStatus.Busy, false, WindowUrgency.AgentWorking)]
    [InlineData(AgentStatus.Running, false, WindowUrgency.AgentWorking)] // OpenCode: state unknown
    public void Rank_Agent_FollowsItsStatus(AgentStatus status, bool unseen, int expected)
    {
        Assert.Equal(expected, WindowUrgency.Rank(Window(agent: status, unseen: unseen)));
    }

    [Fact]
    public void Rank_NoAgentNoState_IsQuiet()
    {
        Assert.Equal(WindowUrgency.Quiet, WindowUrgency.Rank(Window()));
    }

    [Theory]
    [InlineData(true, false, false, null)]
    [InlineData(false, true, false, null)]
    [InlineData(false, false, true, null)]
    [InlineData(false, false, false, "awaiting_action")]
    public void Rank_AttentionState_ComesRightAfterAWaitingAgent(bool hung, bool modal, bool flashing, string? verdict)
    {
        var window = Window(agent: AgentStatus.Idle, unseen: true);
        window.IsHung = hung;
        window.IsModalBlocked = modal;
        window.IsFlashing = flashing;
        window.LlmVerdict = verdict;

        Assert.Equal(WindowUrgency.Attention, WindowUrgency.Rank(window));
    }

    [Fact]
    public void Rank_WaitingAgent_OutranksAnAttentionState()
    {
        var window = Window(agent: AgentStatus.Waiting);
        window.IsModalBlocked = true;

        Assert.Equal(WindowUrgency.AgentWaiting, WindowUrgency.Rank(window));
    }

    // ── NeedsMe ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(AgentStatus.Waiting, true)]
    [InlineData(AgentStatus.Idle, true)] // waits for the next prompt
    [InlineData(AgentStatus.Busy, false)]
    [InlineData(null, false)]
    public void NeedsMe_KeepsAllButWorkingAgentsAndQuietWindows(AgentStatus? status, bool expected)
    {
        Assert.Equal(expected, WindowUrgency.NeedsMe(Window(agent: status)));
    }

    // ── StatusKey ────────────────────────────────────────────────────────

    [Fact]
    public void StatusKey_SeeingAFinishedTurn_DoesNotChangeIt()
    {
        // The time shown is the time since the agent stopped, not since the user looked
        var unseen = Window(agent: AgentStatus.Idle, unseen: true);
        var seen = Window(agent: AgentStatus.Idle, unseen: false);

        Assert.Equal(WindowUrgency.StatusKey(unseen), WindowUrgency.StatusKey(seen));
    }

    [Fact]
    public void StatusKey_DiffersBetweenStatuses()
    {
        var keys = new[]
        {
            WindowUrgency.StatusKey(Window(agent: AgentStatus.Busy)),
            WindowUrgency.StatusKey(Window(agent: AgentStatus.Waiting)),
            WindowUrgency.StatusKey(Window(agent: AgentStatus.Idle)),
            WindowUrgency.StatusKey(Window()),
            WindowUrgency.StatusKey(new TrackedWindow((IntPtr)1, "a", "a") { IsModalBlocked = true }),
        };

        Assert.Equal(keys.Length, keys.Distinct().Count());
    }

    // ── FormatAge ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(30, "< 1 min")]
    [InlineData(60, "1 min")]
    [InlineData(59 * 60 + 59, "59 min")]
    [InlineData(3600 + 5 * 60, "1 h 05")]
    [InlineData(23 * 3600 + 59 * 60, "23 h 59")]
    [InlineData(2 * 86400 + 3600, "2 d")]
    public void FormatAge_IsCompact(int seconds, string expected)
    {
        Assert.Equal(expected, WindowUrgency.FormatAge(TimeSpan.FromSeconds(seconds)));
    }

    // ── Comparer ─────────────────────────────────────────────────────────

    [Fact]
    public void Comparer_MostUrgentFirst_ThenLongestInThatStatus()
    {
        var working = Window(1, AgentStatus.Busy);
        working.StatusSince = Noon.AddHours(-5);
        var waitingRecently = Window(2, AgentStatus.Waiting);
        waitingRecently.StatusSince = Noon.AddMinutes(-1);
        var waitingLong = Window(3, AgentStatus.Waiting);
        waitingLong.StatusSince = Noon.AddMinutes(-20);
        var quiet = Window(4);
        var finished = Window(5, AgentStatus.Idle, unseen: true);

        var sorted = new[] { working, waitingRecently, quiet, finished, waitingLong }
            .OrderBy(w => w, WindowUrgency.Comparer.Instance)
            .Select(w => (int)w.Hwnd);

        Assert.Equal(new[] { 3, 2, 5, 1, 4 }, sorted);
    }

    [Fact]
    public void Comparer_SameRankAndTime_OrdersByHandle()
    {
        // A total order: equal windows never swap places between two sorts
        var a = Window(7);
        var b = Window(8);

        Assert.True(WindowUrgency.Comparer.Instance.Compare(a, b) < 0);
        Assert.True(WindowUrgency.Comparer.Instance.Compare(b, a) > 0);
        Assert.Equal(0, WindowUrgency.Comparer.Instance.Compare(a, a));
    }
}
