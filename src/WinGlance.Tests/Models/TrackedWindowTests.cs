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

    [Fact]
    public void Title_DoesNotRaise_WhenSameValue()
    {
        var window = new TrackedWindow(IntPtr.Zero, "test", "Test") { Title = "Hello" };
        var raised = false;
        window.PropertyChanged += (_, _) => raised = true;

        window.Title = "Hello"; // same value

        Assert.False(raised);
    }

    [Fact]
    public void Icon_DefaultsToNull()
    {
        var window = new TrackedWindow(IntPtr.Zero, "test", "Test");
        Assert.Null(window.Icon);
    }

    [Fact]
    public void Equality_SameHwndDifferentProperties_AreEqual()
    {
        var a = new TrackedWindow((IntPtr)100, "notepad", "Notepad") { Title = "File A" };
        var b = new TrackedWindow((IntPtr)100, "notepad", "Notepad") { Title = "File B" };

        Assert.Equal(a, b); // equality based on Hwnd only
    }

    [Fact]
    public void Equality_DifferentHwndSameProperties_AreNotEqual()
    {
        var a = new TrackedWindow((IntPtr)100, "notepad", "Notepad") { Title = "Same" };
        var b = new TrackedWindow((IntPtr)200, "notepad", "Notepad") { Title = "Same" };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_ViaObjectEquals()
    {
        var a = new TrackedWindow((IntPtr)100, "notepad", "Notepad");
        object b = new TrackedWindow((IntPtr)100, "notepad", "Notepad");

        Assert.True(a.Equals(b));
    }

    [Fact]
    public void LlmVerdict_DefaultsToNull()
    {
        var window = new TrackedWindow(IntPtr.Zero, "test", "Test");
        Assert.Null(window.LlmVerdict);
    }

    [Fact]
    public void AllBooleanFlags_DefaultToFalse()
    {
        var window = new TrackedWindow(IntPtr.Zero, "test", "Test");

        Assert.False(window.IsActive);
        Assert.False(window.IsFlashing);
        Assert.False(window.IsHung);
        Assert.False(window.IsModalBlocked);
        Assert.False(window.IsStale);
    }
}
