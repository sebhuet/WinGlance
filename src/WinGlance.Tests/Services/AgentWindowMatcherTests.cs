using WinGlance.Models;
using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class AgentWindowMatcherTests
{
    // Process layout observed with VS Code: one main process owns every window; each window has
    // its own extension host (where the Claude extension runs claude.exe), and one ptyHost serves
    // the integrated terminals of all windows.
    private const int VsCodeMain = 100;
    private const int ExtensionHostA = 201;
    private const int ExtensionHostB = 202;
    private const int ClaudeInA = 301;
    private const int ClaudeInB = 302;
    private const int PtyHost = 400;
    private const int TerminalShell = 401;
    private const int OpenCodeInTerminal = 402;

    private static readonly ProcessTree VsCodeTree = new([
        new ProcessInfo(1, 0, "explorer.exe"),
        new ProcessInfo(VsCodeMain, 1, "Code.exe"),
        new ProcessInfo(ExtensionHostA, VsCodeMain, "Code.exe"),
        new ProcessInfo(ExtensionHostB, VsCodeMain, "Code.exe"),
        new ProcessInfo(ClaudeInA, ExtensionHostA, "claude.exe"),
        new ProcessInfo(ClaudeInB, ExtensionHostB, "claude.exe"),
        new ProcessInfo(PtyHost, VsCodeMain, "Code.exe"),
        new ProcessInfo(TerminalShell, PtyHost, "powershell.exe"),
        new ProcessInfo(OpenCodeInTerminal, TerminalShell, "opencode.exe"),
    ]);

    private static TrackedWindow Window(int hwnd, int processId, string title) =>
        new((IntPtr)hwnd, "Code", "VS Code") { ProcessId = (uint)processId, Title = title };

    private static AgentSession Claude(int pid, string cwd, AgentStatus status = AgentStatus.Busy) =>
        new(AgentKind.Claude, pid, cwd, status);

    private static readonly TrackedWindow BookWindow =
        Window(1, VsCodeMain, "README.md - BookAmPlus - Visual Studio Code");

    private static readonly TrackedWindow GlanceWindow =
        Window(2, VsCodeMain, "● PreviewTab.xaml - WinGlance - Visual Studio Code");

    // ── Match ────────────────────────────────────────────────────────────

    [Fact]
    public void Match_WindowsSharingOneProcess_AreToldApartByTheFolderInTheirTitle()
    {
        var badges = AgentWindowMatcher.Match(
            [BookWindow, GlanceWindow],
            [
                Claude(ClaudeInA, @"w:\00_DEV\BookAmPlus", AgentStatus.Busy),
                Claude(ClaudeInB, @"z:\My Drive\00_DEV\WinGlance", AgentStatus.Idle),
            ],
            VsCodeTree);

        Assert.Equal(AgentStatus.Busy, badges[BookWindow.Hwnd].Status);
        Assert.Equal(AgentStatus.Idle, badges[GlanceWindow.Hwnd].Status);
    }

    [Fact]
    public void Match_AgentInIntegratedTerminalSubfolder_FindsItsWorkspaceWindow()
    {
        var openCode = new AgentSession(
            AgentKind.OpenCode, OpenCodeInTerminal, @"z:\My Drive\00_DEV\WinGlance\src", AgentStatus.Running);

        var badges = AgentWindowMatcher.Match([BookWindow, GlanceWindow], [openCode], VsCodeTree);

        Assert.Equal("OpenCode · running", Assert.Single(badges, b => b.Key == GlanceWindow.Hwnd).Value.Text);
        Assert.Single(badges);
    }

    [Fact]
    public void Match_SingleWindowForTheHostProcess_IsUsedWithoutTitleMatch()
    {
        var tree = new ProcessTree([
            new ProcessInfo(500, 1, "WindowsTerminal.exe"),
            new ProcessInfo(501, 500, "pwsh.exe"),
            new ProcessInfo(502, 501, "claude.exe"),
        ]);
        var terminal = Window(9, 500, "Windows PowerShell");

        var badges = AgentWindowMatcher.Match([terminal], [Claude(502, @"C:\unrelated")], tree);

        Assert.True(badges.ContainsKey(terminal.Hwnd));
    }

    [Fact]
    public void Match_NearestWindowOwningAncestor_Wins()
    {
        // A standalone terminal started from VS Code: the terminal window is the nearer host
        var tree = new ProcessTree([
            new ProcessInfo(VsCodeMain, 1, "Code.exe"),
            new ProcessInfo(600, VsCodeMain, "WindowsTerminal.exe"),
            new ProcessInfo(601, 600, "claude.exe"),
        ]);
        var terminal = Window(9, 600, "claude");

        var badges = AgentWindowMatcher.Match(
            [BookWindow, GlanceWindow, terminal], [Claude(601, @"z:\My Drive\00_DEV\WinGlance")], tree);

        Assert.Equal([terminal.Hwnd], badges.Keys);
    }

    [Fact]
    public void Match_SameFolderNameInTwoWindows_LeavesTheSessionUnassigned()
    {
        var first = Window(1, VsCodeMain, "a.cs - WinGlance - Visual Studio Code");
        var second = Window(2, VsCodeMain, "b.cs - WinGlance - Visual Studio Code");

        var badges = AgentWindowMatcher.Match(
            [first, second], [Claude(ClaudeInA, @"z:\My Drive\00_DEV\WinGlance")], VsCodeTree);

        Assert.Empty(badges);
    }

    [Fact]
    public void Match_NoTitleNamesTheFolder_LeavesTheSessionUnassigned()
    {
        var badges = AgentWindowMatcher.Match(
            [BookWindow, GlanceWindow], [Claude(ClaudeInA, @"C:\elsewhere\Other")], VsCodeTree);

        Assert.Empty(badges);
    }

    [Fact]
    public void Match_SessionOutsideAnyTrackedWindow_IsIgnored()
    {
        // e.g. a background session run by the Claude supervisor, which has no window
        var tree = new ProcessTree([new ProcessInfo(700, 1, "claude.exe"), new ProcessInfo(701, 700, "claude.exe")]);

        var badges = AgentWindowMatcher.Match([BookWindow], [Claude(701, @"w:\00_DEV\BookAmPlus")], tree);

        Assert.Empty(badges);
    }

    [Fact]
    public void Match_WindowWithoutProcessId_IsNeverAHost()
    {
        var window = Window(5, 0, "BookAmPlus");
        var tree = new ProcessTree([new ProcessInfo(800, 0, "claude.exe")]);

        Assert.Empty(AgentWindowMatcher.Match([window], [Claude(800, @"w:\00_DEV\BookAmPlus")], tree));
    }

    [Fact]
    public void Match_SessionsSharingAWindow_AreAggregatedIntoOneBadge()
    {
        var tree = new ProcessTree([
            new ProcessInfo(500, 1, "WindowsTerminal.exe"),
            new ProcessInfo(501, 500, "claude.exe"),
            new ProcessInfo(502, 500, "claude.exe"),
        ]);
        var terminal = Window(9, 500, "Terminal");

        var badges = AgentWindowMatcher.Match(
            [terminal],
            [Claude(501, @"C:\a", AgentStatus.Busy), new AgentSession(AgentKind.Claude, 502, @"C:\b", AgentStatus.Waiting, "permission prompt")],
            tree);

        var badge = badges[terminal.Hwnd];
        Assert.Equal(AgentStatus.Waiting, badge.Status);
        Assert.Equal("Claude · permission prompt +1", badge.Text);
    }

    [Fact]
    public void Match_NoSessions_ReturnsEmpty()
    {
        Assert.Empty(AgentWindowMatcher.Match([BookWindow], [], VsCodeTree));
    }

    // ── PickByWorkingDirectory ───────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PickByWorkingDirectory_NoDirectory_ReturnsNull(string? cwd)
    {
        Assert.Null(AgentWindowMatcher.PickByWorkingDirectory([BookWindow, GlanceWindow], cwd));
    }

    [Fact]
    public void PickByWorkingDirectory_HigherScoreWins()
    {
        // Leaf folder (WinGlance) beats parent folder (00_DEV)
        var parentMatch = Window(1, VsCodeMain, "x - 00_DEV - Visual Studio Code");

        var picked = AgentWindowMatcher.PickByWorkingDirectory(
            [parentMatch, GlanceWindow], @"z:\My Drive\00_DEV\WinGlance");

        Assert.Same(GlanceWindow, picked);
    }

    // ── ScoreTitle ───────────────────────────────────────────────────────

    [Theory]
    // Full path shown by a terminal
    [InlineData(@"pwsh - C:\Users\me\proj", @"C:\Users\me\proj", 1000)]
    [InlineData(@"C:\Users\me\proj\src> claude", @"C:\Users\me\proj", 1000)]
    // A longer folder name is not the same folder
    [InlineData(@"C:\Users\me\proj-old", @"C:\Users\me\proj", 0)]
    // VS Code "file - Folder - app" titles
    [InlineData("README.md - WinGlance - Visual Studio Code", @"z:\My Drive\00_DEV\WinGlance", 100)]
    [InlineData("README.md - winglance - Visual Studio Code", @"z:\My Drive\00_DEV\WinGlance", 100)]
    [InlineData("README.md - WinGlance - Visual Studio Code", @"z:\My Drive\00_DEV\WinGlance\", 100)]
    [InlineData("WinGlance (Workspace) - Visual Studio Code", @"z:\My Drive\00_DEV\WinGlance", 100)]
    // Agent started in a subfolder: the workspace folder is one level up
    [InlineData("README.md - WinGlance - Visual Studio Code", @"z:\My Drive\00_DEV\WinGlance\src", 90)]
    [InlineData("README.md - WinGlance - Visual Studio Code", @"z:\My Drive\00_DEV\WinGlance\src\a\b", 70)]
    // No match, and the drive is never a match
    [InlineData("README.md - Other - Visual Studio Code", @"z:\My Drive\00_DEV\WinGlance", 0)]
    [InlineData("C: - Visual Studio Code", @"C:\", 0)]
    [InlineData("", @"C:\work", 0)]
    [InlineData("README.md - work - Visual Studio Code", "  ", 0)]
    public void ScoreTitle_RanksFullPathThenFolderNameByDepth(string title, string cwd, int expected)
    {
        Assert.Equal(expected, AgentWindowMatcher.ScoreTitle(title, cwd));
    }
}
