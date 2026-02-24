using WinGlance.Models;
using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class WindowEnumeratorTests
{
    [Fact]
    public void GetWindows_EmptyMonitoredApps_ReturnsEmpty()
    {
        var enumerator = new WindowEnumerator();
        var result = enumerator.GetWindows([]);

        Assert.Empty(result);
    }

    [Fact]
    public void GetWindows_WithMonitoredApp_FindsMatchingWindows()
    {
        // Monitor an app that's certainly running — the test host process
        var testProcessName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        var apps = new List<MonitoredApp> { new(testProcessName, testProcessName) };

        var enumerator = new WindowEnumerator();
        var result = enumerator.GetWindows(apps);

        // The test runner process may or may not have visible windows.
        // We just verify it doesn't crash and returns a valid list.
        Assert.NotNull(result);
    }

    [Fact]
    public void DiscoverRunningApps_ReturnsNonEmptyList()
    {
        var enumerator = new WindowEnumerator();
        var apps = enumerator.DiscoverRunningApps();

        Assert.NotNull(apps);
        Assert.True(apps.Count > 0, "There should be at least one running app with a visible window");
    }

    [Fact]
    public void DiscoverRunningApps_EachEntryHasPositiveWindowCount()
    {
        var enumerator = new WindowEnumerator();
        var apps = enumerator.DiscoverRunningApps();

        foreach (var app in apps)
        {
            Assert.True(app.WindowCount > 0, $"{app.ProcessName} should have at least 1 window");
            Assert.False(string.IsNullOrWhiteSpace(app.ProcessName));
        }
    }

    // ── AUMID parsing ───────────────────────────────────────────────────

    [Theory]
    [InlineData("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", "WindowsCalculator")]
    [InlineData("Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", "WindowsTerminal")]
    [InlineData("CompanyName.AppName_hash!EntryPoint", "AppName")]
    [InlineData("SimpleApp_hash!Main", "SimpleApp")]
    public void ExtractAppNameFromAumid_ParsesCorrectly(string aumid, string expected)
    {
        var result = WindowEnumerator.ExtractAppNameFromAumid(aumid);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExtractAppNameFromAumid_NoSeparators_ReturnsInput()
    {
        var result = WindowEnumerator.ExtractAppNameFromAumid("SimpleApp");
        Assert.Equal("SimpleApp", result);
    }

    // ── Filtering ───────────────────────────────────────────────────────

    [Fact]
    public void IsEligibleWindow_ZeroHandle_ReturnsFalse()
    {
        // IntPtr.Zero is not a valid window
        var result = WindowEnumerator.IsEligibleWindow(IntPtr.Zero);
        Assert.False(result);
    }
}
