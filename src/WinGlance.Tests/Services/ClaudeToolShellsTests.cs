using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class ClaudeToolShellsTests
{
    // Command lines observed under claude.exe 2.1.30x on Windows
    private const string BashTool =
        "E:\\Tools\\Git\\bin\\bash.exe -c \"source /c/Users/me/.claude/shell-snapshots/snapshot-bash-1792596292664-27sswy.sh 2>/dev/null || true && py scripts/serveur.py\"";
    private const string PowerShellTool =
        "C:\\Windows\\System32\\cmd.exe /d /s /c \"\"C:\\Windows\\System32\\chcp.com\" 65001 >nul & \"powershell.exe\" -NoProfile -Command \"$__claudeCodeScript = $env:CLAUDE_CODE_SHELL_LAUNCHER_SCRIPT; …\"\"";
    private const string McpServer = "cmd /c npx -y mcp-server-gsc";
    private const string McpPowerShell = "powershell -NoProfile -ExecutionPolicy Bypass -File D:\\mcp\\gmail.ps1";

    [Theory]
    [InlineData(BashTool, true)]
    [InlineData(PowerShellTool, true)]
    [InlineData(McpServer, false)]
    [InlineData(McpPowerShell, false)]
    [InlineData("\\??\\C:\\Windows\\system32\\conhost.exe 0x4", false)]
    [InlineData(null, false)]
    public void IsToolShell_RecognizesOnlyClaudeToolShells(string? commandLine, bool expected)
    {
        Assert.Equal(expected, ClaudeToolShells.IsToolShell(commandLine));
    }

    [Fact]
    public void CountRunning_CountsToolShellChildrenOnly()
    {
        // claude.exe 100 runs two background commands next to its MCP servers; the tool shell
        // of another session (200) and a grandchild shell are not its direct commands
        var tree = new ProcessTree([
            new ProcessInfo(100, 1, "claude.exe"),
            new ProcessInfo(101, 100, "conhost.exe"),
            new ProcessInfo(102, 100, "cmd.exe"),
            new ProcessInfo(103, 100, "bash.exe"),
            new ProcessInfo(104, 100, "cmd.exe"),
            new ProcessInfo(105, 103, "bash.exe"),
            new ProcessInfo(200, 1, "claude.exe"),
            new ProcessInfo(201, 200, "bash.exe"),
        ]);
        var commandLines = new Dictionary<int, string>
        {
            [101] = "conhost.exe 0x4",
            [102] = McpServer,
            [103] = BashTool,
            [104] = PowerShellTool,
            [105] = BashTool,
            [201] = BashTool,
        };

        var count = ClaudeToolShells.CountRunning(tree, 100, pid => commandLines.GetValueOrDefault(pid));

        Assert.Equal(2, count);
    }

    [Fact]
    public void CountRunning_UnreadableCommandLines_CountNothing()
    {
        var tree = new ProcessTree([new ProcessInfo(100, 1, "claude.exe"), new ProcessInfo(103, 100, "bash.exe")]);

        Assert.Equal(0, ClaudeToolShells.CountRunning(tree, 100, _ => null));
    }
}
