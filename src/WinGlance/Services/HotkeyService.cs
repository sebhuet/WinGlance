using System.Windows.Input;
using System.Windows.Interop;
using WinGlance.NativeApi;

namespace WinGlance.Services;

/// <summary>
/// Registers and manages a global hotkey using the Win32 RegisterHotKey API.
/// Hooks into the window message loop via <see cref="HwndSource"/> to receive WM_HOTKEY.
/// </summary>
/// <param name="hotkeyId">
/// Identifier of the hotkey on its window: each hotkey of a window needs its own.
/// </param>
internal sealed class HotkeyService(int hotkeyId = 1) : IDisposable
{
    private readonly int _hotkeyId = hotkeyId;

    private IntPtr _hwnd;
    private HwndSource? _hwndSource;
    private bool _isRegistered;
    private uint _modifiers;
    private uint _virtualKey;

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

    /// <summary>True while a hotkey is registered.</summary>
    public bool IsRegistered => _isRegistered;

    /// <summary>
    /// Parses a hotkey string (e.g., "Ctrl+Alt+G") and registers it globally, replacing the
    /// current one. Returns false if the string is invalid or the combination is taken by
    /// another application; the previous hotkey then stays registered.
    /// </summary>
    public bool Register(string hotkeyString)
    {
        if (!TryParseHotkey(hotkeyString, out var modifiers, out var vk))
            return false;

        var hadPrevious = _isRegistered;
        var (previousModifiers, previousKey) = (_modifiers, _virtualKey);
        Unregister();

        // MOD_NOREPEAT: holding the keys down must not toggle the panel over and over
        if (NativeMethods.RegisterHotKey(_hwnd, _hotkeyId, modifiers | NativeMethods.MOD_NOREPEAT, vk))
        {
            (_modifiers, _virtualKey, _isRegistered) = (modifiers, vk, true);
            return true;
        }

        if (hadPrevious)
        {
            _isRegistered = NativeMethods.RegisterHotKey(
                _hwnd, _hotkeyId, previousModifiers | NativeMethods.MOD_NOREPEAT, previousKey);
            (_modifiers, _virtualKey) = (previousModifiers, previousKey);
        }

        return false;
    }

    /// <summary>
    /// Unregisters the current global hotkey.
    /// </summary>
    public void Unregister()
    {
        if (_isRegistered)
        {
            NativeMethods.UnregisterHotKey(_hwnd, _hotkeyId);
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
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == _hotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Parses a hotkey string like "Ctrl+Alt+G" into Win32 modifier flags and virtual key code.
    /// At least one modifier is required: a bare key would be captured system-wide.
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

        if (modifiers == 0)
            return false;

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

        // Try named keys (F1-F24, etc.). Enum.TryParse also accepts numbers ("65") and
        // comma-separated flags ("A,B"): only real key names, which start with a letter, count.
        if (char.IsLetter(keyPart[0])
            && !keyPart.Contains(',')
            && Enum.TryParse<Key>(keyPart, ignoreCase: true, out var key)
            && Enum.IsDefined(key))
        {
            vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            return vk != 0;
        }

        return false;
    }
}
