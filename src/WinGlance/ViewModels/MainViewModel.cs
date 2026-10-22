using WinGlance.Models;
using WinGlance.Services;

namespace WinGlance.ViewModels;

/// <summary>
/// Top-level ViewModel that orchestrates the three tabs and holds shared state.
/// Bound to <see cref="MainWindow"/> as its DataContext.
/// </summary>
internal class MainViewModel : ViewModelBase
{
    private int _selectedTabIndex;

    public MainViewModel(ConfigService configService, AppConfig config, Action<double>? onOpacityChanged = null)
    {
        ConfigService = configService;
        Config = config;

        PreviewViewModel = new PreviewViewModel
        {
            Layout = config.Layout,
            ThumbnailWidth = config.ThumbnailWidth,
            ThumbnailHeight = config.ThumbnailHeight,
            PollingIntervalMs = config.PollingIntervalMs,
            MonitoredApps = config.MonitoredApps
                .Select(a => new MonitoredApp(a.ProcessName, a.DisplayName))
                .ToList(),
            AgentMonitor = config.AgentDetection.Enabled
                ? new AgentMonitor(config.AgentDetection.RefreshIntervalMs)
                : null,
        };

        ApplicationsViewModel = new ApplicationsViewModel(
            configService, config, OnMonitoredAppsChanged);

        SettingsViewModel = new SettingsViewModel(
            configService, config, PreviewViewModel, onOpacityChanged);

        PromptEditorViewModel = new PromptEditorViewModel(configService, config);

        // LLM analysis follows the settings live, and a saved prompt applies immediately
        SettingsViewModel.LlmSettingsChanged = ApplyLlmSettings;
        PromptEditorViewModel.PromptSaved = prompt => PreviewViewModel.LlmService?.ReloadPrompt(prompt);
        ApplyLlmSettings();
    }

    /// <summary>
    /// Starts, rebuilds or stops the LLM service to match <see cref="AppConfig.Llm"/>.
    /// Turning analysis off also clears the verdicts it left on the thumbnails.
    /// </summary>
    internal void ApplyLlmSettings()
    {
        if (PreviewViewModel.LlmService is { } previous)
        {
            PreviewViewModel.LlmService = null;
            previous.Dispose(); // cancels its calls in flight

            foreach (var window in PreviewViewModel.GetWindowsSnapshot())
            {
                window.LlmVerdict = null;
                window.IsStale = false;
            }
        }

        if (!Config.Llm.Enabled)
        {
            PromptEditorViewModel.RunAnalysisFunc = null;
            return;
        }

        var service = new LlmService(Config.Llm) { DebugLogger = PromptEditorViewModel };
        PreviewViewModel.LlmService = service;
        PromptEditorViewModel.RunAnalysisFunc = () => RunAnalysisAsync(service);
    }

    /// <summary>
    /// "Run" button of the Prompt Editor: analyzes every tracked window now, off the UI thread.
    /// </summary>
    private async Task RunAnalysisAsync(LlmService service)
    {
        var windows = PreviewViewModel.GetWindowsSnapshot();
        if (windows.Length == 0)
        {
            PromptEditorViewModel.AppendLog("—", "—", "No monitored windows found");
            return;
        }

        await Task.Run(async () =>
        {
            foreach (var window in windows)
                await service.ForceEvaluateAsync(window).ConfigureAwait(false);
        });
    }

    /// <summary>Index of the currently selected tab (0=Preview, 1=Applications, 2=Settings, 3=Prompt).</summary>
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    /// <summary>ViewModel for the Preview tab.</summary>
    public PreviewViewModel PreviewViewModel { get; }

    /// <summary>ViewModel for the Applications tab.</summary>
    public ApplicationsViewModel ApplicationsViewModel { get; }

    /// <summary>ViewModel for the Settings tab.</summary>
    public SettingsViewModel SettingsViewModel { get; }

    /// <summary>ViewModel for the Prompt Editor screen.</summary>
    public PromptEditorViewModel PromptEditorViewModel { get; }

    /// <summary>The configuration service for load/save operations.</summary>
    public ConfigService ConfigService { get; }

    /// <summary>The current in-memory configuration.</summary>
    public AppConfig Config { get; }

    /// <summary>
    /// Returns the saved panel position when it should be restored and the title bar would be
    /// reachable on the current screens; null to let the window center itself instead.
    /// </summary>
    /// <param name="virtualScreen">Bounding box of all monitors, in device-independent pixels.</param>
    internal static System.Windows.Point? GetRestorablePosition(AppConfig config, System.Windows.Rect virtualScreen)
    {
        if (!config.RememberPosition || !double.IsFinite(config.PanelX) || !double.IsFinite(config.PanelY))
            return null;

        // A point inside the title bar, left of the buttons: it must be on screen to drag the panel
        var titleBarPoint = new System.Windows.Point(config.PanelX + 40, config.PanelY + 16);
        return virtualScreen.Contains(titleBarPoint)
            ? new System.Windows.Point(config.PanelX, config.PanelY)
            : null;
    }

    private void OnMonitoredAppsChanged(IReadOnlyList<MonitoredApp> apps)
    {
        PreviewViewModel.MonitoredApps = apps;
    }
}
