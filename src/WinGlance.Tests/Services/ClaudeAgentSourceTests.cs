using WinGlance.Models;
using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class ClaudeAgentSourceTests
{
    // Shape of a real `claude agents --json` output (v2.1.296)
    private const string CliOutput = """
        [
          {
            "pid": 4992,
            "cwd": "d:\\00_VOXDEV\\CoDirFollow",
            "kind": "interactive",
            "startedAt": 1792564714092,
            "sessionId": "bdd01031-10c7-470c-94bf-03aa089a3418",
            "name": "codirfollow-38",
            "status": "idle"
          },
          {
            "pid": 73696,
            "cwd": "d:\\00_VOXDEV\\AGuide_Smart_CMS",
            "kind": "interactive",
            "startedAt": 1792575901142,
            "name": "aguide-smart-cms-7f",
            "status": "busy"
          }
        ]
        """;

    // ── Parse ────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_CliOutput_ReturnsOneSessionPerEntry()
    {
        var sessions = ClaudeAgentSource.Parse(CliOutput);

        Assert.Equal(2, sessions.Count);
        Assert.Equal(
            new AgentSession(AgentKind.Claude, 4992, @"d:\00_VOXDEV\CoDirFollow", AgentStatus.Idle, null, "codirfollow-38"),
            sessions[0]);
        Assert.Equal(73696, sessions[1].ProcessId);
        Assert.Equal(AgentStatus.Busy, sessions[1].Status);
    }

    [Fact]
    public void Parse_WaitingSession_KeepsWaitingForReason()
    {
        var sessions = ClaudeAgentSource.Parse(
            """[{ "pid": 12, "status": "waiting", "waitingFor": "permission prompt" }]""");

        var session = Assert.Single(sessions);
        Assert.Equal(AgentStatus.Waiting, session.Status);
        Assert.Equal("permission prompt", session.WaitingFor);
    }

    [Fact]
    public void Parse_EntryWithoutPid_IsSkipped()
    {
        // Background sessions whose process has exited have no pid: no window can host them
        var sessions = ClaudeAgentSource.Parse(
            """[{ "id": "abc123", "kind": "background", "state": "done", "cwd": "C:\\work" }]""");

        Assert.Empty(sessions);
    }

    [Fact]
    public void Parse_NonNumericPid_IsSkipped()
    {
        var sessions = ClaudeAgentSource.Parse("""[{ "pid": "4992", "status": "busy" }]""");

        Assert.Empty(sessions);
    }

    [Fact]
    public void Parse_MissingOptionalFields_AreNull()
    {
        var session = Assert.Single(ClaudeAgentSource.Parse("""[{ "pid": 7 }]"""));

        Assert.Null(session.WorkingDirectory);
        Assert.Null(session.Name);
        Assert.Null(session.WaitingFor);
        Assert.Equal(AgentStatus.Running, session.Status);
    }

    [Fact]
    public void Parse_NonObjectItems_AreSkipped()
    {
        var sessions = ClaudeAgentSource.Parse("""[42, "text", null, { "pid": 7, "status": "idle" }]""");

        Assert.Equal(7, Assert.Single(sessions).ProcessId);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("error: unknown option '--json'")]
    public void Parse_EmptyOrUnexpectedOutput_ReturnsEmpty(string output)
    {
        Assert.Empty(ClaudeAgentSource.Parse(output));
    }

    // ── ParseStatus ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("busy", AgentStatus.Busy)]
    [InlineData("BUSY", AgentStatus.Busy)]
    [InlineData("waiting", AgentStatus.Waiting)]
    [InlineData("idle", AgentStatus.Idle)]
    [InlineData("paused", AgentStatus.Running)]
    [InlineData(null, AgentStatus.Running)]
    public void ParseStatus_MapsDocumentedValues_AndFallsBackToRunning(string? status, AgentStatus expected)
    {
        Assert.Equal(expected, ClaudeAgentSource.ParseStatus(status));
    }

    // ── Executable handling ──────────────────────────────────────────────

    [Fact]
    public void GetSessions_NoExecutable_ReturnsEmpty()
    {
        var source = new ClaudeAgentSource(null);

        Assert.Empty(source.GetSessions());
    }

    [Fact]
    public void GetSessions_MissingExecutable_ReturnsEmpty()
    {
        var source = new ClaudeAgentSource(@"C:\does-not-exist\claude.exe");

        Assert.Empty(source.GetSessions());
    }

    [Theory]
    [InlineData(@"C:\Users\me\AppData\Roaming\npm\claude.cmd", true)]
    [InlineData(@"C:\Users\me\.local\bin\claude.exe", false)]
    [InlineData(null, false)]
    public void IsScriptShim_IsTrueOnlyForCmdShims(string? path, bool expected)
    {
        Assert.Equal(expected, new ClaudeAgentSource(path).IsScriptShim);
    }

    [Fact]
    public void FindExecutable_ReturnsNullOrAnExistingClaudeFile()
    {
        var path = ClaudeAgentSource.FindExecutable();

        if (path is not null)
        {
            Assert.True(File.Exists(path));
            Assert.StartsWith("claude.", Path.GetFileName(path), StringComparison.OrdinalIgnoreCase);
        }
    }
}
