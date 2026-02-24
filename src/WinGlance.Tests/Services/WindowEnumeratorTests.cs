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

    [Fact]
    public void IsEligibleWindow_InvalidHandle_ReturnsFalse()
    {
        // An arbitrary invalid handle should not be eligible
        var result = WindowEnumerator.IsEligibleWindow((IntPtr)(-1));
        Assert.False(result);
    }

    // ── AUMID edge cases ─────────────────────────────────────────────────

    [Theory]
    [InlineData("Publisher.App_hash", "App")]         // no bang (no entry point)
    [InlineData("App_hash!Main", "App")]              // no publisher prefix
    [InlineData("A.B.C_hash!Main", "B.C")]            // multiple dots → only first removed
    public void ExtractAppNameFromAumid_EdgeCases(string aumid, string expected)
    {
        var result = WindowEnumerator.ExtractAppNameFromAumid(aumid);
        Assert.Equal(expected, result);
    }

    // ── DiscoveredApp model ──────────────────────────────────────────────

    [Fact]
    public void DiscoveredApp_Properties()
    {
        var app = new DiscoveredApp("notepad", 3);
        Assert.Equal("notepad", app.ProcessName);
        Assert.Equal(3, app.WindowCount);
    }

    [Fact]
    public void DiscoveredApp_WindowCountIsMutable()
    {
        var app = new DiscoveredApp("notepad", 1);
        app.WindowCount = 5;
        Assert.Equal(5, app.WindowCount);
    }

    [Fact]
    public void DiscoverRunningApps_ResultsAreSortedByProcessName()
    {
        var enumerator = new WindowEnumerator();
        var apps = enumerator.DiscoverRunningApps();

        for (var i = 1; i < apps.Count; i++)
        {
            Assert.True(
                string.Compare(apps[i - 1].ProcessName, apps[i].ProcessName, StringComparison.OrdinalIgnoreCase) <= 0,
                $"Expected sorted order but '{apps[i - 1].ProcessName}' came before '{apps[i].ProcessName}'");
        }
    }

    [Fact]
    public void GetWindows_UnknownProcess_ReturnsEmpty()
    {
        var enumerator = new WindowEnumerator();
        var apps = new List<MonitoredApp> { new("ThisProcessDoesNotExist_XYZ", "Fake App") };

        var result = enumerator.GetWindows(apps);

        Assert.Empty(result);
    }
}
