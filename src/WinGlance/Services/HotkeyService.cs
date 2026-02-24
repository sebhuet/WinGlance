using System.Windows.Input;
using System.Windows.Interop;
using WinGlance.NativeApi;

namespace WinGlance.Services;

/// <summary>
/// Registers and manages a global hotkey using the Win32 RegisterHotKey API.
/// Hooks into the window message loop via <see cref="HwndSource"/> to receive WM_HOTKEY.
/// </summary>
internal sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x0001;

    private IntPtr _hwnd;
    private HwndSource? _hwndSource;
    private bool _isRegistered;

    /// <summary>Raised when the registered global hotkey is pressed.</summary>
    public event EventHandler? HotkeyPressed;

    /// <summary>
    /// Initializes the service with the window handle to receive WM_HOTKEY messages.
    /// </summary>
    public void Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);
    }

    /// <summary>
    /// Parses a hotkey string (e.g., "Ctrl+Alt+G") and registers it globally.
    /// </summary>
    public bool Register(string hotkeyString)
    {
        Unregister();

        if (!TryParseHotkey(hotkeyString, out var modifiers, out var vk))
            return false;

        _isRegistered = NativeMethods.RegisterHotKey(_hwnd, HotkeyId, modifiers, vk);
        return _isRegistered;
    }

    /// <summary>
    /// Unregisters the current global hotkey.
    /// </summary>
    public void Unregister()
    {
        if (_isRegistered)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
            _isRegistered = false;
        }
    }

    public void Dispose()
    {
        Unregister();
        _hwndSource?.RemoveHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Parses a hotkey string like "Ctrl+Alt+G" into Win32 modifier flags and virtual key code.
    /// </summary>
    internal static bool TryParseHotkey(string hotkeyString, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        if (string.IsNullOrWhiteSpace(hotkeyString))
            return false;

        var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToUpperInvariant())
            {
                case "CTRL" or "CONTROL":
                    modifiers |= 0x0002; // MOD_CONTROL
                    break;
                case "ALT":
                    modifiers |= 0x0001; // MOD_ALT
                    break;
                case "SHIFT":
                    modifiers |= 0x0004; // MOD_SHIFT
                    break;
                case "WIN":
                    modifiers |= 0x0008; // MOD_WIN
                    break;
                default:
                    return false; // unknown modifier
            }
        }

        var keyPart = parts[^1].ToUpperInvariant();

        // Try single letter/digit
        if (keyPart.Length == 1)
        {
            var c = keyPart[0];
            if (c is >= 'A' and <= 'Z')
            {
                vk = (uint)KeyInterop.VirtualKeyFromKey((Key)Enum.Parse(typeof(Key), keyPart));
                return vk != 0;
            }

            if (c is >= '0' and <= '9')
            {
                vk = (uint)c; // VK_0 to VK_9 match ASCII
                return true;
            }
        }

        // Try named keys (F1-F24, etc.)
        if (Enum.TryParse<Key>(keyPart, ignoreCase: true, out var key))
        {
            vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            return vk != 0;
        }

        return false;
    }
}
