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
}
