using WinGlance.Models;
using WinGlance.Services;
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
    public void StatusRowScale_FollowsTheDisplayedThumbnailWidth()
    {
        using var vm = FitViewModel(1);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.SetViewportSize(new System.Windows.Size(1800, 1120));

        Assert.Contains(nameof(PreviewViewModel.StatusRowScale), changed);
        Assert.True(vm.StatusRowScale > 1);
        Assert.Equal(vm.DisplayThumbnailWidth, vm.StatusRowWidth * vm.StatusRowScale, precision: 6);
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

    // ── Agent transitions (notifications) ────────────────────────────────

    [Theory]
    [InlineData(AgentStatus.Busy, AgentStatus.Idle, false, AgentTransition.Finished)]
    [InlineData(AgentStatus.Busy, AgentStatus.Idle, true, AgentTransition.None)] // the user is looking
    [InlineData(AgentStatus.Busy, AgentStatus.Waiting, false, AgentTransition.NeedsInput)]
    [InlineData(AgentStatus.Idle, AgentStatus.Waiting, false, AgentTransition.NeedsInput)]
    [InlineData(AgentStatus.Waiting, AgentStatus.Waiting, false, AgentTransition.None)] // already told
    [InlineData(AgentStatus.Idle, AgentStatus.Idle, false, AgentTransition.None)]
    [InlineData(AgentStatus.Idle, AgentStatus.Busy, false, AgentTransition.None)]
    [InlineData(null, AgentStatus.Waiting, false, AgentTransition.None)] // first seen (startup): no burst
    public void ApplyAgentState_ReportsTheTransitionToNotify(
        AgentStatus? previous, AgentStatus current, bool isActive, AgentTransition expected)
    {
        var window = WindowWithAgent(previous, isActive);

        Assert.Equal(expected, PreviewViewModel.ApplyAgentState(window, Badge(current)));
    }

    [Fact]
    public void MergeResults_RaisesAgentNeedsAttention_ForTransitionsOnly()
    {
        using var vm = new PreviewViewModel();
        var raised = new List<(IntPtr, AgentTransition)>();
        vm.AgentNeedsAttention += (window, transition) => raised.Add((window.Hwnd, transition));
        TrackedWindow Scan(int hwnd, AgentStatus status) =>
            new((IntPtr)hwnd, "Code", "VS Code") { Agent = Badge(status) };

        vm.MergeResults([Scan(1, AgentStatus.Busy), Scan(2, AgentStatus.Idle)]); // first scan: no event
        vm.MergeResults([Scan(1, AgentStatus.Idle), Scan(2, AgentStatus.Waiting)]);
        vm.MergeResults([Scan(1, AgentStatus.Idle), Scan(2, AgentStatus.Waiting)]); // nothing new

        Assert.Equal([((IntPtr)1, AgentTransition.Finished), ((IntPtr)2, AgentTransition.NeedsInput)], raised);
    }

    // ── Next agent ───────────────────────────────────────────────────────

    private static PreviewViewModel WithAgents(params (int Hwnd, AgentStatus? Status, bool Unseen)[] windows)
    {
        var vm = new PreviewViewModel();
        foreach (var (hwnd, status, unseen) in windows)
        {
            vm.Windows.Add(new TrackedWindow((IntPtr)hwnd, "Code", "VS Code")
            {
                Agent = status is null ? null : Badge(status.Value),
                NeedsAgentAttention = unseen,
            });
        }
        return vm;
    }

    [Fact]
    public void FindNextAgentWindow_GoesToWaitingAgentsFirst_ThenFinished_ThenIdle()
    {
        using var vm = WithAgents(
            (1, AgentStatus.Idle, false),
            (2, AgentStatus.Busy, false),
            (3, AgentStatus.Idle, true), // finished, not seen
            (4, null, false),
            (5, AgentStatus.Waiting, true));

        var visited = new List<IntPtr>();
        var current = IntPtr.Zero; // no tracked window in front
        for (var i = 0; i < 4; i++)
        {
            current = vm.FindNextAgentWindow(current)!.Hwnd;
            visited.Add(current);
        }

        // Busy and agent-less windows are skipped; the cycle starts over
        Assert.Equal([(IntPtr)5, 3, 1, 5], visited);
    }

    [Fact]
    public void FindNextAgentWindow_NoAgentNeedsTheUser_ReturnsNull()
    {
        using var vm = WithAgents((1, AgentStatus.Busy, false), (2, null, false));

        Assert.Null(vm.FindNextAgentWindow(IntPtr.Zero));
    }

    [Fact]
    public void FindNextAgentWindow_SingleCandidate_IsReturnedEvenWhenInFront()
    {
        using var vm = WithAgents((1, AgentStatus.Waiting, false));

        Assert.Equal((IntPtr)1, vm.FindNextAgentWindow((IntPtr)1)!.Hwnd);
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

    // ── MergeResults (one polling scan) ──────────────────────────────────

    private static TrackedWindow Scanned(int hwnd, string title = "Readme - Project") =>
        new((IntPtr)hwnd, "Code", "VS Code") { Title = title, AspectRatio = 1.6 };

    [Fact]
    public void MergeResults_NewWindows_AreAddedInScanOrder()
    {
        using var vm = new PreviewViewModel();

        vm.MergeResults([Scanned(1), Scanned(2)]);

        Assert.Equal(new[] { (IntPtr)1, (IntPtr)2 }, vm.Windows.Select(w => w.Hwnd));
    }

    [Fact]
    public void MergeResults_ClosedWindow_IsRemovedAndForgetsItsFlash()
    {
        // Window handles are reused: a later window with the same handle must not start flashing
        var detector = new AttentionDetector();
        using var vm = new PreviewViewModel { AttentionDetector = detector };
        vm.MergeResults([Scanned(1), Scanned(2)]);
        detector.HandleShellMessage(WinGlance.NativeApi.NativeMethods.HSHELL_FLASH, (IntPtr)2);

        vm.MergeResults([Scanned(1)]);

        Assert.Equal(new[] { (IntPtr)1 }, vm.Windows.Select(w => w.Hwnd));
        Assert.False(detector.IsFlashing((IntPtr)2));
    }

    [Fact]
    public void MergeResults_KnownWindow_UpdatesTheSameInstance()
    {
        // The thumbnail stays registered on the existing instance: it must not be replaced
        using var vm = new PreviewViewModel();
        vm.MergeResults([Scanned(1, "Old title")]);
        var original = vm.Windows.Single();

        var rescanned = Scanned(1, "New title");
        rescanned.IsActive = true;
        rescanned.IsHung = true;
        rescanned.IsModalBlocked = true;
        vm.MergeResults([rescanned]);

        var window = Assert.Single(vm.Windows);
        Assert.Same(original, window);
        Assert.Equal("New title", window.Title);
        Assert.True(window.IsActive);
        Assert.True(window.IsHung);
        Assert.True(window.IsModalBlocked);
    }

    [Fact]
    public void MergeResults_MinimizedWindow_KeepsTheLastAspectRatio()
    {
        // A minimized window reports an empty client area, hence a ratio of 0
        using var vm = new PreviewViewModel();
        vm.MergeResults([Scanned(1)]);

        var minimized = Scanned(1);
        minimized.AspectRatio = 0;
        vm.MergeResults([minimized]);

        Assert.Equal(1.6, vm.Windows.Single().AspectRatio);
    }

    [Fact]
    public void MergeResults_FlashingWindow_IsFlaggedUntilItGetsTheFocus()
    {
        var detector = new AttentionDetector();
        using var vm = new PreviewViewModel { AttentionDetector = detector };
        vm.MergeResults([Scanned(1)]);
        detector.HandleShellMessage(WinGlance.NativeApi.NativeMethods.HSHELL_FLASH, (IntPtr)1);

        vm.MergeResults([Scanned(1)]);
        Assert.True(vm.Windows.Single().IsFlashing);

        var focused = Scanned(1);
        focused.IsActive = true;
        vm.MergeResults([focused]);

        Assert.False(vm.Windows.Single().IsFlashing);
        Assert.False(detector.IsFlashing((IntPtr)1));
    }

    [Fact]
    public void MergeResults_Icon_IsOnlyFilledWhenMissing()
    {
        // The enumerator skips the icon of windows that already have one
        var first = System.Windows.Media.Imaging.BitmapSource.Create(
            1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[4], 4);
        var second = System.Windows.Media.Imaging.BitmapSource.Create(
            1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[4], 4);
        using var vm = new PreviewViewModel();
        vm.MergeResults([Scanned(1)]);

        var withIcon = Scanned(1);
        withIcon.Icon = first;
        vm.MergeResults([withIcon]);
        var withOtherIcon = Scanned(1);
        withOtherIcon.Icon = second;
        vm.MergeResults([withOtherIcon]);

        Assert.Same(first, vm.Windows.Single().Icon);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)] // in front: the question is asked where the user looks
    public void MergeResults_NewWindowWithWaitingAgent_NeedsAttentionUnlessInFront(bool isActive, bool expected)
    {
        using var vm = new PreviewViewModel();
        var window = Scanned(1);
        window.IsActive = isActive;
        window.Agent = Badge(AgentStatus.Waiting);

        vm.MergeResults([window]);

        Assert.Equal(expected, vm.Windows.Single().NeedsAgentAttention);
    }

    [Fact]
    public void MergeResults_AgentFinishesTurn_RaisesAgentNeedsAttentionOnce()
    {
        using var vm = new PreviewViewModel();
        var busy = Scanned(1);
        busy.Agent = Badge(AgentStatus.Busy);
        vm.MergeResults([busy]);
        var raised = new List<(IntPtr Hwnd, AgentTransition Transition)>();
        vm.AgentNeedsAttention += (window, transition) => raised.Add((window.Hwnd, transition));

        var idle = Scanned(1);
        idle.Agent = Badge(AgentStatus.Idle);
        vm.MergeResults([idle]);
        var stillIdle = Scanned(1);
        stillIdle.Agent = Badge(AgentStatus.Idle);
        vm.MergeResults([stillIdle]);

        Assert.Equal(new[] { ((IntPtr)1, AgentTransition.Finished) }, raised);
    }

    // ── Status time, reminders, order and filter ─────────────────────────

    private static readonly DateTime Noon = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

    private static TrackedWindow WithAgent(int hwnd, AgentStatus? status, bool isActive = false)
    {
        var window = Scanned(hwnd);
        window.IsActive = isActive;
        window.Agent = status is null ? null : Badge(status.Value);
        return window;
    }

    private static PreviewViewModel AtNoon(out Func<DateTime, DateTime> setNow)
    {
        var now = Noon;
        var vm = new PreviewViewModel { UtcNow = () => now };
        setNow = value => now = value;
        return vm;
    }

    [Fact]
    public void MergeResults_StatusChange_RestartsTheStatusTime()
    {
        using var vm = AtNoon(out var setNow);
        vm.MergeResults([WithAgent(1, AgentStatus.Busy)]);

        setNow(Noon.AddMinutes(7));
        vm.MergeResults([WithAgent(1, AgentStatus.Busy)]);
        Assert.Equal(Noon, vm.Windows.Single().StatusSince);
        Assert.Equal("7 min", vm.Windows.Single().StatusAge);

        setNow(Noon.AddMinutes(9));
        vm.MergeResults([WithAgent(1, AgentStatus.Idle)]);
        Assert.Equal(Noon.AddMinutes(9), vm.Windows.Single().StatusSince);
        Assert.Equal("< 1 min", vm.Windows.Single().StatusAge);
    }

    [Fact]
    public void MergeResults_FinishedTurnSeen_KeepsTheStatusTime()
    {
        // Looking at the window clears the orange border, not the time since the agent stopped
        using var vm = AtNoon(out var setNow);
        vm.MergeResults([WithAgent(1, AgentStatus.Busy)]);
        setNow(Noon.AddMinutes(1));
        vm.MergeResults([WithAgent(1, AgentStatus.Idle)]);

        setNow(Noon.AddMinutes(5));
        vm.MergeResults([WithAgent(1, AgentStatus.Idle, isActive: true)]);

        Assert.Equal(Noon.AddMinutes(1), vm.Windows.Single().StatusSince);
    }

    [Fact]
    public void StatusAge_QuietWindowOrOptionOff_IsEmpty()
    {
        using var vm = AtNoon(out _);
        vm.MergeResults([WithAgent(1, null), WithAgent(2, AgentStatus.Busy)]);
        Assert.Equal("", vm.Windows[0].StatusAge);
        Assert.Equal("< 1 min", vm.Windows[1].StatusAge);

        vm.ShowStatusTime = false;

        Assert.Equal("", vm.Windows[1].StatusAge);
    }

    [Fact]
    public void MergeResults_AgentWaitingPastTheDelay_BlinksAndRemindsOnce()
    {
        using var vm = AtNoon(out var setNow);
        vm.RemindAfter = TimeSpan.FromMinutes(10);
        var raised = new List<AgentTransition>();
        vm.AgentNeedsAttention += (_, transition) => raised.Add(transition);
        vm.MergeResults([WithAgent(1, AgentStatus.Waiting)]);

        setNow(Noon.AddMinutes(9));
        vm.MergeResults([WithAgent(1, AgentStatus.Waiting)]);
        Assert.False(vm.Windows.Single().IsEscalated);

        setNow(Noon.AddMinutes(10));
        vm.MergeResults([WithAgent(1, AgentStatus.Waiting)]);
        setNow(Noon.AddMinutes(15));
        vm.MergeResults([WithAgent(1, AgentStatus.Waiting)]);

        Assert.True(vm.Windows.Single().IsEscalated);
        Assert.Equal(new[] { AgentTransition.Reminder }, raised);
    }

    [Fact]
    public void MergeResults_AgentWaitingInFront_NeitherBlinksNorReminds()
    {
        using var vm = AtNoon(out var setNow);
        var raised = new List<AgentTransition>();
        vm.AgentNeedsAttention += (_, transition) => raised.Add(transition);
        vm.MergeResults([WithAgent(1, AgentStatus.Waiting, isActive: true)]);

        setNow(Noon.AddMinutes(30));
        vm.MergeResults([WithAgent(1, AgentStatus.Waiting, isActive: true)]);

        Assert.False(vm.Windows.Single().IsEscalated);
        Assert.Empty(raised);
    }

    [Fact]
    public void MergeResults_RemindersOff_NeverBlink()
    {
        using var vm = AtNoon(out var setNow);
        vm.RemindAfter = TimeSpan.Zero;
        vm.MergeResults([WithAgent(1, AgentStatus.Waiting)]);

        setNow(Noon.AddHours(2));
        vm.MergeResults([WithAgent(1, AgentStatus.Waiting)]);

        Assert.False(vm.Windows.Single().IsEscalated);
    }

    [Fact]
    public void MergeResults_CountsTheAgentsThatNeedTheUser()
    {
        using var vm = AtNoon(out _);
        vm.MergeResults([WithAgent(1, AgentStatus.Busy), WithAgent(2, AgentStatus.Waiting), WithAgent(3, AgentStatus.Waiting)]);
        vm.MergeResults([WithAgent(1, AgentStatus.Idle), WithAgent(2, AgentStatus.Waiting), WithAgent(3, AgentStatus.Waiting, isActive: true)]);

        Assert.Equal(1, vm.AgentsWaiting); // the one in front is being answered
        Assert.Equal(1, vm.AgentsFinished);
    }

    [Fact]
    public void SortByUrgency_ShowsOneListMostUrgentFirst()
    {
        using var vm = AtNoon(out var setNow);
        vm.MergeResults([WithAgent(1, AgentStatus.Busy), WithAgent(2, null)]);
        setNow(Noon.AddMinutes(1));
        vm.MergeResults([WithAgent(1, AgentStatus.Busy), WithAgent(2, null), WithAgent(3, AgentStatus.Waiting)]);

        vm.SortByUrgency = true;

        Assert.Empty(vm.GroupedWindows.GroupDescriptions);
        Assert.Equal(new[] { (IntPtr)3, (IntPtr)1, (IntPtr)2 }, vm.GroupedWindows.Cast<TrackedWindow>().Select(w => w.Hwnd));
    }

    [Fact]
    public void ApplyArrangement_ReordersAtMostEveryIntervalAndNeverUnderTheMouse()
    {
        using var vm = AtNoon(out var setNow);
        vm.MergeResults([WithAgent(1, AgentStatus.Busy), WithAgent(2, AgentStatus.Busy)]);
        vm.SortByUrgency = true; // arranged at noon
        IntPtr[] Shown() => vm.GroupedWindows.Cast<TrackedWindow>().Select(w => w.Hwnd).ToArray();

        // Window 2 starts waiting: it should come first, but not yet
        setNow(Noon.AddSeconds(1));
        vm.MergeResults([WithAgent(1, AgentStatus.Busy), WithAgent(2, AgentStatus.Waiting)]);
        vm.ApplyArrangement();
        Assert.Equal(new[] { (IntPtr)1, (IntPtr)2 }, Shown());

        setNow(Noon + PreviewViewModel.ReorderInterval);
        vm.IsPointerOver = true;
        vm.ApplyArrangement();
        Assert.Equal(new[] { (IntPtr)1, (IntPtr)2 }, Shown());

        vm.IsPointerOver = false; // leaving the panel applies the pending order
        Assert.Equal(new[] { (IntPtr)2, (IntPtr)1 }, Shown());
    }

    [Fact]
    public void ShowOnlyWindowsThatNeedMe_HidesWorkingAgentsAndQuietWindows()
    {
        using var vm = AtNoon(out _);
        vm.MergeResults([WithAgent(1, AgentStatus.Busy), WithAgent(2, null), WithAgent(3, AgentStatus.Idle)]);

        vm.ShowOnlyWindowsThatNeedMe = true;

        Assert.Equal(new[] { (IntPtr)3 }, vm.GroupedWindows.Cast<TrackedWindow>().Select(w => w.Hwnd));
        Assert.False(vm.IsEmptyAfterFilter);
    }

    [Fact]
    public void ShowOnlyWindowsThatNeedMe_NothingNeedsTheUser_SaysSo()
    {
        using var vm = AtNoon(out _);
        vm.MergeResults([WithAgent(1, AgentStatus.Busy)]);

        vm.ShowOnlyWindowsThatNeedMe = true;

        Assert.True(vm.IsEmptyAfterFilter);
        vm.ShowOnlyWindowsThatNeedMe = false;
        Assert.False(vm.IsEmptyAfterFilter);
    }

    [Fact]
    public void TitleFormat_AppliesToTheWindowsAtOnceAndAfterEachScan()
    {
        using var vm = new PreviewViewModel();
        vm.MergeResults([Scanned(1, "Readme.md - WinGlance - Visual Studio Code")]);
        Assert.Equal("Readme.md - WinGlance - Visual Studio Code", vm.Windows.Single().DisplayText);

        vm.TitleFormat = "{project} · {document}";
        Assert.Equal("WinGlance · Readme.md", vm.Windows.Single().DisplayText);

        vm.MergeResults([Scanned(1, "Notes.md - WinGlance - Visual Studio Code")]);
        Assert.Equal("WinGlance · Notes.md", vm.Windows.Single().DisplayText);
    }
}
