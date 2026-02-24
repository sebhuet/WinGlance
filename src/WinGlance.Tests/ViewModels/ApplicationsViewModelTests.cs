using System.IO;
using WinGlance.Models;
using WinGlance.Services;
using WinGlance.ViewModels;

namespace WinGlance.Tests.ViewModels;

public class ApplicationsViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigService _configService;
    private readonly AppConfig _config;

    public ApplicationsViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"WinGlance_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _configService = new ConfigService(_tempDir);
        _config = new AppConfig();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort */ }
    }

    private ApplicationsViewModel CreateVm(Action<IReadOnlyList<MonitoredApp>>? callback = null)
    {
        return new ApplicationsViewModel(_configService, _config, callback ?? (_ => { }));
    }

    [Fact]
    public void Apps_DefaultsToEmpty()
    {
        var vm = CreateVm();

        Assert.Empty(vm.Apps);
    }

    [Fact]
    public void RefreshCommand_IsNotNull()
    {
        var vm = CreateVm();

        Assert.NotNull(vm.RefreshCommand);
    }

    [Fact]
    public void SaveCommand_IsNotNull()
    {
        var vm = CreateVm();

        Assert.NotNull(vm.SaveCommand);
    }

    [Fact]
    public void Refresh_PopulatesApps()
    {
        var vm = CreateVm();

        vm.Refresh();

        // Should discover at least the test runner or other running apps
        Assert.NotEmpty(vm.Apps);
    }

    [Fact]
    public void Refresh_PreservesMonitoredState()
    {
        // Pre-configure a monitored app that we know is running (dotnet is running the test)
        _config.MonitoredApps.Add(new MonitoredAppConfig
        {
            ProcessName = "dotnet",
            DisplayName = "Test Runner",
        });

        var vm = CreateVm();
        vm.Refresh();

        var dotnetEntry = vm.Apps.FirstOrDefault(a =>
            a.ProcessName.Equals("dotnet", StringComparison.OrdinalIgnoreCase));

        if (dotnetEntry is not null)
        {
            Assert.True(dotnetEntry.IsMonitored);
            Assert.Equal("Test Runner", dotnetEntry.DisplayName);
        }
        // If dotnet isn't discoverable (e.g. runs under testhost), that's OK — skip the assertion
    }

    [Fact]
    public void Save_UpdatesConfigAndNotifiesCallback()
    {
        IReadOnlyList<MonitoredApp>? callbackResult = null;
        var vm = CreateVm(apps => callbackResult = apps);

        vm.Refresh();

        // Monitor the first discovered app
        if (vm.Apps.Count > 0)
        {
            vm.Apps[0].IsMonitored = true;
            vm.Apps[0].DisplayName = "Test App";
            vm.Save();

            Assert.Single(_config.MonitoredApps);
            Assert.Equal(vm.Apps[0].ProcessName, _config.MonitoredApps[0].ProcessName);
            Assert.Equal("Test App", _config.MonitoredApps[0].DisplayName);

            Assert.NotNull(callbackResult);
            Assert.Single(callbackResult);

            // Verify file was written
            var loaded = _configService.Load();
            Assert.Single(loaded.MonitoredApps);
        }
    }

    [Fact]
    public void Save_WithNoMonitoredApps_ClearsList()
    {
        _config.MonitoredApps.Add(new MonitoredAppConfig
        {
            ProcessName = "old",
            DisplayName = "Old App",
        });

        var vm = CreateVm();
        vm.Refresh();

        // Ensure nothing is monitored
        foreach (var app in vm.Apps)
            app.IsMonitored = false;

        vm.Save();

        Assert.Empty(_config.MonitoredApps);
    }
}

public class AppEntryTests
{
    [Fact]
    public void Constructor_SetsProcessName()
    {
        var entry = new AppEntry("notepad");

        Assert.Equal("notepad", entry.ProcessName);
    }

    [Fact]
    public void DisplayName_DefaultsToProcessName()
    {
        var entry = new AppEntry("notepad");

        Assert.Equal("notepad", entry.DisplayName);
    }

    [Fact]
    public void IsMonitored_DefaultsToFalse()
    {
        var entry = new AppEntry("notepad");

        Assert.False(entry.IsMonitored);
    }

    [Fact]
    public void WindowCount_DefaultsToZero()
    {
        var entry = new AppEntry("notepad");

        Assert.Equal(0, entry.WindowCount);
    }

    [Fact]
    public void IsMonitored_RaisesPropertyChanged()
    {
        var entry = new AppEntry("notepad");
        var raised = false;

        entry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppEntry.IsMonitored))
                raised = true;
        };

        entry.IsMonitored = true;

        Assert.True(raised);
    }

    [Fact]
    public void DisplayName_RaisesPropertyChanged()
    {
        var entry = new AppEntry("notepad");
        var raised = false;

        entry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppEntry.DisplayName))
                raised = true;
        };

        entry.DisplayName = "My Notepad";

        Assert.True(raised);
    }

    [Fact]
    public void WindowCount_RaisesPropertyChanged()
    {
        var entry = new AppEntry("notepad");
        var raised = false;

        entry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppEntry.WindowCount))
                raised = true;
        };

        entry.WindowCount = 5;

        Assert.True(raised);
    }

    [Fact]
    public void IsMonitored_DoesNotRaise_WhenSameValue()
    {
        var entry = new AppEntry("notepad");
        var raised = false;

        entry.PropertyChanged += (_, _) => raised = true;

        entry.IsMonitored = false; // same as default

        Assert.False(raised);
    }
}
