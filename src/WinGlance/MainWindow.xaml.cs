using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Hardcodet.Wpf.TaskbarNotification;
using WinGlance.Models;
using WinGlance.NativeApi;
using WinGlance.Services;
using WinGlance.ViewModels;

namespace WinGlance;

/// <summary>Floating panel: hosts the Preview, Applications, Settings and Prompt Editor views, the tray icon, the global hotkeys and the window switching.</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly TaskbarIcon _trayIcon;
    private readonly Icon _applicationIcon = LoadApplicationIcon();
    private Icon? _badgeIcon;
    private readonly HotkeyService _hotkeyService = new();
    private readonly HotkeyService _nextAgentHotkeyService = new(hotkeyId: 2);

    // Window of the last agent notification: clicking the notification switches to it
    private TrackedWindow? _lastNotifiedWindow;
    private readonly AttentionDetector _attentionDetector = new();
    private readonly WindowHighlighter _windowHighlighter = new();
    private readonly StereoClick _stereoClick = new();
    private readonly uint _showPanelMessage = NativeMethods.RegisterWindowMessage(App.ShowPanelMessageName);
    private HwndSource? _hwndSource;
    private bool _isExiting;

    /// <summary>
    /// Builds the panel from the saved configuration: restores the saved position when its title bar is still reachable on the current monitors, applies the saved size and opacity, and starts the services.
    /// </summary>
    /// <param name="configService">Persists the configuration when settings change or the panel is hidden.</param>
    /// <param name="config">The loaded configuration, shared with the ViewModels.</param>
    internal MainWindow(ConfigService configService, AppConfig config)
    {
        InitializeComponent();
        _viewModel = new MainViewModel(configService, config, opacity => Opacity = opacity);
        DataContext = _viewModel;

        // Restore the saved position only if the title bar would be reachable: the monitor it
        // was on may have been disconnected since
        var virtualScreen = new Rect(
            SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (MainViewModel.GetRestorablePosition(config, virtualScreen) is { } position)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = position.X;
            Top = position.Y;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        Opacity = config.PanelOpacity;

        // "fit": the user sizes the panel and thumbnails follow; other layouts size to content
        Width = Math.Min(config.PanelWidth, virtualScreen.Width);
        Height = Math.Min(config.PanelHeight, virtualScreen.Height);
        ApplySizingMode();
        _viewModel.PreviewViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PreviewViewModel.IsFitLayout))
                ApplySizingMode();
        };

        // System tray icon
        _trayIcon = new TaskbarIcon
        {
            Icon = _applicationIcon,
            ToolTipText = "WinGlance",
            ContextMenu = CreateTrayContextMenu(),
        };
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowPanel();
        _trayIcon.TrayBalloonTipClicked += (_, _) => ActivateNotifiedWindow();

        // Counter of the agents that need the user, on the tray icon (raised on the polling thread)
        _viewModel.PreviewViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PreviewViewModel.AgentsWaiting) or nameof(PreviewViewModel.AgentsFinished))
                Dispatcher.BeginInvoke(UpdateTrayCounter);
        };

        Loaded += OnLoaded;
        Closing += OnClosing;
        StateChanged += OnStateChanged;
        KeyDown += OnKeyDown;

        // Options panel: under the title bar, right-aligned with the panel, so it stays inside it
        OptionsPopup.CustomPopupPlacementCallback = (popupSize, titleBarSize, _) =>
            [new CustomPopupPlacement(new System.Windows.Point(titleBarSize.Width - popupSize.Width - 4, titleBarSize.Height), PopupPrimaryAxis.Horizontal)];
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _viewModel.PreviewViewModel.SetDestinationHwnd(hwnd);
        _viewModel.PreviewViewModel.Start();

        // A second instance broadcasts _showPanelMessage instead of starting
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);

        // Register global hotkey; Settings re-registers it on Save
        _hotkeyService.Initialize(hwnd);
        _hotkeyService.HotkeyPressed += (_, _) => TogglePanel();
        _viewModel.SettingsViewModel.ApplyHotkey = _hotkeyService.Register;
        var hotkeyRegistered = _hotkeyService.Register(_viewModel.Config.Hotkey);
        if (!hotkeyRegistered)
            Log.Warning($"Global hotkey '{_viewModel.Config.Hotkey}' could not be registered");
        _viewModel.SettingsViewModel.ReportHotkeyResult(hotkeyRegistered);

        // "Next agent" hotkey: jumps to the next window whose agent needs the user
        _nextAgentHotkeyService.Initialize(hwnd);
        _nextAgentHotkeyService.HotkeyPressed += (_, _) => GoToNextAgent();
        _viewModel.SettingsViewModel.ApplyNextAgentHotkey = RegisterNextAgentHotkey;
        var nextAgentRegistered = RegisterNextAgentHotkey(_viewModel.Config.NextAgentHotkey);
        if (!nextAgentRegistered)
            Log.Warning($"Next agent hotkey '{_viewModel.Config.NextAgentHotkey}' could not be registered");
        _viewModel.SettingsViewModel.ReportNextAgentHotkeyResult(nextAgentRegistered);

        // Agent finished or waiting while the user is elsewhere: Windows notification
        _viewModel.PreviewViewModel.AgentNeedsAttention += (window, transition) =>
            Dispatcher.BeginInvoke(() => NotifyAgent(window, transition)); // raised on the polling thread

        // Attention detection (shell hook for flashing)
        _attentionDetector.Initialize(hwnd);
        _viewModel.PreviewViewModel.AttentionDetector = _attentionDetector;

        // Outline the window a thumbnail click switched to (the checkbox applies without saving)
        _windowHighlighter.Prepare();
        _viewModel.PreviewViewModel.WindowActivated = target =>
        {
            // A full-screen panel stays on top of the window just switched to: step aside
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;

            if (_viewModel.SettingsViewModel.HighlightOnSwitch)
                _windowHighlighter.Flash(target);

            // Heard from the side of the window's monitor: left, center or right
            if (_viewModel.SettingsViewModel.ClickOnSwitch)
                _stereoClick.Play(ScreenSide.PanOfWindow(target));
        };

        // Allow settings to navigate to prompt editor
        _viewModel.SettingsViewModel.NavigateToPromptEditor = () => SwitchToView(3);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg == _showPanelMessage)
        {
            ShowPanel();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExiting && _viewModel.Config.CloseToTray)
        {
            e.Cancel = true;
            HidePanel();
            return;
        }

        PersistState();
        _viewModel.PreviewViewModel.Dispose();
        _attentionDetector.Dispose();
        _windowHighlighter.Dispose();
        _stereoClick.Dispose();
        _hotkeyService.Dispose();
        _nextAgentHotkeyService.Dispose();
        _hwndSource?.RemoveHook(WndProc);
        _trayIcon.Dispose();
        _badgeIcon?.Dispose();
    }

    /// <summary>
    /// Saves the panel position (if enabled) with the configuration. Called on exit, when the
    /// panel is hidden to the tray and when Windows ends the session: WPF raises no Closing
    /// event on logoff, and with close-to-tray the user may never pick Exit.
    /// </summary>
    internal void PersistState()
    {
        var config = _viewModel.Config;
        if (config.RememberPosition && WindowState == WindowState.Normal)
        {
            config.PanelX = Left;
            config.PanelY = Top;
        }

        // Only the "fit" layout has a user-chosen size; the others size to their content
        if (_viewModel.PreviewViewModel.IsFitLayout && WindowState == WindowState.Normal && ActualWidth > 0)
        {
            config.PanelWidth = ActualWidth;
            config.PanelHeight = ActualHeight;
        }

        try
        {
            _viewModel.ConfigService.Save(config);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Must not prevent the cleanup that follows on exit
            Log.Warning("Could not save the configuration", ex);
        }
    }

    // ── Panel visibility ─────────────────────────────────────────────

    /// <summary>Hides the panel when it is visible, shows it otherwise (tray click, hotkey, second instance).</summary>
    internal void TogglePanel()
    {
        if (IsVisible)
            HidePanel();
        else
            ShowPanel();
    }

    private void ShowPanel()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();

        // The live slider value, not the last saved one. FillBehavior.Stop hands the property
        // back to its local value once the fade ends: a held animation would freeze the
        // opacity and ignore the Settings slider from then on.
        var opacity = _viewModel.SettingsViewModel.PanelOpacity;
        Opacity = opacity;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, opacity, TimeSpan.FromMilliseconds(150))
        {
            FillBehavior = FillBehavior.Stop,
        });
    }

    private void HidePanel()
    {
        PersistState();

        var fadeOut = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(150));
        fadeOut.Completed += (_, _) =>
        {
            Hide();
            BeginAnimation(OpacityProperty, null); // release the property for the next show
        };
        BeginAnimation(OpacityProperty, fadeOut);
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Close();
    }

    /// <summary>The executable's icon (Assets/icon.ico), or the generic one if unavailable.</summary>
    private static Icon LoadApplicationIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } exePath && System.Drawing.Icon.ExtractAssociatedIcon(exePath) is { } icon)
                return icon;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            Log.Warning("Could not load the application icon", ex);
        }

        return SystemIcons.Application;
    }

    // ── View switching ───────────────────────────────────────────

    private void SwitchToView(int index)
    {
        _viewModel.SelectedTabIndex = index;
        PreviewView.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        ApplicationsView.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        SettingsView.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        PromptEditorView.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;

        // DWM thumbnails are rendered by the OS compositor and ignore WPF Visibility:
        // unregister them when leaving Preview; they re-register when it becomes visible.
        // Polling keeps running so that attention, agent and LLM states stay current
        // (the Prompt Editor's debug log shows live analyses).
        if (index != 0)
            _viewModel.PreviewViewModel.ThumbnailManager.UnregisterAll();

        // The list is empty until scanned: scan whenever the view opens
        if (index == 1)
            _viewModel.ApplicationsViewModel.Refresh();

        // Force SizeToContent recalculation (WPF resets it to Manual after user resize);
        // the "fit" layout keeps the size the user chose
        if (!_viewModel.PreviewViewModel.IsFitLayout)
        {
            SizeToContent = SizeToContent.Manual;
            SizeToContent = SizeToContent.WidthAndHeight;
        }
    }

    /// <summary>
    /// "fit" layout: free panel size (thumbnails follow it). Other layouts: the panel sizes
    /// itself to the thumbnails.
    /// </summary>
    private void ApplySizingMode()
    {
        if (_viewModel.PreviewViewModel.IsFitLayout)
        {
            SizeToContent = SizeToContent.Manual;
            MinWidth = 320;
            MinHeight = 220;
        }
        else
        {
            MinWidth = 0;
            MinHeight = 0;
            SizeToContent = SizeToContent.WidthAndHeight;
        }
    }

    // ── Right-click context menu ──────────────────────────────────

    private void Border_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Placed on the panel so that the menu takes the DPI of the panel's monitor: without
        // a placement target, WPF scales the popup for the primary monitor
        var menu = new System.Windows.Controls.ContextMenu
        {
            PlacementTarget = (UIElement)sender,
            Placement = PlacementMode.MousePoint,
        };

        var preview = new System.Windows.Controls.MenuItem { Header = "Preview" };
        preview.Click += (_, _) => SwitchToView(0);
        menu.Items.Add(preview);

        var apps = new System.Windows.Controls.MenuItem { Header = "Applications" };
        apps.Click += (_, _) => SwitchToView(1);
        menu.Items.Add(apps);

        var settings = new System.Windows.Controls.MenuItem { Header = "Settings" };
        settings.Click += (_, _) => SwitchToView(2);
        menu.Items.Add(settings);

        var prompt = new System.Windows.Controls.MenuItem { Header = "Prompt Editor" };
        prompt.Click += (_, _) => SwitchToView(3);
        menu.Items.Add(prompt);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exit = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exit.Click += (_, _) => ExitApplication();
        menu.Items.Add(exit);

        menu.IsOpen = true;
        e.Handled = true;
    }

    // ── Tray context menu ────────────────────────────────────────

    private System.Windows.Controls.ContextMenu CreateTrayContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var showHide = new System.Windows.Controls.MenuItem { Header = "Show / Hide" };
        showHide.Click += (_, _) => TogglePanel();
        menu.Items.Add(showHide);

        var settings = new System.Windows.Controls.MenuItem { Header = "Settings" };
        settings.Click += (_, _) =>
        {
            ShowPanel();
            SwitchToView(2);
        };
        menu.Items.Add(settings);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exit = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exit.Click += (_, _) => ExitApplication();
        menu.Items.Add(exit);

        return menu;
    }

    // ── Title bar handlers ───────────────────────────────────────

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            ToggleFullScreen(); // as a double-click on a standard title bar
        else if (e.ClickCount == 1 && WindowState == WindowState.Normal)
            DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    // ── Agents: next agent hotkey and notifications ───────────────

    /// <summary>Registers the "next agent" hotkey; an empty string removes it.</summary>
    private bool RegisterNextAgentHotkey(string hotkey)
    {
        if (!string.IsNullOrWhiteSpace(hotkey))
            return _nextAgentHotkeyService.Register(hotkey);

        _nextAgentHotkeyService.Unregister();
        return true;
    }

    /// <summary>
    /// Switches to the next window whose agent needs the user (waiting, then finished, then
    /// idle), after the window currently in front; repeated presses cycle through them.
    /// </summary>
    private void GoToNextAgent()
    {
        var target = _viewModel.PreviewViewModel.FindNextAgentWindow(NativeMethods.GetForegroundWindow());
        if (target is not null)
            _viewModel.PreviewViewModel.Activate(target);
    }

    /// <summary>
    /// Shows a Windows notification (through the tray icon) for an agent that finished or
    /// waits, unless notifications are off or the user is already in that window.
    /// </summary>
    private void NotifyAgent(TrackedWindow window, AgentTransition transition)
    {
        if (!_viewModel.SettingsViewModel.AgentNotifications
            || NativeMethods.GetForegroundWindow() == window.Hwnd)
            return;

        var (title, message) = AgentNotification.Describe(window, transition);
        _lastNotifiedWindow = window;
        _trayIcon.ShowBalloonTip(title, message, BalloonIcon.Info);
        Log.Info($"Notified: {title}");
    }

    /// <summary>Draws the number of agents that need the user on the tray icon, and lists them in its tooltip.</summary>
    private void UpdateTrayCounter()
    {
        var waiting = _viewModel.PreviewViewModel.AgentsWaiting;
        var finished = _viewModel.PreviewViewModel.AgentsFinished;

        var previous = _badgeIcon;
        _badgeIcon = waiting + finished > 0 ? TrayBadge.Create(_applicationIcon, waiting, finished) : null;
        _trayIcon.Icon = _badgeIcon ?? _applicationIcon;
        _trayIcon.ToolTipText = TrayBadge.Describe(waiting, finished);
        previous?.Dispose();
    }

    private void ActivateNotifiedWindow()
    {
        if (_lastNotifiedWindow is { } window && NativeMethods.IsWindow(window.Hwnd))
            _viewModel.PreviewViewModel.Activate(window);
    }

    // ── Options panel ────────────────────────────────────────────

    private void OptionsButton_Click(object sender, RoutedEventArgs e)
    {
        OptionsPopup.IsOpen = !OptionsPopup.IsOpen;
    }

    private void OptionsPopup_Closed(object? sender, EventArgs e)
    {
        // Applied live while open; kept once the panel closes
        try
        {
            _viewModel.SettingsViewModel.SaveQuickOptions();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning("Could not save the Options panel settings", ex);
        }
    }

    private void AllSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        OptionsPopup.IsOpen = false;
        SwitchToView(2);
    }

    // ── Full screen ──────────────────────────────────────────────

    private void FullScreenButton_Click(object sender, RoutedEventArgs e) => ToggleFullScreen();

    /// <summary>
    /// Full screen is the maximized state: a borderless window then covers its whole monitor,
    /// and in the "fit" layout the thumbnails grow to fill it.
    /// </summary>
    private void ToggleFullScreen()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        var isFullScreen = WindowState == WindowState.Maximized;
        FullScreenButton.Content = isFullScreen ? "❐" : "□";
        FullScreenButton.ToolTip = isFullScreen ? "Exit full screen (F11, Esc)" : "Full screen (F11)";

        // A maximized resizable window keeps its invisible resize frame, which overhangs the
        // monitor (7 px at 100 %): pull the content back onto the screen
        RootBorder.Margin = isFullScreen ? GetMonitorOverhang() : new Thickness(0);

        // Maximizing turns SizeToContent off: the fixed-size layouts must get it back
        if (WindowState == WindowState.Normal)
            ApplySizingMode();
    }

    /// <summary>
    /// How far the window extends past its monitor on each side, in device-independent pixels.
    /// </summary>
    private Thickness GetMonitorOverhang()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!NativeMethods.GetWindowRect(hwnd, out var window) || !NativeMethods.TryGetMonitorBounds(hwnd, out var monitor))
            return new Thickness(0);

        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        return new Thickness(
            Math.Max(0, monitor.Left - window.Left) / dpi.DpiScaleX,
            Math.Max(0, monitor.Top - window.Top) / dpi.DpiScaleY,
            Math.Max(0, window.Right - monitor.Right) / dpi.DpiScaleX,
            Math.Max(0, window.Bottom - monitor.Bottom) / dpi.DpiScaleY);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => SwitchToView(0);

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // Esc leaves the open view first, then full screen
        if (e.Key == Key.Escape && _viewModel.IsSecondaryView)
        {
            SwitchToView(0);
            e.Handled = true;
        }
        else if (e.Key == Key.F11 || (e.Key == Key.Escape && WindowState == WindowState.Maximized))
        {
            ToggleFullScreen();
            e.Handled = true;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
