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
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // cleanup best-effort
        }
    }

    [Fact]
    public void Load_NonexistentFile_ReturnsDefaults()
    {
        var svc = new ConfigService(_tempDir);

        var config = svc.Load();

        Assert.NotNull(config);
        Assert.Equal("fit", config.Layout);
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
        Assert.Equal("fit", config.Layout); // default
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaults()
    {
        var svc = new ConfigService(_tempDir);
        File.WriteAllText(svc.ConfigPath, "");

        var config = svc.Load();

        Assert.NotNull(config);
        Assert.Equal("fit", config.Layout);
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

    // ── Hand-edited files ────────────────────────────────────────────────

    [Fact]
    public void Load_NullSections_AreReplacedByDefaults()
    {
        // These used to crash MainWindow construction and leave a zombie process
        var svc = new ConfigService(_tempDir);
        File.WriteAllText(svc.ConfigPath, """{"monitoredApps":null,"llm":null,"agentDetection":null,"hotkey":null,"layout":null}""");

        var config = svc.Load();

        Assert.Empty(config.MonitoredApps);
        Assert.NotNull(config.Llm);
        Assert.NotNull(config.AgentDetection);
        Assert.Equal("Ctrl+Alt+G", config.Hotkey);
        Assert.Equal("fit", config.Layout);
    }

    [Fact]
    public void Load_OutOfRangeValues_AreClamped()
    {
        var svc = new ConfigService(_tempDir);
        File.WriteAllText(svc.ConfigPath, """
            {"panelOpacity":0,"thumbnailWidth":5000,"thumbnailHeight":1,"pollingIntervalMs":10,
             "llm":{"staleThresholdSeconds":0},"agentDetection":{"refreshIntervalMs":1}}
            """);

        var config = svc.Load();

        Assert.Equal(0.3, config.PanelOpacity); // 0 made the panel invisible
        Assert.Equal(500, config.ThumbnailWidth);
        Assert.Equal(75, config.ThumbnailHeight);
        Assert.Equal(500, config.PollingIntervalMs);
        Assert.Equal(10, config.Llm.StaleThresholdSeconds);
        Assert.Equal(1000, config.AgentDetection.RefreshIntervalMs);
    }

    [Fact]
    public void Load_InvalidMonitoredApps_AreDroppedOrCompleted()
    {
        var svc = new ConfigService(_tempDir);
        File.WriteAllText(svc.ConfigPath, """
            {"monitoredApps":[null,{"processName":"  "},{"processName":"Code","displayName":""}]}
            """);

        var app = Assert.Single(svc.Load().MonitoredApps);

        Assert.Equal(("Code", "Code"), (app.ProcessName, app.DisplayName));
    }

    [Theory]
    [InlineData("""{"nextAgentHotkey":""}""", "")] // explicitly none
    [InlineData("""{"nextAgentHotkey":null}""", "Ctrl+Alt+N")]
    [InlineData("""{}""", "Ctrl+Alt+N")]
    public void Load_NextAgentHotkey_EmptyMeansNone_MissingMeansDefault(string json, string expected)
    {
        var svc = new ConfigService(_tempDir);
        File.WriteAllText(svc.ConfigPath, json);

        Assert.Equal(expected, svc.Load().NextAgentHotkey);
    }

    [Fact]
    public void Load_UnknownLayout_FallsBackToTheDefault()
    {
        var svc = new ConfigService(_tempDir);
        File.WriteAllText(svc.ConfigPath, """{"layout":"Diagonal"}""");

        Assert.Equal("fit", svc.Load().Layout);
    }

    // ── Config location ──────────────────────────────────────────────────

    [Fact]
    public void ResolveConfigPath_WritableExeFolder_IsUsed()
    {
        var path = ConfigService.ResolveConfigPath(@"C:\Tools", _tempDir, _ => true);

        Assert.Equal(@"C:\Tools\config.json", path);
    }

    [Fact]
    public void ResolveConfigPath_ReadOnlyExeFolder_FallsBackToLocalAppData()
    {
        var path = ConfigService.ResolveConfigPath(@"C:\Program Files\WinGlance", _tempDir, _ => false);

        Assert.Equal(Path.Combine(_tempDir, "WinGlance", "config.json"), path);
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "WinGlance")));
    }

    [Fact]
    public void IsDirectoryWritable_TempFolder_IsTrue_AndLeavesNoFile()
    {
        Assert.True(ConfigService.IsDirectoryWritable(_tempDir));
        Assert.Empty(Directory.GetFiles(_tempDir));
    }

    [Fact]
    public void IsDirectoryWritable_MissingFolder_IsFalse()
    {
        Assert.False(ConfigService.IsDirectoryWritable(Path.Combine(_tempDir, "missing")));
    }

    [Fact]
    public void Load_ConfigWrittenBeforeAgentDetection_EnablesItByDefault()
    {
        var svc = new ConfigService(_tempDir);
        File.WriteAllText(svc.ConfigPath, """{"layout":"grid","llm":{"enabled":false}}""");

        var config = svc.Load();

        Assert.True(config.AgentDetection.Enabled);
        Assert.Equal(5000, config.AgentDetection.RefreshIntervalMs);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(10, 10)]
    [InlineData(500, 60)]
    public void Normalize_RemindAfterMinutes_IsClampedTo0To60(int value, int expected)
    {
        var config = new AppConfig { AgentDetection = { RemindAfterMinutes = value } };

        Assert.Equal(expected, ConfigService.Normalize(config).AgentDetection.RemindAfterMinutes);
    }

    [Fact]
    public void Normalize_BlankTitleFormat_FallsBackToTheTitle()
    {
        var config = new AppConfig { TitleFormat = "  " };

        Assert.Equal("{project} - {document}", ConfigService.Normalize(config).TitleFormat);
    }
}
