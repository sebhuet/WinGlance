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
    private readonly Func<List<DiscoveredApp>> _discoverRunningApps;
    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private readonly Action<IReadOnlyList<MonitoredApp>> _onMonitoredAppsChanged;

    /// <param name="discoverRunningApps">
    /// Scans the running applications; defaults to <see cref="WindowEnumerator.DiscoverRunningApps"/>.
    /// </param>
    public ApplicationsViewModel(
        ConfigService configService,
        AppConfig config,
        Action<IReadOnlyList<MonitoredApp>> onMonitoredAppsChanged,
        Func<List<DiscoveredApp>>? discoverRunningApps = null)
    {
        _configService = configService;
        _config = config;
        _onMonitoredAppsChanged = onMonitoredAppsChanged;
        _discoverRunningApps = discoverRunningApps ?? new WindowEnumerator().DiscoverRunningApps;

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
    /// Already-monitored apps retain their checked state and display name; monitored apps
    /// that are not running are listed too (0 windows), so that saving does not drop them.
    /// </summary>
    public void Refresh()
    {
        var discovered = _discoverRunningApps();

        // A hand-edited config can list the same process twice; the first entry wins
        var monitored = new Dictionary<string, MonitoredAppConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in _config.MonitoredApps)
            monitored.TryAdd(app.ProcessName, app);

        Apps.Clear();

        foreach (var app in discovered)
        {
            var isMonitored = monitored.Remove(app.ProcessName, out var existing);
            Apps.Add(new AppEntry(app.ProcessName)
            {
                WindowCount = app.WindowCount,
                IsMonitored = isMonitored,
                DisplayName = isMonitored ? existing!.DisplayName : app.ProcessName,
            });
        }

        // What remains is monitored but not running right now
        foreach (var app in monitored.Values.OrderBy(a => a.ProcessName, StringComparer.OrdinalIgnoreCase))
        {
            Apps.Add(new AppEntry(app.ProcessName)
            {
                WindowCount = 0,
                IsMonitored = true,
                DisplayName = app.DisplayName,
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
                // An emptied name would give the group an empty header
                DisplayName = string.IsNullOrWhiteSpace(a.DisplayName) ? a.ProcessName : a.DisplayName.Trim(),
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
