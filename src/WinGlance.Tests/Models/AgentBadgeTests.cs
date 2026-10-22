using WinGlance.Models;

namespace WinGlance.Tests.Models;

public class AgentBadgeTests
{
    private static AgentSession Claude(AgentStatus status, string? waitingFor = null, string? name = null, string? cwd = null) =>
        new(AgentKind.Claude, 1, cwd, status, waitingFor, name);

    [Fact]
    public void FromSessions_NoSession_ReturnsNull()
    {
        Assert.Null(AgentBadge.FromSessions([]));
    }

    [Theory]
    [InlineData(AgentStatus.Busy, "Claude · working")]
    [InlineData(AgentStatus.Idle, "Claude · idle")]
    [InlineData(AgentStatus.Waiting, "Claude · needs input")]
    [InlineData(AgentStatus.Running, "Claude · running")]
    public void FromSessions_SingleSession_DescribesItsStatus(AgentStatus status, string expectedText)
    {
        var badge = AgentBadge.FromSessions([Claude(status)])!;

        Assert.Equal(status, badge.Status);
        Assert.Equal(expectedText, badge.Text);
    }

    [Fact]
    public void FromSessions_WaitingSession_ShowsWhatItWaitsFor()
    {
        var badge = AgentBadge.FromSessions([Claude(AgentStatus.Waiting, waitingFor: "permission prompt")])!;

        Assert.Equal("Claude · permission prompt", badge.Text);
    }

    [Fact]
    public void FromSessions_OpenCodeSession_IsLabelledOpenCode()
    {
        var badge = AgentBadge.FromSessions([new AgentSession(AgentKind.OpenCode, 1, null, AgentStatus.Running)])!;

        Assert.Equal("OpenCode · running", badge.Text);
    }

    [Fact]
    public void FromSessions_SeveralSessions_ShowsTheMostUrgentAndCountsTheOthers()
    {
        var badge = AgentBadge.FromSessions([
            Claude(AgentStatus.Idle),
            Claude(AgentStatus.Waiting, waitingFor: "input needed"),
            Claude(AgentStatus.Busy),
        ])!;

        Assert.Equal(AgentStatus.Waiting, badge.Status);
        Assert.Equal("Claude · input needed +2", badge.Text);
    }

    [Fact]
    public void FromSessions_RunningOutranksIdle()
    {
        // An OpenCode instance may be working; an idle Claude session is certainly not
        var badge = AgentBadge.FromSessions([
            Claude(AgentStatus.Idle),
            new AgentSession(AgentKind.OpenCode, 2, null, AgentStatus.Running),
        ])!;

        Assert.Equal(AgentStatus.Running, badge.Status);
    }

    [Fact]
    public void FromSessions_Details_ListsEachSessionWithNameAndFolder_MostUrgentFirst()
    {
        var badge = AgentBadge.FromSessions([
            Claude(AgentStatus.Idle, name: "winglance-bb", cwd: @"z:\My Drive\00_DEV\WinGlance"),
            Claude(AgentStatus.Busy),
        ])!;

        var lines = badge.Details.Split(Environment.NewLine);
        Assert.Equal(["Claude · working", @"Claude · idle — winglance-bb (z:\My Drive\00_DEV\WinGlance)"], lines);
    }

    [Fact]
    public void FromSessions_SameInput_ProducesEqualBadges()
    {
        // Value equality keeps TrackedWindow from raising PropertyChanged on every poll
        var first = AgentBadge.FromSessions([Claude(AgentStatus.Busy, name: "a")]);
        var second = AgentBadge.FromSessions([Claude(AgentStatus.Busy, name: "a")]);

        Assert.Equal(first, second);
    }

    // ── Background commands of an idle session ───────────────────────────

    [Theory]
    [InlineData(1, "Claude · 1 background task")]
    [InlineData(3, "Claude · 3 background tasks")]
    public void FromSessions_IdleWithBackgroundTasks_ShowsWorkInProgress(int tasks, string expectedText)
    {
        // Claude says "idle" once its turn ends, even while its background commands run
        var badge = AgentBadge.FromSessions([Claude(AgentStatus.Idle) with { BackgroundTasks = tasks }])!;

        Assert.Equal(AgentStatus.Busy, badge.Status);
        Assert.Equal(expectedText, badge.Text);
    }

    [Fact]
    public void FromSessions_BackgroundTasks_OutrankAPlainIdleSession()
    {
        var badge = AgentBadge.FromSessions([
            Claude(AgentStatus.Idle),
            Claude(AgentStatus.Idle) with { BackgroundTasks = 2 },
        ])!;

        Assert.Equal("Claude · 2 background tasks +1", badge.Text);
    }

    [Fact]
    public void FromSessions_WaitingStillOutranksBackgroundTasks()
    {
        var badge = AgentBadge.FromSessions([
            Claude(AgentStatus.Idle) with { BackgroundTasks = 2 },
            Claude(AgentStatus.Waiting, waitingFor: "permission prompt"),
        ])!;

        Assert.Equal(AgentStatus.Waiting, badge.Status);
    }

    [Theory]
    [InlineData(AgentStatus.Idle, 0, AgentStatus.Idle)]
    [InlineData(AgentStatus.Idle, 2, AgentStatus.Busy)]
    [InlineData(AgentStatus.Waiting, 2, AgentStatus.Waiting)]
    [InlineData(AgentStatus.Busy, 0, AgentStatus.Busy)]
    public void EffectiveStatus_TurnsIdleIntoBusyOnlyWithBackgroundTasks(AgentStatus status, int tasks, AgentStatus expected)
    {
        Assert.Equal(expected, (Claude(status) with { BackgroundTasks = tasks }).EffectiveStatus);
    }

    [Fact]
    public void AgentStatus_IsOrderedByUrgency()
    {
        Assert.True(AgentStatus.Idle < AgentStatus.Running);
        Assert.True(AgentStatus.Running < AgentStatus.Busy);
        Assert.True(AgentStatus.Busy < AgentStatus.Waiting);
    }
}
