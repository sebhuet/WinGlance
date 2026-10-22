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

    // 1 while a scan / an LLM evaluation pass runs: ticks never pile up behind a slow one
    private int _isScanning;
    private int _isEvaluating;

    /// <summary>Layout in which thumbnails grow with the panel, as large as possible.</summary>
    public const string FitLayout = "fit";

    private ObservableCollection<TrackedWindow> _windows = [];
    private string _layout = FitLayout;
    private int _thumbnailWidth = 200;
    private int _thumbnailHeight = 150;
    private double _displayThumbnailWidth = 200;
    private double _displayThumbnailHeight = 150;
    private System.Windows.Size _viewportSize = System.Windows.Size.Empty;

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
    /// Optional coding agent monitor (Claude Code, OpenCode). Null disables agent badges.
    /// </summary>
    public AgentMonitor? AgentMonitor { get; set; }

    /// <summary>
    /// Called with the window brought to the foreground by a thumbnail click; MainWindow
    /// uses it to outline the window on its screen.
    /// </summary>
    public Action<IntPtr>? WindowActivated { get; set; }

    /// <summary>
    /// Layout mode: "fit" (thumbnails follow the panel size), "horizontal", "vertical", or "grid".
    /// </summary>
    public string Layout
    {
        get => _layout;
        set
        {
            if (SetProperty(ref _layout, value))
            {
                OnPropertyChanged(nameof(IsFitLayout));
                UpdateDisplaySize();
            }
        }
    }

    /// <summary>True in the "fit" layout, where the panel has a free size.</summary>
    public bool IsFitLayout => string.Equals(_layout, FitLayout, StringComparison.OrdinalIgnoreCase);

    /// <summary>Configured thumbnail width (Settings), used by the fixed-size layouts.</summary>
    public int ThumbnailWidth
    {
        get => _thumbnailWidth;
        set
        {
            if (SetProperty(ref _thumbnailWidth, value))
                UpdateDisplaySize();
        }
    }

    /// <summary>Configured thumbnail height (Settings), used by the fixed-size layouts.</summary>
    public int ThumbnailHeight
    {
        get => _thumbnailHeight;
        set
        {
            if (SetProperty(ref _thumbnailHeight, value))
                UpdateDisplaySize();
        }
    }

    /// <summary>
    /// Width at which thumbnails are drawn: computed from the panel size in the "fit" layout,
    /// <see cref="ThumbnailWidth"/> otherwise.
    /// </summary>
    public double DisplayThumbnailWidth
    {
        get => _displayThumbnailWidth;
        private set
        {
            if (SetProperty(ref _displayThumbnailWidth, value))
            {
                OnPropertyChanged(nameof(StatusRowScale));
                OnPropertyChanged(nameof(StatusRowWidth));
            }
        }
    }

    /// <summary>Height at which thumbnails are drawn (see <see cref="DisplayThumbnailWidth"/>).</summary>
    public double DisplayThumbnailHeight
    {
        get => _displayThumbnailHeight;
        private set => SetProperty(ref _displayThumbnailHeight, value);
    }

    /// <summary>
    /// Scale applied to the badge/title row under each thumbnail: its text grows with the
    /// thumbnail (see <see cref="ThumbnailLayoutCalculator.StatusRowScale"/>).
    /// </summary>
    public double StatusRowScale => ThumbnailLayoutCalculator.StatusRowScale(_displayThumbnailWidth);

    /// <summary>
    /// Width of the status row before scaling, so that once scaled it spans the thumbnail.
    /// </summary>
    public double StatusRowWidth => _displayThumbnailWidth / StatusRowScale;

    /// <summary>
    /// Size of the Preview area, reported by the view when it changes: the "fit" layout
    /// recomputes the thumbnail size from it.
    /// </summary>
    public void SetViewportSize(System.Windows.Size size)
    {
        lock (_lock)
            _viewportSize = size;
        UpdateDisplaySize();
    }

    /// <summary>
    /// Recomputes <see cref="DisplayThumbnailWidth"/>/<see cref="DisplayThumbnailHeight"/>.
    /// Called when the panel is resized, the layout or configured size changes, and after
    /// every scan (windows opened, closed or resized). Safe from any thread.
    /// </summary>
    internal void UpdateDisplaySize()
    {
        if (!IsFitLayout)
        {
            DisplayThumbnailWidth = _thumbnailWidth;
            DisplayThumbnailHeight = _thumbnailHeight;
            return;
        }

        System.Windows.Size viewport;
        List<int> groupSizes;
        double aspectRatio;
        lock (_lock)
        {
            viewport = _viewportSize;
            groupSizes = _windows.GroupBy(w => w.DisplayName).Select(g => g.Count()).ToList();
            aspectRatio = ThumbnailLayoutCalculator.TypicalAspectRatio(_windows.Select(w => w.AspectRatio));
        }

        // Before the first layout pass or without windows, keep the current size
        if (viewport.IsEmpty || ThumbnailLayoutCalculator.Fit(viewport, groupSizes, aspectRatio) is not { } size)
            return;

        DisplayThumbnailWidth = size.Width;
        DisplayThumbnailHeight = size.Height;
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

    /// <summary>
    /// Starts polling: an immediate first scan on a background thread, then one per interval.
    /// </summary>
    public void Start()
    {
        if (!_timer.IsEnabled)
        {
            _timer.Start();
            OnTimerTick(this, EventArgs.Empty); // first scan now, not after one interval
        }
    }

    /// <summary>Returns a copy of the tracked windows, safe to use from any thread.</summary>
    public TrackedWindow[] GetWindowsSnapshot()
    {
        lock (_lock)
            return [.. _windows];
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
    private void ActivateWindow(object? parameter)
    {
        if (parameter is TrackedWindow window)
            Activate(window);
    }

    /// <summary>
    /// Brings the window to the foreground, restoring it if minimized, then raises
    /// <see cref="WindowActivated"/> (red frame, side click). Used by thumbnail clicks, the
    /// "next agent" hotkey and notification clicks.
    /// </summary>
    public void Activate(TrackedWindow window)
    {
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

        WindowActivated?.Invoke(hwnd);
    }

    // ── Polling logic ───────────────────────────────────────────────────

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        // A scan stuck on a slow window must not be joined by a new one every second
        if (Interlocked.Exchange(ref _isScanning, 1) == 1)
            return;

        try
        {
            await Task.Run(Poll);
        }
        catch (Exception ex)
        {
            // Logged, not rethrown: on the dispatcher it would open an error dialog every second
            Log.Warning("Window scan failed", ex);
            return;
        }
        finally
        {
            Volatile.Write(ref _isScanning, 0);
        }

        StartLlmEvaluation();
    }

    /// <summary>
    /// Runs the LLM evaluation of all windows on a background thread. Screen captures and
    /// LLM calls (up to 30 s) stay off the UI thread and never delay the next scan; a new pass
    /// starts only once the previous one has finished.
    /// </summary>
    private void StartLlmEvaluation()
    {
        if (LlmService is not { } llmService || Interlocked.Exchange(ref _isEvaluating, 1) == 1)
            return;

        var windows = GetWindowsSnapshot();
        _ = Task.Run(async () =>
        {
            try
            {
                foreach (var window in windows)
                    await llmService.EvaluateAsync(window).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning("LLM evaluation pass failed", ex);
            }
            finally
            {
                Volatile.Write(ref _isEvaluating, 0);
            }
        });
    }

    private void Poll()
    {
        // Icons are only extracted for windows that do not have one yet
        HashSet<IntPtr> windowsWithIcon;
        lock (_lock)
        {
            windowsWithIcon = _windows.Where(w => w.Icon is not null).Select(w => w.Hwnd).ToHashSet();
        }

        var scanned = _enumerator.GetWindows(MonitoredApps, hwnd => !windowsWithIcon.Contains(hwnd));
        AttentionDetector?.PruneClosedWindows();

        var badges = AgentMonitor?.GetBadges(scanned);
        foreach (var window in scanned)
            window.Agent = badges?.GetValueOrDefault(window.Hwnd);

        MergeResults(scanned);
    }

    /// <summary>
    /// Applies a freshly scanned agent badge to a tracked window and updates its attention flag:
    /// a waiting agent highlights the window whenever it is not focused, and an agent going from
    /// busy to idle keeps it highlighted until the user focuses the window.
    /// </summary>
    /// <returns>
    /// The change worth telling the user about while they are elsewhere: a finished turn, or
    /// an agent starting to wait. An agent first seen already waiting (e.g. at startup) is not
    /// a change: notifying every waiting agent at launch would be noise.
    /// </returns>
    internal static AgentTransition ApplyAgentState(TrackedWindow window, AgentBadge? badge)
    {
        var previous = window.Agent?.Status;
        var current = badge?.Status;
        var justFinished = previous == AgentStatus.Busy && current == AgentStatus.Idle;

        window.Agent = badge;
        window.NeedsAgentAttention = !window.IsActive && current switch
        {
            AgentStatus.Waiting => true,
            AgentStatus.Idle => justFinished || window.NeedsAgentAttention,
            _ => false,
        };

        if (window.IsActive)
            return AgentTransition.None;
        if (justFinished)
            return AgentTransition.Finished;
        if (current == AgentStatus.Waiting && previous is not null and not AgentStatus.Waiting)
            return AgentTransition.NeedsInput;
        return AgentTransition.None;
    }

    /// <summary>
    /// Raised after a scan for each window whose agent finished or started waiting while the
    /// user was elsewhere. Raised on the polling thread.
    /// </summary>
    public event Action<TrackedWindow, AgentTransition>? AgentNeedsAttention;

    /// <summary>
    /// Picks the window to visit next among those whose agent needs the user: waiting agents
    /// first, then turns finished but not seen yet, then idle agents; each group in panel order.
    /// Starting after <paramref name="current"/> in that order, so repeated calls cycle through
    /// them. Null when no agent needs the user.
    /// </summary>
    internal TrackedWindow? FindNextAgentWindow(IntPtr current)
    {
        List<TrackedWindow> candidates;
        lock (_lock)
        {
            candidates = _windows
                .Select((window, index) => (window, index, rank: AgentUrgencyRank(window)))
                .Where(c => c.rank >= 0)
                .OrderBy(c => c.rank)
                .ThenBy(c => c.index)
                .Select(c => c.window)
                .ToList();
        }

        if (candidates.Count == 0)
            return null;

        var position = candidates.FindIndex(w => w.Hwnd == current);
        return candidates[(position + 1) % candidates.Count];
    }

    /// <summary>0 waiting, 1 finished and unseen, 2 idle; -1 when the agent does not need the user.</summary>
    private static int AgentUrgencyRank(TrackedWindow window) => window.Agent?.Status switch
    {
        AgentStatus.Waiting => 0,
        AgentStatus.Idle when window.NeedsAgentAttention => 1,
        AgentStatus.Idle => 2,
        _ => -1,
    };

    /// <summary>
    /// Diffs scanned windows against the current collection:
    /// - Add new windows
    /// - Remove closed windows
    /// - Update properties of existing windows
    /// </summary>
    internal void MergeResults(List<TrackedWindow> scanned)
    {
        // Raised once the lock is released: handlers may read the window list
        var transitions = new List<(TrackedWindow Window, AgentTransition Transition)>();

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
                    // Window handles are reused: a new window must not inherit this one's flash
                    AttentionDetector?.ClearFlashing(_windows[i].Hwnd);
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
                    // A minimized window reports no ratio: keep the last known one
                    if (scannedWindow.AspectRatio > 0)
                        existing.AspectRatio = scannedWindow.AspectRatio;
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

                    // After IsActive, which clears the "finished, not yet seen" highlight
                    var transition = ApplyAgentState(existing, scannedWindow.Agent);
                    if (transition != AgentTransition.None)
                        transitions.Add((existing, transition));
                }
                else
                {
                    // No previous state: only a waiting agent can need attention
                    scannedWindow.NeedsAgentAttention = !scannedWindow.IsActive
                        && scannedWindow.Agent?.Status == AgentStatus.Waiting;
                    _windows.Add(scannedWindow);
                }
            }
        }

        // Windows opened, closed or resized change the best "fit" size
        UpdateDisplaySize();

        foreach (var (window, transition) in transitions)
            AgentNeedsAttention?.Invoke(window, transition);
    }
}
