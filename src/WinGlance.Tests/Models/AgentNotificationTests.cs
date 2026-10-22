using WinGlance.Models;

namespace WinGlance.Tests.Models;

public class AgentNotificationTests
{
    private static TrackedWindow Window(AgentStatus status, string? waitingFor = null)
    {
        var session = new AgentSession(AgentKind.Claude, 1, @"w:\00_DEV\BookAmPlus", status, waitingFor);
        return new TrackedWindow((IntPtr)1, "Code", "VS Code")
        {
            Title = "Contenus A+ Amazon - BookAmPlus - Visual Studio Code",
            Agent = AgentBadge.FromSessions([session]),
        };
    }

    [Fact]
    public void Describe_Finished_NamesTheAgentAndTheProject()
    {
        var (title, message) = AgentNotification.Describe(Window(AgentStatus.Idle), AgentTransition.Finished);

        Assert.Equal("Claude finished · BookAmPlus", title);
        Assert.Equal("Contenus A+ Amazon - BookAmPlus - Visual Studio Code", message);
    }

    [Fact]
    public void Describe_NeedsInput_SaysWhatTheAgentWaitsFor()
    {
        var (title, message) = AgentNotification.Describe(
            Window(AgentStatus.Waiting, "permission prompt"), AgentTransition.NeedsInput);

        Assert.Equal("Claude needs you · BookAmPlus", title);
        Assert.StartsWith("permission prompt" + Environment.NewLine, message);
        Assert.EndsWith("Visual Studio Code", message);
    }

    [Fact]
    public void Describe_WithoutBadge_StillReadable()
    {
        var window = new TrackedWindow((IntPtr)1, "Code", "VS Code") { Title = "Some window" };

        var (title, message) = AgentNotification.Describe(window, AgentTransition.Finished);

        Assert.Equal("Agent finished", title);
        Assert.Equal("Some window", message);
    }
}
