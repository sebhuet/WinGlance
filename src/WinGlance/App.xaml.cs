using System.Windows;
using System.Windows.Threading;
using WinGlance.Services;

namespace WinGlance;

public partial class App : Application
{
    private Mutex? _mutex;
    private ThemeService? _themeService;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // Single-instance enforcement via named Mutex
        _mutex = new Mutex(true, "WinGlance_SingleInstance_Mutex", out var isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show(
                "WinGlance is already running.",
                "WinGlance",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // DWM composition check (Phase 13)
        if (!DwmCompositionCheck())
        {
            Shutdown();
            return;
        }

        // Theme detection
        _themeService = new ThemeService();
        _themeService.Initialize();

        var configService = new ConfigService();
        var config = configService.Load();

        var mainWindow = new MainWindow(configService, config);
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _themeService?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private static bool DwmCompositionCheck()
    {
        var hr = NativeApi.NativeMethods.DwmIsCompositionEnabled(out var enabled);
        if (hr != 0 || !enabled)
        {
            MessageBox.Show(
                "DWM desktop composition is not enabled.\nWinGlance requires DWM to display live window thumbnails.",
                "WinGlance — DWM Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}",
            "WinGlance — Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show(
                $"A fatal error occurred:\n\n{ex.Message}",
                "WinGlance — Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
    }
}
