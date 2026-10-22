using WinGlance.Models;
using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class OpenCodeAgentSourceTests
{
    // High PID unlikely to exist, so that reading its working directory fails cleanly
    private const int FakePid = 0x3FFFFFF0;

    private static ProcessInfo P(int id, int parent, string exe) => new(id, parent, exe);

    [Fact]
    public void FindRootProcesses_NoOpenCode_ReturnsEmpty()
    {
        var tree = new ProcessTree([P(1, 0, "explorer.exe"), P(2, 1, "pwsh.exe")]);

        Assert.Empty(OpenCodeAgentSource.FindRootProcesses(tree));
    }

    [Fact]
    public void FindRootProcesses_SingleInstance_ReturnsIt()
    {
        var tree = new ProcessTree([P(1, 0, "pwsh.exe"), P(2, 1, "opencode.exe")]);

        Assert.Equal(2, Assert.Single(OpenCodeAgentSource.FindRootProcesses(tree)).ProcessId);
    }

    [Fact]
    public void FindRootProcesses_HelperSpawnedByAnInstance_IsNotCounted()
    {
        var tree = new ProcessTree([
            P(1, 0, "pwsh.exe"),
            P(2, 1, "opencode.exe"),
            P(3, 2, "node.exe"),
            P(4, 3, "opencode.exe"),
        ]);

        Assert.Equal(2, Assert.Single(OpenCodeAgentSource.FindRootProcesses(tree)).ProcessId);
    }

    [Fact]
    public void FindRootProcesses_IndependentInstances_AreAllReturned()
    {
        var tree = new ProcessTree([
            P(1, 0, "pwsh.exe"),
            P(2, 1, "opencode.exe"),
            P(3, 0, "pwsh.exe"),
            P(4, 3, "opencode.exe"),
        ]);

        Assert.Equal([2, 4], OpenCodeAgentSource.FindRootProcesses(tree).Select(p => p.ProcessId).Order());
    }

    [Theory]
    [InlineData("OpenCode.EXE", 1)]
    [InlineData("opencode-helper.exe", 0)]
    [InlineData("opencode", 0)]
    public void FindRootProcesses_MatchesOnlyTheOpenCodeExecutable(string exeName, int expectedCount)
    {
        var tree = new ProcessTree([P(1, 0, "pwsh.exe"), P(2, 1, exeName)]);

        Assert.Equal(expectedCount, OpenCodeAgentSource.FindRootProcesses(tree).Count);
    }

    [Fact]
    public void GetSessions_ReportsRunningStatus_WithoutBusyIdleDetail()
    {
        var tree = new ProcessTree([P(FakePid, 0, "opencode.exe")]);

        var session = Assert.Single(new OpenCodeAgentSource().GetSessions(tree));

        Assert.Equal(AgentKind.OpenCode, session.Kind);
        Assert.Equal(FakePid, session.ProcessId);
        Assert.Equal(AgentStatus.Running, session.Status);
        Assert.Null(session.WorkingDirectory); // process does not exist
    }
}
