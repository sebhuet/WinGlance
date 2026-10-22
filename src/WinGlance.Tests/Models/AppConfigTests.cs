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
        Assert.NotNull(config.AgentDetection);
    }

    // ── API key at rest ──────────────────────────────────────────────────

    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public void Json_StoresTheApiKeyEncrypted_NeverInClear()
    {
        var config = new AppConfig { Llm = { ApiKey = "sk-live-123" } };

        var json = JsonSerializer.Serialize(config, CamelCase);

        Assert.DoesNotContain("sk-live-123", json);
        Assert.DoesNotContain("\"apiKey\"", json);
        Assert.Contains("\"apiKeyProtected\"", json);
        Assert.Equal("sk-live-123", JsonSerializer.Deserialize<AppConfig>(json, CamelCase)!.Llm.ApiKey);
    }

    [Fact]
    public void Json_PlaintextKeyFromAnOlderConfig_IsStillRead()
    {
        var config = JsonSerializer.Deserialize<AppConfig>("""{"llm":{"apiKey":"sk-old"}}""", CamelCase)!;

        Assert.Equal("sk-old", config.Llm.ApiKey);
    }

    [Fact]
    public void Json_EmptyProtectedKey_DoesNotEraseAPlaintextKey()
    {
        var config = JsonSerializer.Deserialize<AppConfig>(
            """{"llm":{"apiKey":"sk-typed-by-hand","apiKeyProtected":""}}""", CamelCase)!;

        Assert.Equal("sk-typed-by-hand", config.Llm.ApiKey);
    }

    [Fact]
    public void Json_KeyEncryptedElsewhere_ReadsAsEmpty()
    {
        // config.json copied with the portable exe to another machine or user
        var config = JsonSerializer.Deserialize<AppConfig>("""{"llm":{"apiKeyProtected":"AQIDBA=="}}""", CamelCase)!;

        Assert.Equal("", config.Llm.ApiKey);
    }

    [Fact]
    public void ProviderDefaults_CoverEveryProvider()
    {
        Assert.Equal(["google", "ollama", "openai"], LlmConfig.ProviderDefaults.Keys.Order());
    }

    [Fact]
    public void AgentDetectionConfig_Defaults_AreCorrect()
    {
        var agents = new AgentDetectionConfig();

        Assert.True(agents.Enabled);
        Assert.Equal(5000, agents.RefreshIntervalMs);
    }

    [Fact]
    public void JsonRoundTrip_PreservesAgentDetection()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var original = new AppConfig
        {
            AgentDetection = new AgentDetectionConfig { Enabled = false, RefreshIntervalMs = 8000 },
        };

        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<AppConfig>(json, options)!;

        Assert.Contains("\"agentDetection\"", json);
        Assert.False(restored.AgentDetection.Enabled);
        Assert.Equal(8000, restored.AgentDetection.RefreshIntervalMs);
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
