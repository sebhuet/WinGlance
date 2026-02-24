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
    public void Layout_DefaultsToHorizontal()
    {
        using var vm = new PreviewViewModel();
        Assert.Equal("horizontal", vm.Layout);
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

        vm.Layout = "horizontal"; // same as default

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
}
