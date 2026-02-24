namespace WinGlance.Models;

/// <summary>
/// Root configuration model serialized to/from <c>config.json</c>.
/// All properties have sensible defaults so that a missing or corrupt file
/// produces a working configuration.
/// </summary>
internal sealed class AppConfig
{
    public List<MonitoredAppConfig> MonitoredApps { get; set; } = [];
    public string Layout { get; set; } = "horizontal";
    public int ThumbnailWidth { get; set; } = 200;
    public int ThumbnailHeight { get; set; } = 150;
    public double PanelOpacity { get; set; } = 0.85;
    public int PollingIntervalMs { get; set; } = 1000;
    public bool RememberPosition { get; set; } = true;
    public double PanelX { get; set; } = 100;
    public double PanelY { get; set; } = 100;
    public bool AutoStart { get; set; }
    public bool CloseToTray { get; set; } = true;
    public string Hotkey { get; set; } = "Ctrl+Alt+G";
    public LlmConfig Llm { get; set; } = new();
}

/// <summary>
/// Lightweight DTO for a monitored app entry in <c>config.json</c>.
/// Kept separate from <see cref="MonitoredApp"/> to avoid serialization
/// of equality/hash logic.
/// </summary>
internal sealed class MonitoredAppConfig
{
    public string ProcessName { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

/// <summary>
/// LLM analysis configuration (FR-10).
/// </summary>
internal sealed class LlmConfig
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "openai";
    public string Endpoint { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-4o-mini";
    public int StaleThresholdSeconds { get; set; } = 30;
}
