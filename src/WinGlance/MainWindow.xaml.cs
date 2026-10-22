using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Hardcodet.Wpf.TaskbarNotification;
using WinGlance.Models;
using WinGlance.NativeApi;
using WinGlance.Services;
using WinGlance.ViewModels;

namespace WinGlance;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly TaskbarIcon _trayIcon;
    private readonly HotkeyService _hotkeyService = new();
    private readonly AttentionDetector _attentionDetector = new();
    private readonly uint _showPanelMessage = NativeMethods.RegisterWindowMessage(App.ShowPanelMessageName);
    private HwndSource? _hwndSource;
    private bool _isExiting;

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
            Icon = LoadApplicationIcon(),
            ToolTipText = "WinGlance",
            ContextMenu = CreateTrayContextMenu(),
        };
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowPanel();

        Loaded += OnLoaded;
        Closing += OnClosing;
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

        // Attention detection (shell hook for flashing)
        _attentionDetector.Initialize(hwnd);
        _viewModel.PreviewViewModel.AttentionDetector = _attentionDetector;

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
        _hotkeyService.Dispose();
        _hwndSource?.RemoveHook(WndProc);
        _trayIcon.Dispose();
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
        var menu = new System.Windows.Controls.ContextMenu();

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
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
