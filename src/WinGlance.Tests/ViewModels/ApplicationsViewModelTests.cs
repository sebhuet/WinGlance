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
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    private ApplicationsViewModel CreateVm() => CreateVm([]);

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

    // Deterministic scan: the tests used to depend on whatever happened to run on the machine
    private static List<DiscoveredApp> Running(params (string Name, int Windows)[] apps) =>
        apps.Select(a => new DiscoveredApp(a.Name, a.Windows)).ToList();

    private ApplicationsViewModel CreateVm(
        List<DiscoveredApp> running,
        Action<IReadOnlyList<MonitoredApp>>? callback = null)
    {
        return new ApplicationsViewModel(_configService, _config, callback ?? (_ => { }), () => running);
    }

    [Fact]
    public void Refresh_ListsRunningApps()
    {
        var vm = CreateVm(Running(("Code", 3), ("notepad", 1)));

        vm.Refresh();

        Assert.Equal(["Code", "notepad"], vm.Apps.Select(a => a.ProcessName));
        Assert.Equal(3, vm.Apps[0].WindowCount);
        Assert.All(vm.Apps, a => Assert.False(a.IsMonitored));
    }

    [Fact]
    public void Refresh_PreservesMonitoredStateAndDisplayName()
    {
        _config.MonitoredApps.Add(new MonitoredAppConfig { ProcessName = "code", DisplayName = "VS Code" });
        var vm = CreateVm(Running(("Code", 3)));

        vm.Refresh();

        var entry = Assert.Single(vm.Apps);
        Assert.True(entry.IsMonitored);
        Assert.Equal("VS Code", entry.DisplayName);
    }

    [Fact]
    public void Refresh_KeepsMonitoredAppsThatAreNotRunning()
    {
        _config.MonitoredApps.Add(new MonitoredAppConfig { ProcessName = "Slack", DisplayName = "Slack" });
        var vm = CreateVm(Running(("Code", 3)));

        vm.Refresh();

        var slack = Assert.Single(vm.Apps, a => a.ProcessName == "Slack");
        Assert.True(slack.IsMonitored);
        Assert.Equal(0, slack.WindowCount);
    }

    [Fact]
    public void Refresh_DuplicateMonitoredEntries_DoNotThrow()
    {
        _config.MonitoredApps.Add(new MonitoredAppConfig { ProcessName = "Code", DisplayName = "First" });
        _config.MonitoredApps.Add(new MonitoredAppConfig { ProcessName = "CODE", DisplayName = "Second" });
        var vm = CreateVm(Running(("Code", 1)));

        vm.Refresh();

        Assert.Equal("First", Assert.Single(vm.Apps).DisplayName);
    }

    [Fact]
    public void Save_UpdatesConfigAndNotifiesCallback()
    {
        IReadOnlyList<MonitoredApp>? callbackResult = null;
        var vm = CreateVm(Running(("Code", 3), ("notepad", 1)), apps => callbackResult = apps);
        vm.Refresh();

        vm.Apps[0].IsMonitored = true;
        vm.Apps[0].DisplayName = "VS Code";
        vm.Save();

        var saved = Assert.Single(_config.MonitoredApps);
        Assert.Equal(("Code", "VS Code"), (saved.ProcessName, saved.DisplayName));
        Assert.Equal("VS Code", Assert.Single(callbackResult!).DisplayName);
        Assert.Single(_configService.Load().MonitoredApps);
    }

    [Fact]
    public void Save_KeepsMonitoredAppsThatAreNotRunning()
    {
        _config.MonitoredApps.Add(new MonitoredAppConfig { ProcessName = "Slack", DisplayName = "Slack" });
        var vm = CreateVm(Running(("Code", 3)));
        vm.Refresh();

        vm.Save();

        Assert.Equal("Slack", Assert.Single(_config.MonitoredApps).ProcessName);
    }

    [Fact]
    public void Save_EmptyDisplayName_FallsBackToProcessName()
    {
        var vm = CreateVm(Running(("Code", 3)));
        vm.Refresh();
        vm.Apps[0].IsMonitored = true;
        vm.Apps[0].DisplayName = "  ";

        vm.Save();

        Assert.Equal("Code", Assert.Single(_config.MonitoredApps).DisplayName);
    }

    [Fact]
    public void Save_WithNoMonitoredApps_ClearsList()
    {
        _config.MonitoredApps.Add(new MonitoredAppConfig { ProcessName = "old", DisplayName = "Old App" });
        var vm = CreateVm(Running(("Code", 3)));
        vm.Refresh();

        foreach (var app in vm.Apps)
            app.IsMonitored = false;
        vm.Save();

        Assert.Empty(_config.MonitoredApps);
    }

    [Fact]
    public void Constructor_WithoutDiscovery_UsesTheRealScan()
    {
        var vm = new ApplicationsViewModel(_configService, _config, _ => { });

        var exception = Record.Exception(vm.Refresh);

        Assert.Null(exception);
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
