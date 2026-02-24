using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Hardcodet.Wpf.TaskbarNotification;
using WinGlance.Models;
using WinGlance.Services;
using WinGlance.ViewModels;

namespace WinGlance;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly TaskbarIcon _trayIcon;
    private readonly HotkeyService _hotkeyService = new();
    private readonly AttentionDetector _attentionDetector = new();
    private bool _isExiting;

    internal MainWindow(ConfigService configService, AppConfig config)
    {
        InitializeComponent();
        _viewModel = new MainViewModel(configService, config, opacity => Opacity = opacity);
        DataContext = _viewModel;

        // Restore saved panel position
        if (config.RememberPosition)
        {
            Left = config.PanelX;
            Top = config.PanelY;
        }

        Opacity = config.PanelOpacity;

        // Auto-size to content
        SizeToContent = SizeToContent.WidthAndHeight;

        // System tray icon
        _trayIcon = new TaskbarIcon
        {
            Icon = SystemIcons.Application,
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

        // Register global hotkey
        _hotkeyService.Initialize(hwnd);
        _hotkeyService.HotkeyPressed += (_, _) => TogglePanel();
        _hotkeyService.Register(_viewModel.Config.Hotkey);

        // Attention detection (shell hook for flashing)
        _attentionDetector.Initialize(hwnd);
        _viewModel.PreviewViewModel.AttentionDetector = _attentionDetector;

        // LLM analysis service
        if (_viewModel.Config.Llm.Enabled)
        {
            var llmService = new LlmService(_viewModel.Config.Llm);
            llmService.DebugLogger = _viewModel.PromptEditorViewModel;
            _viewModel.PreviewViewModel.LlmService = llmService;
        }

        // Allow settings to navigate to prompt editor
        _viewModel.SettingsViewModel.NavigateToPromptEditor = () => SwitchToView(3);
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExiting && _viewModel.Config.CloseToTray)
        {
            e.Cancel = true;
            HidePanel();
            return;
        }

        // Persist panel position if configured
        var config = _viewModel.Config;
        if (config.RememberPosition)
        {
            config.PanelX = Left;
            config.PanelY = Top;
        }

        _viewModel.ConfigService.Save(config);
        _viewModel.PreviewViewModel.Dispose();
        _attentionDetector.Dispose();
        _hotkeyService.Dispose();
        _trayIcon.Dispose();
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
        var fadeIn = new DoubleAnimation(0, _viewModel.Config.PanelOpacity, TimeSpan.FromMilliseconds(150));
        BeginAnimation(OpacityProperty, fadeIn);
    }

    private void HidePanel()
    {
        var fadeOut = new DoubleAnimation(_viewModel.Config.PanelOpacity, 0, TimeSpan.FromMilliseconds(150));
        fadeOut.Completed += (_, _) => Hide();
        BeginAnimation(OpacityProperty, fadeOut);
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Close();
    }

    // ── View switching ───────────────────────────────────────────

    private void SwitchToView(int index)
    {
        _viewModel.SelectedTabIndex = index;
        PreviewView.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        ApplicationsView.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        SettingsView.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        PromptEditorView.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;

        // DWM thumbnails are rendered by the OS compositor and ignore WPF Visibility.
        // We must unregister them when leaving Preview; they re-register on the next poll.
        if (index == 0)
        {
            _viewModel.PreviewViewModel.Start();
        }
        else
        {
            _viewModel.PreviewViewModel.Stop();
            _viewModel.PreviewViewModel.ThumbnailManager.UnregisterAll();
        }

        // Re-measure so the window auto-sizes to the new content
        InvalidateMeasure();
        UpdateLayout();
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
