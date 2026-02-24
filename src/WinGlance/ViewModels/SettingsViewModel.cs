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

    private string _layout;
    private int _thumbnailWidth;
    private int _thumbnailHeight;
    private double _panelOpacity;
    private int _pollingIntervalMs;
    private bool _rememberPosition;
    private bool _autoStart;
    private bool _closeToTray;
    private string _hotkey;

    // LLM settings
    private bool _llmEnabled;
    private string _llmProvider;
    private string _llmEndpoint;
    private string _llmApiKey;
    private string _llmModel;
    private int _llmStaleThresholdSeconds;

    public SettingsViewModel(
        ConfigService configService,
        AppConfig config,
        PreviewViewModel previewViewModel,
        Action<double>? onOpacityChanged = null)
    {
        _configService = configService;
        _config = config;
        _previewViewModel = previewViewModel;
        _onOpacityChanged = onOpacityChanged;

        // Initialize from config
        _layout = config.Layout;
        _thumbnailWidth = config.ThumbnailWidth;
        _thumbnailHeight = config.ThumbnailHeight;
        _panelOpacity = config.PanelOpacity;
        _pollingIntervalMs = config.PollingIntervalMs;
        _rememberPosition = config.RememberPosition;
        _autoStart = config.AutoStart;
        _closeToTray = config.CloseToTray;
        _hotkey = config.Hotkey;

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

    public string Layout
    {
        get => _layout;
        set
        {
            if (SetProperty(ref _layout, value))
                _previewViewModel.Layout = value;
        }
    }

    // ── Thumbnail size ──────────────────────────────────────────────────

    public int ThumbnailWidth
    {
        get => _thumbnailWidth;
        set
        {
            if (SetProperty(ref _thumbnailWidth, value))
                _previewViewModel.ThumbnailWidth = value;
        }
    }

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

    public double PanelOpacity
    {
        get => _panelOpacity;
        set
        {
            if (SetProperty(ref _panelOpacity, value))
                _onOpacityChanged?.Invoke(value);
        }
    }

    // ── Polling interval ────────────────────────────────────────────────

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

    public bool RememberPosition
    {
        get => _rememberPosition;
        set => SetProperty(ref _rememberPosition, value);
    }

    public bool AutoStart
    {
        get => _autoStart;
        set => SetProperty(ref _autoStart, value);
    }

    public bool CloseToTray
    {
        get => _closeToTray;
        set => SetProperty(ref _closeToTray, value);
    }

    public string Hotkey
    {
        get => _hotkey;
        set => SetProperty(ref _hotkey, value);
    }

    // ── LLM settings ───────────────────────────────────────────────────

    public bool LlmEnabled
    {
        get => _llmEnabled;
        set => SetProperty(ref _llmEnabled, value);
    }

    public string LlmProvider
    {
        get => _llmProvider;
        set => SetProperty(ref _llmProvider, value);
    }

    public string LlmEndpoint
    {
        get => _llmEndpoint;
        set => SetProperty(ref _llmEndpoint, value);
    }

    public string LlmApiKey
    {
        get => _llmApiKey;
        set => SetProperty(ref _llmApiKey, value);
    }

    public string LlmModel
    {
        get => _llmModel;
        set => SetProperty(ref _llmModel, value);
    }

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

    // ── Commands ────────────────────────────────────────────────────────

    public ICommand SaveCommand { get; }
    public ICommand EditPromptCommand { get; }

    /// <summary>
    /// Persists all settings to the config file.
    /// </summary>
    public void Save()
    {
        _config.Layout = Layout;
        _config.ThumbnailWidth = ThumbnailWidth;
        _config.ThumbnailHeight = ThumbnailHeight;
        _config.PanelOpacity = PanelOpacity;
        _config.PollingIntervalMs = PollingIntervalMs;
        _config.RememberPosition = RememberPosition;
        _config.AutoStart = AutoStart;
        _config.CloseToTray = CloseToTray;
        _config.Hotkey = Hotkey;

        _config.Llm.Enabled = LlmEnabled;
        _config.Llm.Provider = LlmProvider;
        _config.Llm.Endpoint = LlmEndpoint;
        _config.Llm.ApiKey = LlmApiKey;
        _config.Llm.Model = LlmModel;
        _config.Llm.StaleThresholdSeconds = LlmStaleThresholdSeconds;

        _configService.Save(_config);

        // Sync auto-start registry entry
        Services.AutoStartService.SetAutoStart(AutoStart);
    }

    /// <summary>
    /// Navigates to the in-app Prompt Editor screen.
    /// </summary>
    private void EditPrompt()
    {
        NavigateToPromptEditor?.Invoke();
    }
}
