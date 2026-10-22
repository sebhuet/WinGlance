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

    [Theory]
    [InlineData("\"C:\\Tools\\WinGlance.exe\"", true, false)]
    [InlineData("C:\\Tools\\WinGlance.exe", true, false)]
    [InlineData("\"D:\\Moved\\WinGlance.exe\"", false, true)]
    [InlineData("", false, false)]
    [InlineData("\"\"", false, false)]
    public void IsStale_IsTrueOnlyWhenTheRegisteredExeIsMissing(string runValue, bool exists, bool expected)
    {
        Assert.Equal(expected, AutoStartService.IsStale(runValue, _ => exists));
    }

    [Fact]
    public void IsStale_ChecksThePathWithoutQuotes()
    {
        string? checkedPath = null;

        AutoStartService.IsStale("\"C:\\Tools\\WinGlance.exe\"", path =>
        {
            checkedPath = path;
            return true;
        });

        Assert.Equal("C:\\Tools\\WinGlance.exe", checkedPath);
    }
}
