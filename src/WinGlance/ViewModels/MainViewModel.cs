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

    public MainViewModel(ConfigService configService, AppConfig config)
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
        };

        ApplicationsViewModel = new ApplicationsViewModel(
            configService, config, OnMonitoredAppsChanged);
    }

    /// <summary>Index of the currently selected tab (0=Preview, 1=Applications, 2=Settings).</summary>
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    /// <summary>ViewModel for the Preview tab.</summary>
    public PreviewViewModel PreviewViewModel { get; }

    /// <summary>ViewModel for the Applications tab.</summary>
    public ApplicationsViewModel ApplicationsViewModel { get; }

    /// <summary>The configuration service for load/save operations.</summary>
    public ConfigService ConfigService { get; }

    /// <summary>The current in-memory configuration.</summary>
    public AppConfig Config { get; }

    private void OnMonitoredAppsChanged(IReadOnlyList<MonitoredApp> apps)
    {
        PreviewViewModel.MonitoredApps = apps;
    }
}
