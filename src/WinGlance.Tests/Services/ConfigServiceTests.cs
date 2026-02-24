using System.IO;
using WinGlance.Models;
using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class ConfigServiceTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"WinGlance_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* cleanup best-effort */ }
    }

    [Fact]
    public void Load_NonexistentFile_ReturnsDefaults()
    {
        var svc = new ConfigService(_tempDir);

        var config = svc.Load();

        Assert.NotNull(config);
        Assert.Equal("horizontal", config.Layout);
        Assert.Equal(200, config.ThumbnailWidth);
        Assert.Empty(config.MonitoredApps);
    }

    [Fact]
    public void SaveThenLoad_RoundTrip_PreservesValues()
    {
        var svc = new ConfigService(_tempDir);
        var original = new AppConfig
        {
            Layout = "grid",
            ThumbnailWidth = 350,
            PanelOpacity = 0.7,
            MonitoredApps =
            [
                new MonitoredAppConfig { ProcessName = "notepad", DisplayName = "Notepad" },
            ],
        };

        svc.Save(original);
        var loaded = svc.Load();

        Assert.Equal("grid", loaded.Layout);
        Assert.Equal(350, loaded.ThumbnailWidth);
        Assert.Equal(0.7, loaded.PanelOpacity);
        Assert.Single(loaded.MonitoredApps);
        Assert.Equal("notepad", loaded.MonitoredApps[0].ProcessName);
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaults()
    {
        var svc = new ConfigService(_tempDir);
        File.WriteAllText(svc.ConfigPath, "{ this is not valid json !!!");

        var config = svc.Load();

        Assert.NotNull(config);
        Assert.Equal("horizontal", config.Layout); // default
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaults()
    {
        var svc = new ConfigService(_tempDir);
        File.WriteAllText(svc.ConfigPath, "");

        var config = svc.Load();

        Assert.NotNull(config);
        Assert.Equal("horizontal", config.Layout);
    }

    [Fact]
    public void GetConfigPath_ReturnsValidPath()
    {
        var svc = new ConfigService(_tempDir);

        Assert.False(string.IsNullOrWhiteSpace(svc.ConfigPath));
        Assert.EndsWith("config.json", svc.ConfigPath);
    }

    [Fact]
    public void Save_CreatesFile()
    {
        var svc = new ConfigService(_tempDir);

        svc.Save(new AppConfig());

        Assert.True(File.Exists(svc.ConfigPath));
    }

    [Fact]
    public void Save_NoTempFileLeftBehind()
    {
        var svc = new ConfigService(_tempDir);

        svc.Save(new AppConfig());

        Assert.False(File.Exists(svc.ConfigPath + ".tmp"));
    }

    [Fact]
    public void Save_OverwritesExistingConfig()
    {
        var svc = new ConfigService(_tempDir);
        svc.Save(new AppConfig { Layout = "horizontal" });
        svc.Save(new AppConfig { Layout = "vertical" });

        var loaded = svc.Load();

        Assert.Equal("vertical", loaded.Layout);
    }

    [Fact]
    public void Load_PartialJson_MergesWithDefaults()
    {
        var svc = new ConfigService(_tempDir);
        // Write JSON with only one property — the rest should take defaults
        File.WriteAllText(svc.ConfigPath, """{"layout":"grid"}""");

        var config = svc.Load();

        Assert.Equal("grid", config.Layout);
        Assert.Equal(200, config.ThumbnailWidth); // default
        Assert.Equal(150, config.ThumbnailHeight); // default
    }
}
