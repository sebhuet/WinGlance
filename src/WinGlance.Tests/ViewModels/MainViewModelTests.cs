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

    // ── View navigation ──────────────────────────────────────────────────

    [Theory]
    [InlineData(0, false, "")]
    [InlineData(1, true, "Applications")]
    [InlineData(2, true, "Settings")]
    [InlineData(3, true, "Prompt Editor")]
    public void SelectedTabIndex_TellsWhetherABackButtonIsNeeded(int index, bool isSecondary, string title)
    {
        var vm = CreateVm();

        vm.SelectedTabIndex = index;

        Assert.Equal(isSecondary, vm.IsSecondaryView);
        Assert.Equal(title, vm.ViewTitle);
    }

    [Fact]
    public void SelectedTabIndex_RaisesTheBackButtonProperties()
    {
        var vm = CreateVm();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.SelectedTabIndex = 2;

        Assert.Contains(nameof(MainViewModel.IsSecondaryView), raised);
        Assert.Contains(nameof(MainViewModel.ViewTitle), raised);
    }

    // ── LLM service lifecycle ────────────────────────────────────────────

    [Fact]
    public void Constructor_LlmDisabled_HasNoService()
    {
        var vm = CreateVm();

        Assert.Null(vm.PreviewViewModel.LlmService);
        Assert.Null(vm.PromptEditorViewModel.RunAnalysisFunc);
    }

    [Fact]
    public void Constructor_LlmEnabled_StartsTheServiceAndTheRunButton()
    {
        var vm = CreateVm(new AppConfig { Llm = { Enabled = true } });

        Assert.NotNull(vm.PreviewViewModel.LlmService);
        Assert.NotNull(vm.PromptEditorViewModel.RunAnalysisFunc);
    }

    [Fact]
    public void ApplyLlmSettings_TurnedOn_StartsWithoutRestart()
    {
        var vm = CreateVm();

        vm.Config.Llm.Enabled = true;
        vm.ApplyLlmSettings();

        Assert.NotNull(vm.PreviewViewModel.LlmService);
    }

    [Fact]
    public void ApplyLlmSettings_TurnedOff_StopsTheServiceAndClearsVerdicts()
    {
        var vm = CreateVm(new AppConfig { Llm = { Enabled = true } });
        var window = new TrackedWindow((IntPtr)1, "Code", "VS Code") { LlmVerdict = "idle", IsStale = true };
        vm.PreviewViewModel.Windows.Add(window);

        vm.Config.Llm.Enabled = false;
        vm.ApplyLlmSettings();

        Assert.Null(vm.PreviewViewModel.LlmService);
        Assert.Null(vm.PromptEditorViewModel.RunAnalysisFunc);
        Assert.Null(window.LlmVerdict);
        Assert.False(window.IsStale);
    }

    [Fact]
    public void SavingLlmSettings_RebuildsTheService()
    {
        var vm = CreateVm(new AppConfig { Llm = { Enabled = true } });
        var before = vm.PreviewViewModel.LlmService;

        vm.SettingsViewModel.LlmModel = "another-model";
        vm.SettingsViewModel.Save();

        Assert.NotNull(vm.PreviewViewModel.LlmService);
        Assert.NotSame(before, vm.PreviewViewModel.LlmService);
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
