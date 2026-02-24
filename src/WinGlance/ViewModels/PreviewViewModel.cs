using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using WinGlance.Models;
using WinGlance.NativeApi;
using WinGlance.Services;

namespace WinGlance.ViewModels;

/// <summary>
/// ViewModel for the Preview tab. Polls monitored windows at a configurable interval
/// using <see cref="WindowEnumerator"/> and maintains a live <see cref="ObservableCollection{T}"/>
/// of <see cref="TrackedWindow"/> instances for data binding.
/// </summary>
internal sealed class PreviewViewModel : ViewModelBase, IDisposable
{
    private readonly WindowEnumerator _enumerator = new();
    private readonly ThumbnailManager _thumbnailManager = new();
    private readonly DispatcherTimer _timer;
    private readonly object _lock = new(); // guards _windows for cross-thread collection sync

    private ObservableCollection<TrackedWindow> _windows = [];
    private string _layout = "horizontal";
    private int _thumbnailWidth = 200;
    private int _thumbnailHeight = 150;

    public PreviewViewModel()
    {
        BindingOperations.EnableCollectionSynchronization(Windows, _lock);

        GroupedWindows = CollectionViewSource.GetDefaultView(Windows);
        GroupedWindows.GroupDescriptions.Add(
            new PropertyGroupDescription(nameof(TrackedWindow.DisplayName)));

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000),
        };
        _timer.Tick += OnTimerTick;

        ActivateWindowCommand = new RelayCommand(ActivateWindow);
    }

    // ── Public API ──────────────────────────────────────────────────────

    /// <summary>Live collection of tracked windows, bound to the Preview tab ItemsControl.</summary>
    public ObservableCollection<TrackedWindow> Windows
    {
        get => _windows;
        private set => SetProperty(ref _windows, value);
    }

    /// <summary>Grouped view of <see cref="Windows"/> for the ItemsControl.</summary>
    public ICollectionView GroupedWindows { get; }

    /// <summary>The ThumbnailManager passed down to ThumbnailControls.</summary>
    public ThumbnailManager ThumbnailManager => _thumbnailManager;

    /// <summary>
    /// The apps to monitor. Set by the main view model or settings.
    /// </summary>
    public IReadOnlyList<MonitoredApp> MonitoredApps { get; set; } = [];

    /// <summary>
    /// Optional attention detector for flash notifications. Set by MainWindow after shell hook init.
    /// </summary>
    public AttentionDetector? AttentionDetector { get; set; }

    /// <summary>
    /// Optional LLM service for stale window analysis. Set by MainWindow after config load.
    /// </summary>
    public LlmService? LlmService { get; set; }

    /// <summary>
    /// Layout mode: "horizontal", "vertical", or "grid".
    /// </summary>
    public string Layout
    {
        get => _layout;
        set => SetProperty(ref _layout, value);
    }

    /// <summary>Desired thumbnail width in device-independent pixels.</summary>
    public int ThumbnailWidth
    {
        get => _thumbnailWidth;
        set => SetProperty(ref _thumbnailWidth, value);
    }

    /// <summary>Desired thumbnail height in device-independent pixels.</summary>
    public int ThumbnailHeight
    {
        get => _thumbnailHeight;
        set => SetProperty(ref _thumbnailHeight, value);
    }

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

    /// <summary>True if the polling timer is currently running.</summary>
    public bool IsPolling => _timer.IsEnabled;

    /// <summary>
    /// Command that activates (brings to foreground) a clicked window.
    /// Parameter is the <see cref="TrackedWindow"/> to activate.
    /// </summary>
    public ICommand ActivateWindowCommand { get; }

    /// <summary>
    /// Sets the destination HWND on the ThumbnailManager. Call after the window is loaded.
    /// </summary>
    public void SetDestinationHwnd(IntPtr hwnd)
    {
        _thumbnailManager.SetDestination(hwnd);
    }

    /// <summary>Starts polling. Performs an immediate first scan then starts the timer.</summary>
    public void Start()
    {
        if (!_timer.IsEnabled)
        {
            Poll(); // immediate first scan
            _timer.Start();
        }
    }

    /// <summary>Stops the polling timer.</summary>
    public void Stop()
    {
        _timer.Stop();
    }

    public void Dispose()
    {
        _timer.Stop();
        _thumbnailManager.Dispose();
        LlmService?.Dispose();
    }

    // ── Click-to-switch ────────────────────────────────────────────────

    /// <summary>
    /// Activates the given window: restores it if minimized, then brings it to the foreground.
    /// Uses an Alt key simulation workaround to bypass Windows focus-stealing prevention.
    /// </summary>
    private static void ActivateWindow(object? parameter)
    {
        if (parameter is not TrackedWindow window)
            return;

        var hwnd = window.Hwnd;

        // Restore minimized windows
        if (NativeMethods.IsIconic(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);

        // Workaround for focus-stealing prevention:
        // Simulate an Alt key press/release so Windows allows SetForegroundWindow.
        var input = new NativeMethods.INPUT
        {
            Type = NativeMethods.INPUT_KEYBOARD,
            Union = new NativeMethods.InputUnion
            {
                Keyboard = new NativeMethods.KEYBDINPUT { VirtualKey = NativeMethods.VK_MENU },
            },
        };
        NativeMethods.SendInput(1, [input], NativeMethods.INPUT.Size);

        input.Union.Keyboard.Flags = NativeMethods.KEYEVENTF_KEYUP;
        NativeMethods.SendInput(1, [input], NativeMethods.INPUT.Size);

        NativeMethods.SetForegroundWindow(hwnd);
    }

    // ── Polling logic ───────────────────────────────────────────────────

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        await Task.Run(Poll);

        // LLM evaluation runs after merge, on background thread
        if (LlmService is not null)
        {
            TrackedWindow[] snapshot;
            lock (_lock)
            {
                snapshot = [.. _windows];
            }
            foreach (var w in snapshot)
            {
                await LlmService.EvaluateAsync(w);
            }
        }
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
                {
                    _thumbnailManager.Unregister(_windows[i].Hwnd);
                    LlmService?.Remove(_windows[i].Hwnd);
                    _windows.RemoveAt(i);
                }
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

                    // Flash detection via shell hook
                    if (AttentionDetector is not null)
                    {
                        existing.IsFlashing = AttentionDetector.IsFlashing(existing.Hwnd);
                        // Auto-clear flashing when window gains focus
                        if (existing.IsActive && existing.IsFlashing)
                        {
                            AttentionDetector.ClearFlashing(existing.Hwnd);
                            existing.IsFlashing = false;
                        }
                    }

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
