using WinGlance.Models;
using WinGlance.Services;
using WinGlance.ViewModels;

namespace WinGlance.Tests.ViewModels;

public class MainViewModelTests
{
    private static MainViewModel CreateVm(AppConfig? config = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"WinGlance_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var svc = new ConfigService(tempDir);
        return new MainViewModel(svc, config ?? new AppConfig());
    }

    [Fact]
    public void SelectedTabIndex_DefaultsToZero()
    {
        var vm = CreateVm();

        Assert.Equal(0, vm.SelectedTabIndex);
    }

    [Fact]
    public void SelectedTabIndex_RaisesPropertyChanged()
    {
        var vm = CreateVm();
        var raised = false;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedTabIndex))
                raised = true;
        };

        vm.SelectedTabIndex = 2;

        Assert.True(raised);
        Assert.Equal(2, vm.SelectedTabIndex);
    }

    [Fact]
    public void SelectedTabIndex_DoesNotRaise_WhenSameValue()
    {
        var vm = CreateVm();
        var raised = false;

        vm.PropertyChanged += (_, _) => raised = true;

        vm.SelectedTabIndex = 0;

        Assert.False(raised);
    }

    [Fact]
    public void Constructor_AppliesConfigToPreviewViewModel()
    {
        var config = new AppConfig
        {
            Layout = "grid",
            ThumbnailWidth = 300,
            ThumbnailHeight = 200,
            PollingIntervalMs = 2000,
            MonitoredApps =
            [
                new MonitoredAppConfig { ProcessName = "notepad", DisplayName = "Notepad" },
            ],
        };

        var vm = CreateVm(config);

        Assert.Equal("grid", vm.PreviewViewModel.Layout);
        Assert.Equal(300, vm.PreviewViewModel.ThumbnailWidth);
        Assert.Equal(200, vm.PreviewViewModel.ThumbnailHeight);
        Assert.Equal(2000, vm.PreviewViewModel.PollingIntervalMs);
        Assert.Single(vm.PreviewViewModel.MonitoredApps);
        Assert.Equal("notepad", vm.PreviewViewModel.MonitoredApps[0].ProcessName);
    }

    [Fact]
    public void Constructor_ExposesConfigAndConfigService()
    {
        var vm = CreateVm();

        Assert.NotNull(vm.Config);
        Assert.NotNull(vm.ConfigService);
    }

    [Fact]
    public void Constructor_AgentDetectionEnabled_GivesPreviewAMonitor()
    {
        var config = new AppConfig { AgentDetection = { Enabled = true, RefreshIntervalMs = 3000 } };

        var vm = CreateVm(config);

        Assert.NotNull(vm.PreviewViewModel.AgentMonitor);
        Assert.Equal(TimeSpan.FromSeconds(3), vm.PreviewViewModel.AgentMonitor.RefreshInterval);
    }

    // ── Panel position restore ───────────────────────────────────────────

    private static readonly System.Windows.Rect TwoMonitors = new(0, 0, 3840, 1080);

    [Fact]
    public void GetRestorablePosition_OnScreen_ReturnsTheSavedPosition()
    {
        var config = new AppConfig { PanelX = 2500, PanelY = 300 };

        Assert.Equal(new System.Windows.Point(2500, 300), MainViewModel.GetRestorablePosition(config, TwoMonitors));
    }

    [Theory]
    [InlineData(4000, 300)] // second monitor disconnected
    [InlineData(100, -200)] // title bar above the screen
    [InlineData(-500, 100)]
    [InlineData(double.NaN, 100)]
    public void GetRestorablePosition_TitleBarOffScreen_ReturnsNull(double x, double y)
    {
        var config = new AppConfig { PanelX = x, PanelY = y };

        Assert.Null(MainViewModel.GetRestorablePosition(config, TwoMonitors));
    }

    [Fact]
    public void GetRestorablePosition_RememberPositionOff_ReturnsNull()
    {
        var config = new AppConfig { RememberPosition = false, PanelX = 100, PanelY = 100 };

        Assert.Null(MainViewModel.GetRestorablePosition(config, TwoMonitors));
    }

    [Fact]
    public void Constructor_AgentDetectionDisabled_LeavesPreviewWithoutMonitor()
    {
        var config = new AppConfig { AgentDetection = { Enabled = false } };

        var vm = CreateVm(config);

        Assert.Null(vm.PreviewViewModel.AgentMonitor);
    }
}
