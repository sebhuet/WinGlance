using System.Collections.ObjectModel;
using System.Windows.Input;
using WinGlance.Models;
using WinGlance.Services;

namespace WinGlance.ViewModels;

/// <summary>
/// ViewModel for the Applications tab.
/// Discovers running apps and lets the user toggle which ones to monitor.
/// </summary>
internal sealed class ApplicationsViewModel : ViewModelBase
{
    private readonly WindowEnumerator _enumerator = new();
    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private readonly Action<IReadOnlyList<MonitoredApp>> _onMonitoredAppsChanged;

    public ApplicationsViewModel(
        ConfigService configService,
        AppConfig config,
        Action<IReadOnlyList<MonitoredApp>> onMonitoredAppsChanged)
    {
        _configService = configService;
        _config = config;
        _onMonitoredAppsChanged = onMonitoredAppsChanged;

        RefreshCommand = new RelayCommand(_ => Refresh());
        SaveCommand = new RelayCommand(_ => Save());
    }

    /// <summary>Discovered applications available for monitoring.</summary>
    public ObservableCollection<AppEntry> Apps { get; } = [];

    /// <summary>Re-scans running applications.</summary>
    public ICommand RefreshCommand { get; }

    /// <summary>Persists the current selection to config and notifies the preview tab.</summary>
    public ICommand SaveCommand { get; }

    /// <summary>
    /// Scans running applications and merges with the current monitored list.
    /// Already-monitored apps retain their checked state.
    /// </summary>
    public void Refresh()
    {
        var discovered = _enumerator.DiscoverRunningApps();
        var monitored = _config.MonitoredApps
            .ToDictionary(a => a.ProcessName, StringComparer.OrdinalIgnoreCase);

        Apps.Clear();

        foreach (var app in discovered)
        {
            var isMonitored = monitored.TryGetValue(app.ProcessName, out var existing);
            Apps.Add(new AppEntry(app.ProcessName)
            {
                WindowCount = app.WindowCount,
                IsMonitored = isMonitored,
                DisplayName = isMonitored ? existing!.DisplayName : app.ProcessName,
            });
        }
    }

    /// <summary>
    /// Saves the current monitored app selection to config and
    /// pushes the updated list to the preview tab.
    /// </summary>
    public void Save()
    {
        var monitoredConfigs = Apps
            .Where(a => a.IsMonitored)
            .Select(a => new MonitoredAppConfig
            {
                ProcessName = a.ProcessName,
                DisplayName = a.DisplayName,
            })
            .ToList();

        _config.MonitoredApps = monitoredConfigs;
        _configService.Save(_config);

        // Notify preview tab
        var monitoredApps = monitoredConfigs
            .Select(a => new MonitoredApp(a.ProcessName, a.DisplayName))
            .ToList();
        _onMonitoredAppsChanged(monitoredApps);
    }
}

/// <summary>
/// Represents a discovered application entry in the Applications tab.
/// </summary>
internal sealed class AppEntry : ViewModelBase
{
    private int _windowCount;
    private bool _isMonitored;
    private string _displayName;

    public AppEntry(string processName)
    {
        ProcessName = processName;
        _displayName = processName;
    }

    /// <summary>The process name (read-only identifier).</summary>
    public string ProcessName { get; }

    /// <summary>Number of visible windows for this application.</summary>
    public int WindowCount
    {
        get => _windowCount;
        set => SetProperty(ref _windowCount, value);
    }

    /// <summary>Whether this application is selected for monitoring.</summary>
    public bool IsMonitored
    {
        get => _isMonitored;
        set => SetProperty(ref _isMonitored, value);
    }

    /// <summary>User-visible display name (defaults to ProcessName, editable).</summary>
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }
}
