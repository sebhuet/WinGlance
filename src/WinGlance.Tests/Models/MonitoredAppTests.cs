using WinGlance.Models;

namespace WinGlance.Tests.Models;

public class MonitoredAppTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var app = new MonitoredApp("Code", "VS Code");

        Assert.Equal("Code", app.ProcessName);
        Assert.Equal("VS Code", app.DisplayName);
    }

    [Fact]
    public void Equality_CaseInsensitiveOnProcessName()
    {
        var a = new MonitoredApp("Code", "VS Code");
        var b = new MonitoredApp("code", "VS Code");
        var c = new MonitoredApp("notepad", "Notepad");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_NullReturnsFalse()
    {
        var app = new MonitoredApp("Code", "VS Code");
        Assert.False(app.Equals((MonitoredApp?)null));
        Assert.False(app.Equals((object?)null));
    }

    [Fact]
    public void ToString_ReturnsDisplayNameAndProcessName()
    {
        var app = new MonitoredApp("Code", "VS Code");
        Assert.Equal("VS Code (Code)", app.ToString());
    }
}
