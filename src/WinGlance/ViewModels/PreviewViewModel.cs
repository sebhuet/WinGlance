using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Windows.Threading;
using WinGlance.Models;
using WinGlance.Services;

namespace WinGlance.ViewModels;

internal sealed class PreviewViewModel : ViewModelBase, IDisposable
{
    private readonly WindowEnumerator _enumerator = new();
    private readonly DispatcherTimer _timer;
    private readonly object _lock = new();

    private ObservableCollection<TrackedWindow> _windows = [];

    public PreviewViewModel()
    {
        BindingOperations.EnableCollectionSynchronization(Windows, _lock);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000),
        };
        _timer.Tick += OnTimerTick;
    }

    // ── Public API ──────────────────────────────────────────────────────

    public ObservableCollection<TrackedWindow> Windows
    {
        get => _windows;
        private set => SetProperty(ref _windows, value);
    }

    /// <summary>
    /// The apps to monitor. Set by the main view model or settings.
    /// </summary>
    public IReadOnlyList<MonitoredApp> MonitoredApps { get; set; } = [];

    /// <summary>
    /// Polling interval in milliseconds. Updates the timer when changed.
    /// </summary>
    public int PollingIntervalMs
    {
        get => (int)_timer.Interval.TotalMilliseconds;
        set
        {
            var clamped = Math.Clamp(value, 500, 5000);
            if ((int)_timer.Interval.TotalMilliseconds == clamped)
                return;
            _timer.Interval = TimeSpan.FromMilliseconds(clamped);
            OnPropertyChanged();
        }
    }

    public bool IsPolling => _timer.IsEnabled;

    public void Start()
    {
        if (!_timer.IsEnabled)
        {
            Poll(); // immediate first scan
            _timer.Start();
        }
    }

    public void Stop()
    {
        _timer.Stop();
    }

    public void Dispose()
    {
        _timer.Stop();
    }

    // ── Polling logic ───────────────────────────────────────────────────

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        await Task.Run(Poll);
    }

    private void Poll()
    {
        var scanned = _enumerator.GetWindows(MonitoredApps);
        MergeResults(scanned);
    }

    /// <summary>
    /// Diffs scanned windows against the current collection:
    /// - Add new windows
    /// - Remove closed windows
    /// - Update properties of existing windows
    /// </summary>
    private void MergeResults(List<TrackedWindow> scanned)
    {
        lock (_lock)
        {
            var scannedByHwnd = scanned.ToDictionary(w => w.Hwnd);
            var existingByHwnd = _windows.ToDictionary(w => w.Hwnd);

            // Remove windows no longer present
            for (var i = _windows.Count - 1; i >= 0; i--)
            {
                if (!scannedByHwnd.ContainsKey(_windows[i].Hwnd))
                    _windows.RemoveAt(i);
            }

            // Update existing or add new
            foreach (var scannedWindow in scanned)
            {
                if (existingByHwnd.TryGetValue(scannedWindow.Hwnd, out var existing))
                {
                    // Update mutable properties
                    existing.Title = scannedWindow.Title;
                    existing.IsActive = scannedWindow.IsActive;
                    existing.IsHung = scannedWindow.IsHung;
                    existing.IsModalBlocked = scannedWindow.IsModalBlocked;
                    // Icon only updated if it was null
                    if (existing.Icon is null && scannedWindow.Icon is not null)
                        existing.Icon = scannedWindow.Icon;
                }
                else
                {
                    _windows.Add(scannedWindow);
                }
            }
        }
    }
}
