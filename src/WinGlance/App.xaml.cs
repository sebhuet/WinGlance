using System.IO;
using System.Windows;
using System.Windows.Threading;
using WinGlance.NativeApi;
using WinGlance.Services;

namespace WinGlance;

public partial class App : Application
{
    /// <summary>
    /// Window message a second instance broadcasts to make the running one show its panel.
    /// </summary>
    internal const string ShowPanelMessageName = "WinGlance_ShowPanel";

    private Mutex? _mutex;
    private bool _ownsMutex;
    private ThemeService? _themeService;
    private int _isShowingError;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        Log.Initialize(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinGlance"));

        // Single-instance enforcement via named Mutex
        _mutex = new Mutex(true, "WinGlance_SingleInstance_Mutex", out _ownsMutex);
        if (!_ownsMutex)
        {
            // Bring up the running instance's panel instead of showing a dialog
            NativeMethods.PostMessage(
                NativeMethods.HWND_BROADCAST,
                NativeMethods.RegisterWindowMessage(ShowPanelMessageName),
                IntPtr.Zero,
                IntPtr.Zero);
            Shutdown();
            return;
        }

        // DWM composition check (Phase 13)
        if (!DwmCompositionCheck())
        {
            Shutdown();
            return;
        }

        try
        {
            _themeService = new ThemeService();
            _themeService.Initialize();

            var configService = new ConfigService();
            var config = configService.Load();
            Log.Info($"Started, configuration at {configService.ConfigPath}");

            // The registry is the source of truth for "Start with Windows"
            AutoStartService.RepairIfStale();
            config.AutoStart = AutoStartService.IsAutoStartEnabled();

            var mainWindow = new MainWindow(configService, config);
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            // Without this, a failure here left a windowless process holding the mutex,
            // and every later launch only said "already running"
            Log.Error("Startup failed", ex);
            MessageBox.Show(
                $"WinGlance could not start:\n\n{ex.Message}\n\nDetails: {Log.FilePath}",
                "WinGlance — Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        SessionEnding += (_, _) => (MainWindow as MainWindow)?.PersistState();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _themeService?.Dispose();
        // ReleaseMutex throws when this instance does not own the mutex (second instance)
        if (_ownsMutex)
            _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private static bool DwmCompositionCheck()
    {
        var hr = NativeMethods.DwmIsCompositionEnabled(out var enabled);
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
        Log.Error("Unhandled exception on the UI thread", e.Exception);
        e.Handled = true;

        // A repeating failure must not stack one modal dialog per occurrence
        if (Interlocked.Exchange(ref _isShowingError, 1) == 1)
            return;

        try
        {
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nDetails: {Log.FilePath}",
                "WinGlance — Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Volatile.Write(ref _isShowingError, 0);
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Error("Fatal unhandled exception", ex);
            MessageBox.Show(
                $"A fatal error occurred:\n\n{ex.Message}\n\nDetails: {Log.FilePath}",
                "WinGlance — Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Warning("Unobserved background task exception", e.Exception);
        e.SetObserved();
    }
}
