using System.Windows.Input;
using WinGlance.Models;
using WinGlance.Services;

namespace WinGlance.ViewModels;

/// <summary>
/// ViewModel for the Settings tab. Exposes all configuration properties
/// and applies changes live to the preview when relevant.
/// </summary>
internal sealed class SettingsViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private readonly PreviewViewModel _previewViewModel;
    private readonly Action<double>? _onOpacityChanged;
    private readonly Action<bool> _applyAutoStart;

    private string _layout;
    private int _thumbnailWidth;
    private int _thumbnailHeight;
    private double _panelOpacity;
    private int _pollingIntervalMs;
    private bool _rememberPosition;
    private bool _autoStart;
    private bool _closeToTray;
    private bool _highlightOnSwitch;
    private bool _clickOnSwitch;
    private string _hotkey;
    private string _hotkeyStatus = string.Empty;
    private string _nextAgentHotkey;
    private string _nextAgentHotkeyStatus = string.Empty;
    private bool _agentDetectionEnabled;
    private bool _agentNotifications;
    private bool _sortByUrgency;
    private bool _showOnlyWindowsThatNeedMe;
    private bool _showStatusTime;
    private string _titleFormat;
    private int _remindAfterMinutes;

    // LLM settings
    private bool _llmEnabled;
    private string _llmProvider;
    private string _llmEndpoint;
    private string _llmApiKey;
    private string _llmModel;
    private int _llmStaleThresholdSeconds;

    /// <param name="applyAutoStart">
    /// Writes the "Start with Windows" registry entry; defaults to <see cref="AutoStartService.SetAutoStart"/>.
    /// Tests replace it so that they never touch the user's Run key.
    /// </param>
    public SettingsViewModel(
        ConfigService configService,
        AppConfig config,
        PreviewViewModel previewViewModel,
        Action<double>? onOpacityChanged = null,
        Action<bool>? applyAutoStart = null)
    {
        _configService = configService;
        _config = config;
        _previewViewModel = previewViewModel;
        _onOpacityChanged = onOpacityChanged;
        _applyAutoStart = applyAutoStart ?? AutoStartService.SetAutoStart;

        // Initialize from config
        _layout = config.Layout;
        _thumbnailWidth = config.ThumbnailWidth;
        _thumbnailHeight = config.ThumbnailHeight;
        _panelOpacity = config.PanelOpacity;
        _pollingIntervalMs = config.PollingIntervalMs;
        _rememberPosition = config.RememberPosition;
        _autoStart = config.AutoStart;
        _closeToTray = config.CloseToTray;
        _highlightOnSwitch = config.HighlightOnSwitch;
        _clickOnSwitch = config.ClickOnSwitch;
        _hotkey = config.Hotkey;
        _nextAgentHotkey = config.NextAgentHotkey;
        _agentDetectionEnabled = config.AgentDetection.Enabled;
        _agentNotifications = config.AgentDetection.Notify;
        _sortByUrgency = config.SortByUrgency;
        _showOnlyWindowsThatNeedMe = config.ShowOnlyWindowsThatNeedMe;
        _showStatusTime = config.ShowStatusTime;
        _titleFormat = config.TitleFormat;
        _remindAfterMinutes = config.AgentDetection.RemindAfterMinutes;

        _llmEnabled = config.Llm.Enabled;
        _llmProvider = config.Llm.Provider;
        _llmEndpoint = config.Llm.Endpoint;
        _llmApiKey = config.Llm.ApiKey;
        _llmModel = config.Llm.Model;
        _llmStaleThresholdSeconds = config.Llm.StaleThresholdSeconds;

        SaveCommand = new RelayCommand(_ => Save());
        EditPromptCommand = new RelayCommand(_ => EditPrompt());
    }

    // ── Layout ──────────────────────────────────────────────────────────

    /// <summary>Layout name ("fit", "horizontal", "vertical" or "grid"). Applied live to the preview; persisted by <see cref="Save"/>.</summary>
    public string Layout
    {
        get => _layout;
        set
        {
            if (SetProperty(ref _layout, value))
            {
                _previewViewModel.Layout = value;
                OnPropertyChanged(nameof(IsFitLayout));
            }
        }
    }

    /// <summary>
    /// One list, most urgent first, instead of groups by application. Applied live to the
    /// preview; persisted by <see cref="Save"/> and by the Options panel.
    /// </summary>
    public bool SortByUrgency
    {
        get => _sortByUrgency;
        set
        {
            if (SetProperty(ref _sortByUrgency, value))
                _previewViewModel.SortByUrgency = value;
        }
    }

    /// <summary>
    /// Hides the windows that need nothing. Applied live to the preview; persisted by
    /// <see cref="Save"/> and by the Options panel.
    /// </summary>
    public bool ShowOnlyWindowsThatNeedMe
    {
        get => _showOnlyWindowsThatNeedMe;
        set
        {
            if (SetProperty(ref _showOnlyWindowsThatNeedMe, value))
                _previewViewModel.ShowOnlyWindowsThatNeedMe = value;
        }
    }

    /// <summary>Shows the time since the last status change. Applied live to the preview; persisted by <see cref="Save"/>.</summary>
    public bool ShowStatusTime
    {
        get => _showStatusTime;
        set
        {
            if (SetProperty(ref _showStatusTime, value))
                _previewViewModel.ShowStatusTime = value;
        }
    }

    /// <summary>
    /// Template of the text under each thumbnail ({title}, {project}, {document}, {app}).
    /// Applied live to the preview; persisted by <see cref="Save"/>.
    /// </summary>
    public string TitleFormat
    {
        get => _titleFormat;
        set
        {
            if (SetProperty(ref _titleFormat, value ?? string.Empty))
                _previewViewModel.TitleFormat = _titleFormat;
        }
    }

    /// <summary>True in "Fit to panel", where the thumbnail size sliders do not apply.</summary>
    public bool IsFitLayout => string.Equals(_layout, PreviewViewModel.FitLayout, StringComparison.OrdinalIgnoreCase);

    // ── Thumbnail size ──────────────────────────────────────────────────

    /// <summary>Thumbnail width in pixels (100-500). Applied live to the preview; persisted by <see cref="Save"/>.</summary>
    public int ThumbnailWidth
    {
        get => _thumbnailWidth;
        set
        {
            if (SetProperty(ref _thumbnailWidth, value))
                _previewViewModel.ThumbnailWidth = value;
        }
    }

    /// <summary>Thumbnail height in pixels (75-400). Applied live to the preview; persisted by <see cref="Save"/>.</summary>
    public int ThumbnailHeight
    {
        get => _thumbnailHeight;
        set
        {
            if (SetProperty(ref _thumbnailHeight, value))
                _previewViewModel.ThumbnailHeight = value;
        }
    }

    // ── Panel opacity ───────────────────────────────────────────────────

    /// <summary>Panel opacity (0.3-1.0). Applied live through the opacity callback; persisted by <see cref="Save"/>.</summary>
    public double PanelOpacity
    {
        get => _panelOpacity;
        set
        {
            if (SetProperty(ref _panelOpacity, value))
            {
                _onOpacityChanged?.Invoke(value);
                OnPropertyChanged(nameof(PanelTransparency));
            }
        }
    }

    /// <summary>
    /// Transparency level in percent (0 = opaque, 70 = most transparent): the opacity seen
    /// the other way round, as shown in the Options panel.
    /// </summary>
    public int PanelTransparency
    {
        get => (int)Math.Round((1 - _panelOpacity) * 100);
        set => PanelOpacity = 1 - (Math.Clamp(value, 0, 70) / 100.0);
    }

    /// <summary>
    /// Saves the settings of the Options panel alone (transparency, switch frame, switch click,
    /// agent notifications, order and filter of the windows), without the other pending changes of the Settings view: the
    /// Options panel applies its changes without a Save button.
    /// </summary>
    public void SaveQuickOptions()
    {
        if (_config.PanelOpacity == PanelOpacity
            && _config.HighlightOnSwitch == HighlightOnSwitch
            && _config.ClickOnSwitch == ClickOnSwitch
            && _config.AgentDetection.Notify == AgentNotifications
            && _config.SortByUrgency == SortByUrgency
            && _config.ShowOnlyWindowsThatNeedMe == ShowOnlyWindowsThatNeedMe)
            return;

        _config.PanelOpacity = PanelOpacity;
        _config.HighlightOnSwitch = HighlightOnSwitch;
        _config.ClickOnSwitch = ClickOnSwitch;
        _config.AgentDetection.Notify = AgentNotifications;
        _config.SortByUrgency = SortByUrgency;
        _config.ShowOnlyWindowsThatNeedMe = ShowOnlyWindowsThatNeedMe;
        _configService.Save(_config);
    }

    // ── Polling interval ────────────────────────────────────────────────

    /// <summary>Delay between two window scans in milliseconds (500-5000). Applied live to the preview; persisted by <see cref="Save"/>.</summary>
    public int PollingIntervalMs
    {
        get => _pollingIntervalMs;
        set
        {
            if (SetProperty(ref _pollingIntervalMs, value))
                _previewViewModel.PollingIntervalMs = value;
        }
    }

    // ── General settings ────────────────────────────────────────────────

    /// <summary>Reopens the panel at its saved position. Takes effect once saved.</summary>
    public bool RememberPosition
    {
        get => _rememberPosition;
        set => SetProperty(ref _rememberPosition, value);
    }

    /// <summary>Starts WinGlance with Windows. Written to the registry by <see cref="Save"/>.</summary>
    public bool AutoStart
    {
        get => _autoStart;
        set => SetProperty(ref _autoStart, value);
    }

    /// <summary>Hides the panel in the tray when it is closed. Takes effect once saved.</summary>
    public bool CloseToTray
    {
        get => _closeToTray;
        set => SetProperty(ref _closeToTray, value);
    }

    /// <summary>
    /// Outlines the window switched to from a thumbnail with a blinking red frame.
    /// Read live by MainWindow at each switch.
    /// </summary>
    public bool HighlightOnSwitch
    {
        get => _highlightOnSwitch;
        set => SetProperty(ref _highlightOnSwitch, value);
    }

    /// <summary>
    /// Plays a click on the side of the monitor the window switched to is on (left, center, right).
    /// Read live by MainWindow at each switch.
    /// </summary>
    public bool ClickOnSwitch
    {
        get => _clickOnSwitch;
        set => SetProperty(ref _clickOnSwitch, value);
    }

    /// <summary>Global show/hide hotkey, e.g. Ctrl+Alt+G. Registered by <see cref="Save"/>.</summary>
    public string Hotkey
    {
        get => _hotkey;
        set => SetProperty(ref _hotkey, value);
    }

    /// <summary>Error shown under the hotkey field; empty while the hotkey is registered.</summary>
    public string HotkeyStatus
    {
        get => _hotkeyStatus;
        private set => SetProperty(ref _hotkeyStatus, value);
    }

    /// <summary>
    /// Registers a hotkey string globally; returns false if it is invalid or taken.
    /// Set by MainWindow, which owns the window the hotkey is bound to.
    /// </summary>
    public Func<string, bool>? ApplyHotkey { get; set; }

    /// <summary>Updates <see cref="HotkeyStatus"/> after a registration attempt.</summary>
    internal void ReportHotkeyResult(bool registered) => HotkeyStatus = DescribeRegistration(registered);

    /// <summary>
    /// Hotkey that switches to the next window whose agent needs the user; empty for none.
    /// </summary>
    public string NextAgentHotkey
    {
        get => _nextAgentHotkey;
        set => SetProperty(ref _nextAgentHotkey, value);
    }

    /// <summary>Error shown under the "next agent" hotkey field; empty while it is registered.</summary>
    public string NextAgentHotkeyStatus
    {
        get => _nextAgentHotkeyStatus;
        private set => SetProperty(ref _nextAgentHotkeyStatus, value);
    }

    /// <summary>
    /// Registers the "next agent" hotkey; returns false if it is invalid or taken. An empty
    /// string removes it. Set by MainWindow.
    /// </summary>
    public Func<string, bool>? ApplyNextAgentHotkey { get; set; }

    /// <summary>Updates <see cref="NextAgentHotkeyStatus"/> after a registration attempt.</summary>
    internal void ReportNextAgentHotkeyResult(bool registered) =>
        NextAgentHotkeyStatus = DescribeRegistration(registered);

    private static string DescribeRegistration(bool registered) => registered
        ? string.Empty
        : "Not registered: use Modifier+Key (e.g. Ctrl+Alt+G). The combination may be used by another application.";

    // ── Coding agents ──────────────────────────────────────────────────

    /// <summary>
    /// Shows a Windows notification when an agent finishes or starts waiting while its window
    /// is not in front. Read live by MainWindow.
    /// </summary>
    public bool AgentNotifications
    {
        get => _agentNotifications;
        set => SetProperty(ref _agentNotifications, value);
    }

    /// <summary>
    /// Minutes an agent may wait for an answer before its border blinks and a reminder is
    /// shown (0-60, 0: never). Applied live to the preview; persisted by <see cref="Save"/>.
    /// </summary>
    public int RemindAfterMinutes
    {
        get => _remindAfterMinutes;
        set
        {
            if (SetProperty(ref _remindAfterMinutes, Math.Clamp(value, 0, 60)))
            {
                _previewViewModel.RemindAfter = TimeSpan.FromMinutes(_remindAfterMinutes);
                OnPropertyChanged(nameof(RemindAfterText));
            }
        }
    }

    /// <summary>The reminder delay as shown next to its slider: "10 min", or "Never" at 0.</summary>
    public string RemindAfterText => _remindAfterMinutes == 0 ? "Never" : $"{_remindAfterMinutes} min";

    /// <summary>
    /// Shows Claude Code / OpenCode status on thumbnails. Applied live: turning it off
    /// removes the monitor, and the next poll clears every badge.
    /// </summary>
    public bool AgentDetectionEnabled
    {
        get => _agentDetectionEnabled;
        set
        {
            if (SetProperty(ref _agentDetectionEnabled, value))
            {
                _previewViewModel.AgentMonitor = value
                    ? new AgentMonitor(_config.AgentDetection.RefreshIntervalMs)
                    : null;
            }
        }
    }

    // ── LLM settings ───────────────────────────────────────────────────

    /// <summary>Turns the LLM analysis on. Takes effect on <see cref="Save"/>, through <see cref="LlmSettingsChanged"/>.</summary>
    public bool LlmEnabled
    {
        get => _llmEnabled;
        set => SetProperty(ref _llmEnabled, value);
    }

    /// <summary>
    /// LLM provider. Switching provider also switches the endpoint and model when they still
    /// hold a provider default: Gemini requests used to go to the OpenAI endpoint.
    /// Custom values are kept.
    /// </summary>
    public string LlmProvider
    {
        get => _llmProvider;
        set
        {
            if (!SetProperty(ref _llmProvider, value)
                || !LlmConfig.ProviderDefaults.TryGetValue(value ?? "", out var defaults))
                return;

            var knownDefaults = LlmConfig.ProviderDefaults.Values;
            if (string.IsNullOrWhiteSpace(LlmEndpoint) || knownDefaults.Any(d => d.Endpoint == LlmEndpoint))
                LlmEndpoint = defaults.Endpoint;
            if (string.IsNullOrWhiteSpace(LlmModel) || knownDefaults.Any(d => d.Model == LlmModel))
                LlmModel = defaults.Model;
        }
    }

    /// <summary>Base URL of the LLM provider API. Takes effect on <see cref="Save"/>.</summary>
    public string LlmEndpoint
    {
        get => _llmEndpoint;
        set => SetProperty(ref _llmEndpoint, value);
    }

    /// <summary>API key of the LLM provider, stored encrypted in config.json. Takes effect on <see cref="Save"/>.</summary>
    public string LlmApiKey
    {
        get => _llmApiKey;
        set => SetProperty(ref _llmApiKey, value);
    }

    /// <summary>Model name sent to the LLM provider. Takes effect on <see cref="Save"/>.</summary>
    public string LlmModel
    {
        get => _llmModel;
        set => SetProperty(ref _llmModel, value);
    }

    /// <summary>Seconds a window must stay unchanged before it is analyzed (10-120). Takes effect on <see cref="Save"/>.</summary>
    public int LlmStaleThresholdSeconds
    {
        get => _llmStaleThresholdSeconds;
        set => SetProperty(ref _llmStaleThresholdSeconds, value);
    }

    // ── Navigation ──────────────────────────────────────────────────────

    /// <summary>
    /// Callback set by MainWindow to navigate to the Prompt Editor screen.
    /// </summary>
    public Action? NavigateToPromptEditor { get; set; }

    /// <summary>
    /// Called after <see cref="Save"/> when an LLM setting changed, so that the analysis
    /// service is started, stopped or rebuilt without a restart.
    /// </summary>
    public Action? LlmSettingsChanged { get; set; }

    // ── Commands ────────────────────────────────────────────────────────

    /// <summary>Saves all the settings (see <see cref="Save"/>).</summary>
    public ICommand SaveCommand { get; }

    /// <summary>Opens the Prompt Editor screen.</summary>
    public ICommand EditPromptCommand { get; }

    /// <summary>
    /// Persists all settings to the config file.
    /// </summary>
    public void Save()
    {
        _config.Layout = Layout;
        _config.SortByUrgency = SortByUrgency;
        _config.ShowOnlyWindowsThatNeedMe = ShowOnlyWindowsThatNeedMe;
        _config.ShowStatusTime = ShowStatusTime;
        _config.TitleFormat = string.IsNullOrWhiteSpace(TitleFormat) ? Services.WindowTitleFormat.Default : TitleFormat.Trim();
        _config.ThumbnailWidth = ThumbnailWidth;
        _config.ThumbnailHeight = ThumbnailHeight;
        _config.PanelOpacity = PanelOpacity;
        _config.PollingIntervalMs = PollingIntervalMs;
        _config.RememberPosition = RememberPosition;
        _config.AutoStart = AutoStart;
        _config.CloseToTray = CloseToTray;
        _config.HighlightOnSwitch = HighlightOnSwitch;
        _config.ClickOnSwitch = ClickOnSwitch;
        _config.Hotkey = Hotkey;
        _config.NextAgentHotkey = NextAgentHotkey.Trim();
        _config.AgentDetection.Enabled = AgentDetectionEnabled;
        _config.AgentDetection.Notify = AgentNotifications;
        _config.AgentDetection.RemindAfterMinutes = RemindAfterMinutes;

        var llm = _config.Llm;
        var llmChanged = (llm.Enabled, llm.Provider, llm.Endpoint, llm.ApiKey, llm.Model, llm.StaleThresholdSeconds)
            != (LlmEnabled, LlmProvider, LlmEndpoint, LlmApiKey, LlmModel, LlmStaleThresholdSeconds);

        _config.Llm.Enabled = LlmEnabled;
        _config.Llm.Provider = LlmProvider;
        _config.Llm.Endpoint = LlmEndpoint;
        _config.Llm.ApiKey = LlmApiKey;
        _config.Llm.Model = LlmModel;
        _config.Llm.StaleThresholdSeconds = LlmStaleThresholdSeconds;

        _configService.Save(_config);

        // Sync auto-start registry entry
        _applyAutoStart(AutoStart);

        // The hotkey applies immediately, not at next launch
        if (ApplyHotkey is { } applyHotkey)
            ReportHotkeyResult(applyHotkey(Hotkey));
        if (ApplyNextAgentHotkey is { } applyNextAgentHotkey)
            ReportNextAgentHotkeyResult(applyNextAgentHotkey(NextAgentHotkey.Trim()));

        if (llmChanged)
            LlmSettingsChanged?.Invoke();
    }

    /// <summary>
    /// Navigates to the in-app Prompt Editor screen.
    /// </summary>
    private void EditPrompt()
    {
        NavigateToPromptEditor?.Invoke();
    }
}
