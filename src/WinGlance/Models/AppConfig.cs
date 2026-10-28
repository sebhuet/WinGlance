namespace WinGlance.Models;

/// <summary>
/// Root configuration model serialized to/from <c>config.json</c>.
/// All properties have sensible defaults so that a missing or corrupt file
/// produces a working configuration.
/// </summary>
internal sealed class AppConfig
{
    /// <summary>Applications whose windows get a thumbnail in the panel.</summary>
    public List<MonitoredAppConfig> MonitoredApps { get; set; } = [];

    /// <summary>Preview layout: "fit" (default), "horizontal", "vertical" or "grid"; any other value falls back to the default.</summary>
    public string Layout { get; set; } = "fit";

    /// <summary>
    /// Shows the windows in one list ordered by urgency (the one that has needed the user longest
    /// at the top left) instead of grouped by application (default off).
    /// </summary>
    public bool SortByUrgency { get; set; }

    /// <summary>Hides the windows that need nothing: agent working, or no agent and no attention state (default off).</summary>
    public bool ShowOnlyWindowsThatNeedMe { get; set; }

    /// <summary>Shows under each thumbnail the time since the window's status last changed (default on).</summary>
    public bool ShowStatusTime { get; set; } = true;

    /// <summary>
    /// Template of the text under each thumbnail: {title}, {project}, {document} and {app} are
    /// replaced (default "{project} - {document}": the project first, readable when the text is cut);
    /// the full title stays in the tooltip.
    /// </summary>
    public string TitleFormat { get; set; } = Services.WindowTitleFormat.Default;

    /// <summary>Thumbnail width in pixels, 100-500 (default 200); not used by the "fit" layout.</summary>
    public int ThumbnailWidth { get; set; } = 200;

    /// <summary>Thumbnail height in pixels, 75-400 (default 150); not used by the "fit" layout.</summary>
    public int ThumbnailHeight { get; set; } = 150;

    /// <summary>Panel opacity, 0.3 (very transparent) to 1.0 (opaque); default 0.85.</summary>
    public double PanelOpacity { get; set; } = 0.85;

    /// <summary>Delay between two scans of the windows, in milliseconds, 500-5000 (default 1000).</summary>
    public int PollingIntervalMs { get; set; } = 1000;

    /// <summary>Reopens the panel at <see cref="PanelX"/>, <see cref="PanelY"/> (default on).</summary>
    public bool RememberPosition { get; set; } = true;

    /// <summary>Left edge of the panel in device-independent pixels, saved when the panel is hidden or on exit.</summary>
    public double PanelX { get; set; } = 100;

    /// <summary>Top edge of the panel in device-independent pixels, saved when the panel is hidden or on exit.</summary>
    public double PanelY { get; set; } = 100;

    /// <summary>Panel size in the "fit" layout, where the user sizes the panel freely.</summary>
    public double PanelWidth { get; set; } = 900;

    /// <summary>Panel height in the "fit" layout.</summary>
    public double PanelHeight { get; set; } = 560;

    /// <summary>Starts WinGlance with Windows, through the current user's Run registry key (default off).</summary>
    public bool AutoStart { get; set; }

    /// <summary>Hides the panel in the tray when it is closed, instead of exiting (default on).</summary>
    public bool CloseToTray { get; set; } = true;

    /// <summary>Outlines a window with a blinking red frame after switching to it from a thumbnail.</summary>
    public bool HighlightOnSwitch { get; set; } = true;

    /// <summary>
    /// Plays a short click after switching to a window from a thumbnail, on the left, in the center or on the
    /// right depending on the monitor the window is on.
    /// </summary>
    public bool ClickOnSwitch { get; set; } = true;

    /// <summary>Global hotkey that shows or hides the panel, written Modifier+Key; default Ctrl+Alt+G.</summary>
    public string Hotkey { get; set; } = "Ctrl+Alt+G";

    /// <summary>Global hotkey that switches to the next window whose agent needs the user.</summary>
    public string NextAgentHotkey { get; set; } = "Ctrl+Alt+N";

    /// <summary>LLM analysis of idle windows (FR-10).</summary>
    public LlmConfig Llm { get; set; } = new();

    /// <summary>Coding agent badges and notifications (FR-11).</summary>
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
    /// Shows a Windows notification when an agent finishes or starts waiting while its
    /// window is not in front.
    /// </summary>
    public bool Notify { get; set; } = true;

    /// <summary>
    /// Delay between two agent discoveries. Each one runs <c>claude agents --json</c>
    /// (~0.2 s of CPU), hence a slower cadence than window polling.
    /// </summary>
    public int RefreshIntervalMs { get; set; } = 5000;

    /// <summary>
    /// Minutes an agent may wait for an answer before its border blinks and a reminder
    /// notification is shown, 0-60 (default 10; 0 turns reminders off).
    /// </summary>
    public int RemindAfterMinutes { get; set; } = 10;
}

/// <summary>
/// Lightweight DTO for a monitored app entry in <c>config.json</c>.
/// Kept separate from <see cref="MonitoredApp"/> to avoid serialization
/// of equality/hash logic.
/// </summary>
internal sealed class MonitoredAppConfig
{
    /// <summary>Process name of the application, as listed in the Applications tab.</summary>
    public string ProcessName { get; set; } = "";

    /// <summary>Name shown for the application; the process name when left blank.</summary>
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

    /// <summary>Turns the LLM analysis on (default off).</summary>
    public bool Enabled { get; set; }

    /// <summary>LLM provider: "openai" (default), "google" or "ollama".</summary>
    public string Provider { get; set; } = "openai";

    /// <summary>Base URL of the provider API; defaults to the OpenAI endpoint.</summary>
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

    /// <summary>Name of the model sent to the provider; defaults to gpt-4o-mini.</summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>Seconds a window must stay unchanged before it is sent for analysis, 10-120 (default 30).</summary>
    public int StaleThresholdSeconds { get; set; } = 30;

    /// <summary>System prompt sent with each screenshot; edited in the Prompt Editor.</summary>
    public string Prompt { get; set; } = DefaultPrompt;

    /// <summary>Logs each verdict and its reason in the Prompt Editor's debug log.</summary>
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
