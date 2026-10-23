using WinGlance.Models;
using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class WindowTitleFormatTests
{
    private const string VsCodeTitle = "Adaptation française de la doc - Claude4Business - Visual Studio Code";

    private static TrackedWindow Window(string title, string? agentFolder = null)
    {
        var window = new TrackedWindow((IntPtr)1, "Code", "VS Code") { Title = title };
        if (agentFolder is not null)
        {
            var session = new AgentSession(AgentKind.Claude, 1, agentFolder, AgentStatus.Idle, null);
            window.Agent = AgentBadge.FromSessions([session]);
        }

        return window;
    }

    [Theory]
    [InlineData("{title}", VsCodeTitle)]
    [InlineData("{project}", "Claude4Business")]
    [InlineData("{document}", "Adaptation française de la doc")]
    [InlineData("{app}", "VS Code")]
    [InlineData("{project} · {document}", "Claude4Business · Adaptation française de la doc")]
    [InlineData("{PROJECT}", "Claude4Business")] // placeholders ignore case
    public void Apply_ReplacesThePlaceholders(string template, string expected)
    {
        Assert.Equal(expected, WindowTitleFormat.Apply(template, Window(VsCodeTitle)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Apply_BlankTemplate_ShowsTheTitle(string? template)
    {
        Assert.Equal(VsCodeTitle, WindowTitleFormat.Apply(template, Window(VsCodeTitle)));
    }

    [Fact]
    public void Apply_OnlySeparatorsLeft_FallsBackToTheTitle()
    {
        // A template whose fields come out empty must not leave the thumbnail unnamed
        Assert.Equal(VsCodeTitle, WindowTitleFormat.Apply(" · - ", Window(VsCodeTitle)));
    }

    [Fact]
    public void ProjectOf_AgentWindow_UsesTheAgentFolder()
    {
        Assert.Equal("BookAmPlus", WindowTitleFormat.ProjectOf(Window(VsCodeTitle, @"w:\00_DEV\BookAmPlus")));
    }

    [Theory]
    [InlineData("api-server", "api-server")]
    [InlineData("Untitled - Notepad", "Untitled")]
    [InlineData("● main.cs - WinGlance - Visual Studio Code", "WinGlance")]
    public void ProjectOf_FromTheTitle(string title, string expected)
    {
        Assert.Equal(expected, WindowTitleFormat.ProjectOf(Window(title)));
    }

    [Fact]
    public void DocumentOf_StripsTheUnsavedChangesDot()
    {
        Assert.Equal("main.cs", WindowTitleFormat.DocumentOf("● main.cs - WinGlance - Visual Studio Code"));
    }
}
