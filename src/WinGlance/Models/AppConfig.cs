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
