using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class AgentMonitorTests
{
    [Theory]
    [InlineData(200, 1000)]
    [InlineData(5000, 5000)]
    [InlineData(600000, 60000)]
    public void RefreshInterval_IsClampedBetweenOneAndSixtySeconds(int requestedMs, int expectedMs)
    {
        var monitor = new AgentMonitor(requestedMs, new ClaudeAgentSource(null));

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), monitor.RefreshInterval);
    }

    [Fact]
    public void GetBadges_FirstCall_ReturnsEmptyWhileDiscoveryRunsInBackground()
    {
        var monitor = new AgentMonitor(5000, new ClaudeAgentSource(null));

        Assert.Empty(monitor.GetBadges([]));
    }
}
