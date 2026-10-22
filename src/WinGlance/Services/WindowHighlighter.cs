using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WinGlance.NativeApi;

namespace WinGlance.Services;

/// <summary>
/// Outlines a window with a blinking red frame for about a second, so that the eye finds it
/// after a switch from a thumbnail, whatever screen it is on. The frame is a click-through,
/// never-activated topmost window laid over the target's visible bounds. UI thread only.
/// </summary>
internal sealed class WindowHighlighter : IDisposable
{
    internal const int Blinks = 3;

    /// <summary>Time between two on/off steps of the frame.</summary>
    internal static readonly TimeSpan BlinkInterval = TimeSpan.FromMilliseconds(180);

    private const double FrameThickness = 6;
    private static readonly Brush FrameBrush = CreateFrameBrush();

    private readonly DispatcherTimer _timer;
    private Window? _overlay;
    private Border? _frame;
    private IntPtr _overlayHwnd;
    private IntPtr _target;
    private int _step;

    /// <summary>Creates the highlighter; no window is created until one is outlined.</summary>
    public WindowHighlighter()
    {
        _timer = new DispatcherTimer { Interval = BlinkInterval };
        _timer.Tick += (_, _) => Step();
    }

    /// <summary>True while a window is being outlined.</summary>
    public bool IsFlashing => _timer.IsEnabled;

    /// <summary>
    /// Creates and shows the overlay off-screen, empty. Showing a transparent WPF window takes
    /// a few hundred milliseconds the first time: done at startup, the first flash starts at once.
    /// </summary>
    public void Prepare()
    {
        if (_overlay is not null)
            return;

        EnsureOverlay();
        _frame!.Visibility = Visibility.Hidden;
        _overlay!.Show();
    }

    /// <summary>
    /// Starts outlining the window; a flash already running moves to this window.
    /// </summary>
    public void Flash(IntPtr hwnd)
    {
        _target = hwnd;
        _step = 0;
        _timer.Stop();
        _timer.Start();
        Step();
    }

    /// <summary>
    /// Frame visible on even steps, hidden on odd ones: <see cref="Blinks"/> blinks in
    /// 2 × <see cref="Blinks"/> steps.
    /// </summary>
    internal static bool IsFrameShownAt(int step) => step % 2 == 0;

    public void Dispose()
    {
        _timer.Stop();
        _overlay?.Close();
        _overlay = null;
    }

    private void Step()
    {
        if (_step >= Blinks * 2 || !NativeMethods.IsWindow(_target))
        {
            Stop();
            return;
        }

        var shown = IsFrameShownAt(_step++);

        // A minimized window is still being restored: skip this step, it moves into place
        if (!shown || NativeMethods.IsIconic(_target) || !NativeMethods.TryGetVisibleBounds(_target, out var bounds))
        {
            if (_frame is not null)
                _frame.Visibility = Visibility.Hidden;
            return;
        }

        Prepare();

        // Bounds re-read at every step: the window may still be restoring or moving. Applied twice:
        // moving onto a monitor with another DPI makes WPF resize the window to its own suggestion.
        for (var i = 0; i < 2; i++)
        {
            NativeMethods.SetWindowPos(_overlayHwnd, NativeMethods.HWND_TOPMOST, bounds.Left, bounds.Top,
                bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, NativeMethods.SWP_NOACTIVATE);
        }

        _frame!.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Ends the flash. The overlay stays shown, empty and off-screen, ready for the next one.
    /// </summary>
    private void Stop()
    {
        _timer.Stop();
        if (_overlay is null)
            return;

        _frame!.Visibility = Visibility.Hidden;
        NativeMethods.SetWindowPos(_overlayHwnd, NativeMethods.HWND_TOPMOST, -32000, -32000, 1, 1, NativeMethods.SWP_NOACTIVATE);
    }

    private void EnsureOverlay()
    {
        if (_overlay is not null)
            return;

        _frame = new Border { BorderBrush = FrameBrush, BorderThickness = new Thickness(FrameThickness) };
        _overlay = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -32000,
            Top = -32000,
            Width = 1,
            Height = 1,
            Content = _frame,
        };

        // Click-through, never activated, out of Alt+Tab: the frame must not get in the way
        _overlayHwnd = new WindowInteropHelper(_overlay).EnsureHandle();
        var exStyle = NativeMethods.GetWindowLong(_overlayHwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(_overlayHwnd, NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED
            | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);
    }

    private static Brush CreateFrameBrush()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23));
        brush.Freeze();
        return brush;
    }
}
