using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class AutoStartServiceTests
{
    [Fact]
    public void IsAutoStartEnabled_ReturnsBoolean()
    {
        // Should not throw — reads registry
        var result = AutoStartService.IsAutoStartEnabled();
        Assert.IsType<bool>(result);
    }
}
