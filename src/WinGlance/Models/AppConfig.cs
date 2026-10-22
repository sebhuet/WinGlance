namespace WinGlance.Models;

/// <summary>
/// Root configuration model serialized to/from <c>config.json</c>.
/// All properties have sensible defaults so that a missing or corrupt file
/// produces a working configuration.
/// </summary>
internal sealed class AppConfig
{
    public List<MonitoredAppConfig> MonitoredApps { get; set; } = [];
    public string Layout { get; set; } = "fit";
    public int ThumbnailWidth { get; set; } = 200;
    public int ThumbnailHeight { get; set; } = 150;
    public double PanelOpacity { get; set; } = 0.85;
    public int PollingIntervalMs { get; set; } = 1000;
    public bool RememberPosition { get; set; } = true;
    public double PanelX { get; set; } = 100;
    public double PanelY { get; set; } = 100;

    /// <summary>Panel size in the "fit" layout, where the user sizes the panel freely.</summary>
    public double PanelWidth { get; set; } = 900;

    /// <summary>Panel height in the "fit" layout.</summary>
    public double PanelHeight { get; set; } = 560;
    public bool AutoStart { get; set; }
    public bool CloseToTray { get; set; } = true;

    /// <summary>Outlines a window with a blinking red frame after switching to it from a thumbnail.</summary>
    public bool HighlightOnSwitch { get; set; } = true;

    /// <summary>
    /// Plays a short click after switching to a window from a thumbnail, on the left, in the center or on the
    /// right depending on the monitor the window is on.
    /// </summary>
    public bool ClickOnSwitch { get; set; } = true;
    public string Hotkey { get; set; } = "Ctrl+Alt+G";
    public LlmConfig Llm { get; set; } = new();
    public AgentDetectionConfig AgentDetection { get; set; } = new();
}

/// <summary>
/// Coding agent detection configuration (FR-11).
/// </summary>
internal sealed class AgentDetectionConfig
{
    /// <summary>Shows Claude Code / OpenCode status on thumbnails.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Delay between two agent discoveries. Each one runs <c>claude agents --json</c>
    /// (~0.2 s of CPU), hence a slower cadence than window polling.
    /// </summary>
    public int RefreshIntervalMs { get; set; } = 5000;
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
    /// <summary>
    /// Default endpoint and model of each provider, applied when the provider changes.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, (string Endpoint, string Model)> ProviderDefaults =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["openai"] = ("https://api.openai.com/v1", "gpt-4o-mini"),
            ["google"] = ("https://generativelanguage.googleapis.com/v1beta", "gemini-2.0-flash"),
            ["ollama"] = ("http://localhost:11434", "llava"),
        };

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "openai";
    public string Endpoint { get; set; } = "https://api.openai.com/v1";

    /// <summary>
    /// The API key, in clear in memory only: config.json stores <see cref="ApiKeyProtected"/>.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// The API key encrypted with DPAPI for the current Windows user, as stored in config.json.
    /// A key that cannot be decrypted (file copied to another machine or user) reads as empty
    /// and must be entered again.
    /// </summary>
    public string ApiKeyProtected
    {
        get => Services.SecretProtector.Protect(ApiKey);
        set
        {
            // Empty means "no encrypted key": keep a plaintext apiKey read from the same file
            if (string.IsNullOrEmpty(value))
                return;

            var key = Services.SecretProtector.Unprotect(value);
            if (key is null)
                Services.Log.Warning("The saved LLM API key could not be decrypted (config.json from another user or machine); enter it again in Settings");
            ApiKey = key ?? "";
        }
    }

    /// <summary>
    /// Plaintext key of configurations written before encryption (or typed in by hand):
    /// read once, encrypted at the next save, never written back.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("apiKey")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyApiKey
    {
        get => null;
        set
        {
            if (!string.IsNullOrEmpty(value))
                ApiKey = value;
        }
    }

    public string Model { get; set; } = "gpt-4o-mini";
    public int StaleThresholdSeconds { get; set; } = 30;
    public string Prompt { get; set; } = DefaultPrompt;
    public bool DebugEnabled { get; set; }

    internal const string DefaultPrompt = """
        You are a desktop window analyst for WinGlance. You receive a screenshot of an application window along with its title. Your job is to determine whether the window requires the user's attention or is idle.

        ## Classification rules

        Respond with **awaiting_action** if ANY of the following is visible:
        - A modal dialog, confirmation prompt, or message box (OK / Cancel / Yes / No buttons)
        - An error message, warning banner, or crash report
        - A form waiting for user input (login screen, unsaved changes prompt, wizard step)
        - A file download or transfer that has completed and shows a "done" or "open" button
        - A progress bar that has reached 100% and is waiting for acknowledgement
        - An installer or updater waiting for the user to click "Next", "Install", or "Restart"
        - A notification toast or permission request inside the window
        - A debugger break or breakpoint hit (e.g. Visual Studio paused on exception)
        - A terminal/console waiting for user input (prompt with blinking cursor)
        - A "Save As" or "Open File" dialog

        Respond with **idle** if the window shows:
        - Normal application content with no pending interaction (e.g. a code editor with files open, a browser showing a web page, a document being viewed)
        - A long-running operation still in progress (progress bar moving, spinner active, build running)
        - A minimized or blank window
        - Static content with no actionable UI elements

        ## Response format

        Reply with EXACTLY one word on the first line — no quotes, no explanation:
        awaiting_action
        OR
        idle

        You may optionally add a brief one-line reason on the second line (this will be logged but not shown to the user).
        """;
}
