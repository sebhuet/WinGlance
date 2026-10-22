using WinGlance.Models;
using WinGlance.ViewModels;

namespace WinGlance.Tests.ViewModels;

public class PreviewViewModelTests
{
    // ── Constructor defaults ─────────────────────────────────────────────

    [Fact]
    public void Windows_DefaultsToEmptyCollection()
    {
        using var vm = new PreviewViewModel();
        Assert.NotNull(vm.Windows);
        Assert.Empty(vm.Windows);
    }

    [Fact]
    public void GroupedWindows_IsNotNull()
    {
        using var vm = new PreviewViewModel();
        Assert.NotNull(vm.GroupedWindows);
    }

    [Fact]
    public void ThumbnailManager_IsNotNull()
    {
        using var vm = new PreviewViewModel();
        Assert.NotNull(vm.ThumbnailManager);
    }

    [Fact]
    public void PollingIntervalMs_DefaultIs1000()
    {
        using var vm = new PreviewViewModel();
        Assert.Equal(1000, vm.PollingIntervalMs);
    }

    [Fact]
    public void IsPolling_DefaultIsFalse()
    {
        using var vm = new PreviewViewModel();
        Assert.False(vm.IsPolling);
    }

    [Fact]
    public void MonitoredApps_DefaultIsEmpty()
    {
        using var vm = new PreviewViewModel();
        Assert.Empty(vm.MonitoredApps);
    }

    // ── Layout property ──────────────────────────────────────────────────

    [Fact]
    public void Layout_DefaultsToFit()
    {
        using var vm = new PreviewViewModel();
        Assert.Equal("fit", vm.Layout);
    }

    [Fact]
    public void Layout_RaisesPropertyChanged()
    {
        using var vm = new PreviewViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PreviewViewModel.Layout))
                raised = true;
        };

        vm.Layout = "grid";
        Assert.True(raised);
        Assert.Equal("grid", vm.Layout);
    }

    [Fact]
    public void Layout_DoesNotRaise_WhenSameValue()
    {
        using var vm = new PreviewViewModel();
        var raised = false;
        vm.PropertyChanged += (_, _) => raised = true;

        vm.Layout = "fit"; // same as default

        Assert.False(raised);
    }

    [Theory]
    [InlineData("horizontal")]
    [InlineData("vertical")]
    [InlineData("grid")]
    public void Layout_AcceptsValidValues(string layout)
    {
        using var vm = new PreviewViewModel();
        vm.Layout = layout;
        Assert.Equal(layout, vm.Layout);
    }

    // ── ThumbnailWidth property ──────────────────────────────────────────

    [Fact]
    public void ThumbnailWidth_DefaultIs200()
    {
        using var vm = new PreviewViewModel();
        Assert.Equal(200, vm.ThumbnailWidth);
    }

    [Fact]
    public void ThumbnailWidth_RaisesPropertyChanged()
    {
        using var vm = new PreviewViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PreviewViewModel.ThumbnailWidth))
                raised = true;
        };

        vm.ThumbnailWidth = 300;

        Assert.True(raised);
        Assert.Equal(300, vm.ThumbnailWidth);
    }

    [Fact]
    public void ThumbnailWidth_DoesNotRaise_WhenSameValue()
    {
        using var vm = new PreviewViewModel();
        var raised = false;
        vm.PropertyChanged += (_, _) => raised = true;

        vm.ThumbnailWidth = 200; // same as default

        Assert.False(raised);
    }

    // ── ThumbnailHeight property ─────────────────────────────────────────

    [Fact]
    public void ThumbnailHeight_DefaultIs150()
    {
        using var vm = new PreviewViewModel();
        Assert.Equal(150, vm.ThumbnailHeight);
    }

    [Fact]
    public void ThumbnailHeight_RaisesPropertyChanged()
    {
        using var vm = new PreviewViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PreviewViewModel.ThumbnailHeight))
                raised = true;
        };

        vm.ThumbnailHeight = 250;

        Assert.True(raised);
        Assert.Equal(250, vm.ThumbnailHeight);
    }

    [Fact]
    public void ThumbnailHeight_DoesNotRaise_WhenSameValue()
    {
        using var vm = new PreviewViewModel();
        var raised = false;
        vm.PropertyChanged += (_, _) => raised = true;

        vm.ThumbnailHeight = 150; // same as default

        Assert.False(raised);
    }

    // ── PollingIntervalMs clamping ───────────────────────────────────────

    [Fact]
    public void PollingIntervalMs_ClampedToMin500()
    {
        using var vm = new PreviewViewModel();
        vm.PollingIntervalMs = 100;
        Assert.Equal(500, vm.PollingIntervalMs);
    }

    [Fact]
    public void PollingIntervalMs_ClampedToMax5000()
    {
        using var vm = new PreviewViewModel();
        vm.PollingIntervalMs = 10000;
        Assert.Equal(5000, vm.PollingIntervalMs);
    }

    [Fact]
    public void PollingIntervalMs_AcceptsValueInRange()
    {
        using var vm = new PreviewViewModel();
        vm.PollingIntervalMs = 2500;
        Assert.Equal(2500, vm.PollingIntervalMs);
    }

    [Fact]
    public void PollingIntervalMs_RaisesPropertyChanged()
    {
        using var vm = new PreviewViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PreviewViewModel.PollingIntervalMs))
                raised = true;
        };

        vm.PollingIntervalMs = 2000;

        Assert.True(raised);
    }

    [Fact]
    public void PollingIntervalMs_DoesNotRaise_WhenClampedToSameValue()
    {
        using var vm = new PreviewViewModel();
        vm.PollingIntervalMs = 500; // set to min boundary
        var raised = false;
        vm.PropertyChanged += (_, _) => raised = true;

        vm.PollingIntervalMs = 100; // clamped to 500 again → no change

        Assert.False(raised);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(2500)]
    [InlineData(5000)]
    public void PollingIntervalMs_BoundaryValues(int interval)
    {
        using var vm = new PreviewViewModel();
        vm.PollingIntervalMs = interval;
        Assert.Equal(interval, vm.PollingIntervalMs);
    }

    [Theory]
    [InlineData(0, 500)]
    [InlineData(-1, 500)]
    [InlineData(499, 500)]
    [InlineData(5001, 5000)]
    [InlineData(int.MaxValue, 5000)]
    public void PollingIntervalMs_ClampingEdgeCases(int input, int expected)
    {
        using var vm = new PreviewViewModel();
        vm.PollingIntervalMs = input;
        Assert.Equal(expected, vm.PollingIntervalMs);
    }

    // ── MonitoredApps ────────────────────────────────────────────────────

    [Fact]
    public void MonitoredApps_CanBeSet()
    {
        using var vm = new PreviewViewModel();
        var apps = new List<MonitoredApp> { new("Code", "VS Code") };

        vm.MonitoredApps = apps;

        Assert.Single(vm.MonitoredApps);
        Assert.Equal("Code", vm.MonitoredApps[0].ProcessName);
    }

    [Fact]
    public void MonitoredApps_CanBeSetToMultipleApps()
    {
        using var vm = new PreviewViewModel();
        var apps = new List<MonitoredApp>
        {
            new("Code", "VS Code"),
            new("notepad", "Notepad"),
            new("explorer", "Explorer"),
        };

        vm.MonitoredApps = apps;

        Assert.Equal(3, vm.MonitoredApps.Count);
    }

    // ── Dispose ──────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var vm = new PreviewViewModel();
        var exception = Record.Exception(() => vm.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_StopsPolling()
    {
        var vm = new PreviewViewModel();
        vm.Dispose();
        Assert.False(vm.IsPolling);
    }

    [Fact]
    public void Start_StartsPolling_AndStopStopsIt()
    {
        using var vm = new PreviewViewModel();

        vm.Start(); // first scan runs on a background thread
        Assert.True(vm.IsPolling);

        vm.Stop();
        Assert.False(vm.IsPolling);
    }

    // ── Fit layout ───────────────────────────────────────────────────────

    private static PreviewViewModel FitViewModel(int windows)
    {
        var vm = new PreviewViewModel { Layout = PreviewViewModel.FitLayout };
        for (var i = 0; i < windows; i++)
            vm.Windows.Add(new TrackedWindow((IntPtr)(i + 1), "Code", "VS Code") { AspectRatio = 1.84 });
        return vm;
    }

    [Fact]
    public void FitLayout_ThumbnailsFollowThePanelSize()
    {
        using var vm = FitViewModel(5);

        vm.SetViewportSize(new System.Windows.Size(900, 560));
        var small = vm.DisplayThumbnailWidth;
        vm.SetViewportSize(new System.Windows.Size(1800, 1120));

        Assert.True(vm.DisplayThumbnailWidth > small * 1.8);
        Assert.Equal(1.84, vm.DisplayThumbnailWidth / vm.DisplayThumbnailHeight, precision: 1);
    }

    [Fact]
    public void FitLayout_IgnoresTheConfiguredSize()
    {
        using var vm = FitViewModel(1);
        vm.SetViewportSize(new System.Windows.Size(900, 560));

        vm.ThumbnailWidth = 120;

        Assert.NotEqual(120, vm.DisplayThumbnailWidth);
    }

    [Theory]
    [InlineData("horizontal")]
    [InlineData("vertical")]
    [InlineData("grid")]
    public void FixedLayouts_UseTheConfiguredSize(string layout)
    {
        using var vm = FitViewModel(5);
        vm.SetViewportSize(new System.Windows.Size(1800, 1120));
        vm.ThumbnailWidth = 240;
        vm.ThumbnailHeight = 130;

        vm.Layout = layout;

        Assert.Equal((240.0, 130.0), (vm.DisplayThumbnailWidth, vm.DisplayThumbnailHeight));
        Assert.False(vm.IsFitLayout);
    }

    [Fact]
    public void FitLayout_RaisesIsFitLayoutChanges()
    {
        using var vm = new PreviewViewModel { Layout = "grid" };
        var raised = false;
        vm.PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(PreviewViewModel.IsFitLayout);

        vm.Layout = PreviewViewModel.FitLayout;

        Assert.True(raised);
        Assert.True(vm.IsFitLayout);
    }

    [Fact]
    public void GetWindowsSnapshot_ReturnsACopy()
    {
        using var vm = new PreviewViewModel();
        vm.Windows.Add(new TrackedWindow((IntPtr)1, "a", "A"));

        var snapshot = vm.GetWindowsSnapshot();
        vm.Windows.Clear();

        Assert.Single(snapshot);
    }

    // ── Coding agents (FR-11) ────────────────────────────────────────────

    private static AgentBadge Badge(AgentStatus status) => new(status, status.ToString(), "");

    private static TrackedWindow WindowWithAgent(AgentStatus? status, bool isActive = false) =>
        new((IntPtr)1, "Code", "VS Code")
        {
            IsActive = isActive,
            Agent = status is null ? null : Badge(status.Value),
        };

    [Fact]
    public void AgentMonitor_DefaultsToNull()
    {
        using var vm = new PreviewViewModel();
        Assert.Null(vm.AgentMonitor);
    }

    [Fact]
    public void ApplyAgentState_SetsTheBadge()
    {
        var window = WindowWithAgent(null);
        var badge = Badge(AgentStatus.Busy);

        PreviewViewModel.ApplyAgentState(window, badge);

        Assert.Same(badge, window.Agent);
    }

    [Theory]
    [InlineData(AgentStatus.Waiting, false, true)]
    [InlineData(AgentStatus.Waiting, true, false)] // the user is looking at it
    [InlineData(AgentStatus.Busy, false, false)]
    [InlineData(AgentStatus.Running, false, false)]
    [InlineData(AgentStatus.Idle, false, false)] // idle from the start: nothing new to see
    public void ApplyAgentState_FromNoAgent_FlagsOnlyAnUnfocusedWaitingAgent(
        AgentStatus status, bool isActive, bool expected)
    {
        var window = WindowWithAgent(null, isActive);

        PreviewViewModel.ApplyAgentState(window, Badge(status));

        Assert.Equal(expected, window.NeedsAgentAttention);
    }

    [Fact]
    public void ApplyAgentState_BusyToIdle_FlagsTheFinishedTurn()
    {
        var window = WindowWithAgent(AgentStatus.Busy);

        PreviewViewModel.ApplyAgentState(window, Badge(AgentStatus.Idle));

        Assert.True(window.NeedsAgentAttention);
    }

    [Fact]
    public void ApplyAgentState_BusyToIdleWhileFocused_DoesNotFlag()
    {
        var window = WindowWithAgent(AgentStatus.Busy, isActive: true);

        PreviewViewModel.ApplyAgentState(window, Badge(AgentStatus.Idle));

        Assert.False(window.NeedsAgentAttention);
    }

    [Fact]
    public void ApplyAgentState_FinishedTurn_StaysFlaggedUntilTheWindowIsFocused()
    {
        var window = WindowWithAgent(AgentStatus.Busy);
        PreviewViewModel.ApplyAgentState(window, Badge(AgentStatus.Idle));

        PreviewViewModel.ApplyAgentState(window, Badge(AgentStatus.Idle));
        Assert.True(window.NeedsAgentAttention); // still unseen

        window.IsActive = true;
        PreviewViewModel.ApplyAgentState(window, Badge(AgentStatus.Idle));
        Assert.False(window.NeedsAgentAttention); // seen

        window.IsActive = false;
        PreviewViewModel.ApplyAgentState(window, Badge(AgentStatus.Idle));
        Assert.False(window.NeedsAgentAttention); // already seen, not flagged again
    }

    [Fact]
    public void ApplyAgentState_NewTurn_ClearsTheFinishedFlag()
    {
        var window = WindowWithAgent(AgentStatus.Busy);
        PreviewViewModel.ApplyAgentState(window, Badge(AgentStatus.Idle));

        PreviewViewModel.ApplyAgentState(window, Badge(AgentStatus.Busy));

        Assert.False(window.NeedsAgentAttention);
    }

    [Fact]
    public void ApplyAgentState_AgentGone_ClearsBadgeAndFlag()
    {
        var window = WindowWithAgent(AgentStatus.Waiting);
        window.NeedsAgentAttention = true;

        PreviewViewModel.ApplyAgentState(window, null);

        Assert.Null(window.Agent);
        Assert.False(window.NeedsAgentAttention);
    }

    [Fact]
    public void ApplyAgentState_WaitingAgain_FlagsAgainAfterFocusIsLost()
    {
        var window = WindowWithAgent(AgentStatus.Waiting, isActive: true);
        PreviewViewModel.ApplyAgentState(window, Badge(AgentStatus.Waiting));
        Assert.False(window.NeedsAgentAttention);

        window.IsActive = false;
        PreviewViewModel.ApplyAgentState(window, Badge(AgentStatus.Waiting));

        Assert.True(window.NeedsAgentAttention);
    }
}
