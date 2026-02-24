using System.Text.Json;
using WinGlance.Models;

namespace WinGlance.Tests.Models;

public class AppConfigTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var config = new AppConfig();

        Assert.Empty(config.MonitoredApps);
        Assert.Equal("horizontal", config.Layout);
        Assert.Equal(200, config.ThumbnailWidth);
        Assert.Equal(150, config.ThumbnailHeight);
        Assert.Equal(0.85, config.PanelOpacity);
        Assert.Equal(1000, config.PollingIntervalMs);
        Assert.True(config.RememberPosition);
        Assert.Equal(100, config.PanelX);
        Assert.Equal(100, config.PanelY);
        Assert.False(config.AutoStart);
        Assert.True(config.CloseToTray);
        Assert.Equal("Ctrl+Alt+G", config.Hotkey);
        Assert.NotNull(config.Llm);
    }

    [Fact]
    public void LlmConfig_Defaults_AreCorrect()
    {
        var llm = new LlmConfig();

        Assert.False(llm.Enabled);
        Assert.Equal("openai", llm.Provider);
        Assert.Equal("https://api.openai.com/v1", llm.Endpoint);
        Assert.Equal("", llm.ApiKey);
        Assert.Equal("gpt-4o-mini", llm.Model);
        Assert.Equal(30, llm.StaleThresholdSeconds);
    }

    [Fact]
    public void MonitoredAppConfig_Defaults_AreCorrect()
    {
        var mac = new MonitoredAppConfig();

        Assert.Equal("", mac.ProcessName);
        Assert.Equal("", mac.DisplayName);
    }

    [Fact]
    public void JsonRoundTrip_PreservesValues()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        var original = new AppConfig
        {
            Layout = "grid",
            ThumbnailWidth = 400,
            ThumbnailHeight = 300,
            PanelOpacity = 0.5,
            PollingIntervalMs = 2000,
            RememberPosition = false,
            PanelX = 50,
            PanelY = 75,
            AutoStart = true,
            CloseToTray = false,
            Hotkey = "Ctrl+Shift+W",
            MonitoredApps =
            [
                new MonitoredAppConfig { ProcessName = "notepad", DisplayName = "Notepad" },
                new MonitoredAppConfig { ProcessName = "code", DisplayName = "VS Code" },
            ],
            Llm = new LlmConfig
            {
                Enabled = true,
                Provider = "anthropic",
                Endpoint = "https://api.anthropic.com",
                ApiKey = "sk-test",
                Model = "claude-sonnet",
                StaleThresholdSeconds = 60,
            },
        };

        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<AppConfig>(json, options)!;

        Assert.Equal(original.Layout, restored.Layout);
        Assert.Equal(original.ThumbnailWidth, restored.ThumbnailWidth);
        Assert.Equal(original.ThumbnailHeight, restored.ThumbnailHeight);
        Assert.Equal(original.PanelOpacity, restored.PanelOpacity);
        Assert.Equal(original.PollingIntervalMs, restored.PollingIntervalMs);
        Assert.Equal(original.RememberPosition, restored.RememberPosition);
        Assert.Equal(original.PanelX, restored.PanelX);
        Assert.Equal(original.PanelY, restored.PanelY);
        Assert.Equal(original.AutoStart, restored.AutoStart);
        Assert.Equal(original.CloseToTray, restored.CloseToTray);
        Assert.Equal(original.Hotkey, restored.Hotkey);
        Assert.Equal(2, restored.MonitoredApps.Count);
        Assert.Equal("notepad", restored.MonitoredApps[0].ProcessName);
        Assert.Equal("Notepad", restored.MonitoredApps[0].DisplayName);
        Assert.Equal("code", restored.MonitoredApps[1].ProcessName);
        Assert.True(restored.Llm.Enabled);
        Assert.Equal("anthropic", restored.Llm.Provider);
        Assert.Equal("sk-test", restored.Llm.ApiKey);
        Assert.Equal(60, restored.Llm.StaleThresholdSeconds);
    }

    [Fact]
    public void MonitoredAppConfig_Properties_AreSettable()
    {
        var mac = new MonitoredAppConfig
        {
            ProcessName = "explorer",
            DisplayName = "File Explorer",
        };

        Assert.Equal("explorer", mac.ProcessName);
        Assert.Equal("File Explorer", mac.DisplayName);
    }
}
