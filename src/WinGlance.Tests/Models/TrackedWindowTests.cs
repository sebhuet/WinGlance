using WinGlance.Models;

namespace WinGlance.Tests.Models;

public class TrackedWindowTests
{
    [Fact]
    public void Constructor_SetsImmutableProperties()
    {
        var hwnd = (IntPtr)12345;
        var window = new TrackedWindow(hwnd, "notepad", "Notepad");

        Assert.Equal(hwnd, window.Hwnd);
        Assert.Equal("notepad", window.ProcessName);
        Assert.Equal("Notepad", window.DisplayName);
    }

    [Fact]
    public void Title_DefaultsToEmpty()
    {
        var window = new TrackedWindow(IntPtr.Zero, "test", "Test");
        Assert.Equal(string.Empty, window.Title);
    }

    [Fact]
    public void Title_RaisesPropertyChanged()
    {
        var window = new TrackedWindow(IntPtr.Zero, "test", "Test");
        var raised = false;
        window.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TrackedWindow.Title))
                raised = true;
        };

        window.Title = "New Title";
        Assert.True(raised);
        Assert.Equal("New Title", window.Title);
    }

    [Fact]
    public void IsActive_RaisesPropertyChanged()
    {
        var window = new TrackedWindow(IntPtr.Zero, "test", "Test");
        var raised = false;
        window.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TrackedWindow.IsActive))
                raised = true;
        };

        window.IsActive = true;
        Assert.True(raised);
    }

    [Fact]
    public void AttentionProperties_RaisePropertyChanged()
    {
        var window = new TrackedWindow(IntPtr.Zero, "test", "Test");
        var changedProperties = new List<string>();
        window.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        window.IsFlashing = true;
        window.IsHung = true;
        window.IsModalBlocked = true;
        window.IsStale = true;
        window.LlmVerdict = "awaiting_action";

        Assert.Contains(nameof(TrackedWindow.IsFlashing), changedProperties);
        Assert.Contains(nameof(TrackedWindow.IsHung), changedProperties);
        Assert.Contains(nameof(TrackedWindow.IsModalBlocked), changedProperties);
        Assert.Contains(nameof(TrackedWindow.IsStale), changedProperties);
        Assert.Contains(nameof(TrackedWindow.LlmVerdict), changedProperties);
    }

    [Fact]
    public void Equality_BasedOnHwnd()
    {
        var a = new TrackedWindow((IntPtr)100, "notepad", "Notepad");
        var b = new TrackedWindow((IntPtr)100, "notepad", "Notepad");
        var c = new TrackedWindow((IntPtr)200, "notepad", "Notepad");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_NullReturnsFalse()
    {
        var window = new TrackedWindow((IntPtr)1, "test", "Test");
        Assert.False(window.Equals((TrackedWindow?)null));
        Assert.False(window.Equals((object?)null));
    }
}
