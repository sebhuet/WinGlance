using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class ThemeServiceTests
{
    [Fact]
    public void IsLightTheme_ReturnsBoolean()
    {
        // Should not throw — just returns true or false based on registry
        var result = ThemeService.IsLightTheme();
        Assert.IsType<bool>(result);
    }
}
