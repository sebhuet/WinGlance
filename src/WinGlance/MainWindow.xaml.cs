using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WinGlance.Models;
using WinGlance.Services;
using WinGlance.ViewModels;

namespace WinGlance;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

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
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _viewModel.PreviewViewModel.SetDestinationHwnd(hwnd);
        _viewModel.PreviewViewModel.Start();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Persist panel position if configured
        var config = _viewModel.Config;
        if (config.RememberPosition)
        {
            config.PanelX = Left;
            config.PanelY = Top;
        }

        _viewModel.ConfigService.Save(config);
        _viewModel.PreviewViewModel.Dispose();
    }

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
