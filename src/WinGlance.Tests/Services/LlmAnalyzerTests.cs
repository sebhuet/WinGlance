using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class LlmAnalyzerTests
{
    [Theory]
    [InlineData("awaiting_action", "awaiting_action")]
    [InlineData("idle", "idle")]
    [InlineData("The window is awaiting_action from the user.", "awaiting_action")]
    [InlineData("This window appears to be idle.", "idle")]
    [InlineData("AWAITING_ACTION", "awaiting_action")]
    [InlineData("IDLE", "idle")]
    [InlineData("something unexpected", "idle")]
    [InlineData("", "idle")]
    public void ParseVerdict_ReturnsExpectedResult(string response, string expected)
    {
        Assert.Equal(expected, LlmAnalyzer.ParseVerdict(response));
    }

    [Fact]
    public void ParseVerdict_AwaitingTakesPriorityOverIdle()
    {
        // If both keywords appear, awaiting_action wins
        var result = LlmAnalyzer.ParseVerdict("awaiting_action or idle");
        Assert.Equal("awaiting_action", result);
    }
}
