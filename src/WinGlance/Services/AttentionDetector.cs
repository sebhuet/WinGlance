using System.Windows.Interop;
using WinGlance.NativeApi;

namespace WinGlance.Services;

/// <summary>
/// Detects taskbar flashing (HSHELL_FLASH) via shell hook registration.
/// Tracks which window handles are currently flashing.
/// </summary>
internal sealed class AttentionDetector : IDisposable
{
    private IntPtr _hwnd;
    private HwndSource? _hwndSource;
    private uint _shellHookMessage;
    private readonly HashSet<IntPtr> _flashingWindows = [];
    private readonly object _lock = new();

    /// <summary>
    /// Initializes the detector: registers the shell hook window and hooks WndProc.
    /// </summary>
    public void Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);

        _shellHookMessage = NativeMethods.RegisterWindowMessage("SHELLHOOK");
        NativeMethods.RegisterShellHookWindow(hwnd);
    }

    /// <summary>
    /// Returns true if the given window handle is currently flashing.
    /// </summary>
    public bool IsFlashing(IntPtr hwnd)
    {
        lock (_lock)
            return _flashingWindows.Contains(hwnd);
    }

    /// <summary>
    /// Clears the flashing state for the given window (e.g., when it gains focus).
    /// </summary>
    public void ClearFlashing(IntPtr hwnd)
    {
        lock (_lock)
            _flashingWindows.Remove(hwnd);
    }

    /// <summary>
    /// Returns a snapshot of all currently flashing window handles.
    /// </summary>
    public HashSet<IntPtr> GetFlashingWindows()
    {
        lock (_lock)
            return new HashSet<IntPtr>(_flashingWindows);
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
            NativeMethods.DeregisterShellHookWindow(_hwnd);
        _hwndSource?.RemoveHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == _shellHookMessage && wParam.ToInt32() == NativeMethods.HSHELL_FLASH)
        {
            lock (_lock)
                _flashingWindows.Add(lParam);
        }

        return IntPtr.Zero;
    }
}
