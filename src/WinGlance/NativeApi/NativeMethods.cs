using System.Runtime.InteropServices;

namespace WinGlance.NativeApi;

/// <summary>
/// P/Invoke declarations and small helpers over the Win32 API. The <c>extern</c> signatures mirror the
/// Win32 functions of the same name, so Microsoft's documentation applies to them.
/// </summary>
internal static partial class NativeMethods
{
    // ── Constants ──────────────────────────────────────────────────────

    public const int GWL_EXSTYLE = -20;
    public const int GCL_HICON = -14;
    public const int GCL_HICONSM = -34;

    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_APPWINDOW = 0x00040000;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    public const int SW_RESTORE = 9;

    public const uint WM_GETICON = 0x007F;
    public const int ICON_SMALL = 0;
    public const int ICON_BIG = 1;
    public const int ICON_SMALL2 = 2;

    public const int GW_OWNER = 4;
    public const int GW_ENABLEDPOPUP = 6;

    public const uint SMTO_ABORTIFHUNG = 0x0002;

    public const int DWMWA_CLOAKED = 14;
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_LAYERED = 0x00080000;

    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const uint SWP_NOACTIVATE = 0x0010;

    public static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

    public const int HSHELL_REDRAW = 6;

    public const uint MOD_NOREPEAT = 0x4000;

    public const uint DWM_TNP_RECTDESTINATION = 0x00000001;
    public const uint DWM_TNP_RECTSOURCE = 0x00000002;
    public const uint DWM_TNP_OPACITY = 0x00000004;
    public const uint DWM_TNP_VISIBLE = 0x00000008;
    public const uint DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;

    public const int WM_HOTKEY = 0x0312;

    public const int HSHELL_FLASH = 0x8006;

    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const uint PROCESS_VM_READ = 0x0010;

    public const uint TH32CS_SNAPPROCESS = 0x00000002;
    public static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

    public const int ProcessBasicInformation = 0;

    // SendInput constants
    public const uint INPUT_KEYBOARD = 1;
    public const ushort VK_MENU = 0x12; // Alt key
    public const uint KEYEVENTF_KEYUP = 0x0002;

    // ── Structures ────────────────────────────────────────────────────

    /// <summary>Win32 RECT: a rectangle given by its edges in pixels; window bounds and DWM thumbnail rectangles.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>Win32 POINT: a position in pixels; used inside <see cref="WINDOWPLACEMENT"/>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>Win32 SIZE: a size in pixels; the source size returned by <c>DwmQueryThumbnailSourceSize</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE
    {
        public int Width;
        public int Height;
    }

    /// <summary>Win32 WINDOWPLACEMENT: show state and normal position of a window, read with <c>GetWindowPlacement</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public uint Length;
        public uint Flags;
        public uint ShowCmd;
        public POINT MinPosition;
        public POINT MaxPosition;
        public RECT NormalPosition;
    }

    /// <summary>Win32 DWM_THUMBNAIL_PROPERTIES: destination, source, opacity and visibility of a live thumbnail; <c>Flags</c> (DWM_TNP_*) selects the fields DWM applies.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DWM_THUMBNAIL_PROPERTIES
    {
        public uint Flags;
        public RECT Destination;
        public RECT Source;
        public byte Opacity;
        public int Visible;
        public int SourceClientAreaOnly;
    }

    // ── SendInput structures ────────────────────────────────────────────

    /// <summary>Win32 INPUT: one synthetic input event for <c>SendInput</c> (WinGlance sends an Alt key press before taking the foreground).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint Type;
        public InputUnion Union;

        public static int Size => Marshal.SizeOf<INPUT>();
    }

    // MOUSEINPUT is declared only to give the union its native size: SendInput rejects
    // calls whose cbSize differs from sizeof(INPUT) (40 bytes on x64, 28 on x86).
    /// <summary>Union of the INPUT payloads: a mouse or a keyboard event.</summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT Mouse;

        [FieldOffset(0)]
        public KEYBDINPUT Keyboard;
    }

    /// <summary>Win32 MOUSEINPUT: declared only to size <see cref="InputUnion"/>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    /// <summary>Win32 KEYBDINPUT: a keyboard event for <c>SendInput</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    // ── Process structures ──────────────────────────────────────────────

    /// <summary>Win32 PROCESSENTRY32W: one process of a Toolhelp32 snapshot (PID, parent PID, executable name).</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PROCESSENTRY32W
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public IntPtr DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    /// <summary>Win32 PROCESS_BASIC_INFORMATION: filled by <c>NtQueryInformationProcess</c>; WinGlance uses its PEB address to read a process's command line.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr ExitStatus;
        public IntPtr PebBaseAddress;
        public IntPtr AffinityMask;
        public IntPtr BasePriority;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    /// <summary>Win32 MONITORINFO: bounds and work area of a monitor; <c>Size</c> must be set before the call.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int Size;
        public RECT Monitor;
        public RECT WorkArea;
        public uint Flags;
    }

    /// <summary>Win32 DATA_BLOB: a byte buffer passed to and returned by the DPAPI functions <c>CryptProtectData</c> and <c>CryptUnprotectData</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DATA_BLOB
    {
        public int Size;
        public IntPtr Data;
    }

    // ── Delegates ─────────────────────────────────────────────────────

    /// <summary>Callback of <c>EnumWindows</c> and <c>EnumChildWindows</c>; returns true to continue the enumeration.</summary>
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>Callback of <c>EnumDisplayMonitors</c>; returns true to continue the enumeration.</summary>
    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    // ── User32 ────────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, char[] lpString, int nMaxCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool AllowSetForegroundWindow(int dwProcessId);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
    public static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowEnabled(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsHungAppWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hDC, uint nFlags);

    [DllImport("user32.dll")]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    // ── Shell Hook ────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    public static extern bool RegisterShellHookWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool DeregisterShellHookWindow(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode)]
    public static extern uint RegisterWindowMessage(string lpString);

    // ── Hotkey ────────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // ── DWM ───────────────────────────────────────────────────────────

    [DllImport("dwmapi.dll")]
    public static extern int DwmRegisterThumbnail(IntPtr hwndDestination, IntPtr hwndSource, out IntPtr phThumbnailId);

    [DllImport("dwmapi.dll")]
    public static extern int DwmUnregisterThumbnail(IntPtr hThumbnailId);

    [DllImport("dwmapi.dll")]
    public static extern int DwmUpdateThumbnailProperties(IntPtr hThumbnailId, ref DWM_THUMBNAIL_PROPERTIES ptnProperties);

    [DllImport("dwmapi.dll")]
    public static extern int DwmQueryThumbnailSourceSize(IntPtr hThumbnailId, out SIZE pSize);

    [DllImport("dwmapi.dll")]
    public static extern int DwmIsCompositionEnabled(out bool pfEnabled);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    // ── Crypt32 (DPAPI) ───────────────────────────────────────────────

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode)]
    public static extern bool CryptProtectData(ref DATA_BLOB pDataIn, string? szDataDescr, IntPtr pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, out DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode)]
    public static extern bool CryptUnprotectData(ref DATA_BLOB pDataIn, IntPtr ppszDataDescr, IntPtr pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, out DATA_BLOB pDataOut);

    [DllImport("kernel32.dll")]
    public static extern IntPtr LocalFree(IntPtr hMem);

    // ── Kernel32 (UWP) ────────────────────────────────────────────────

    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", EntryPoint = "GetApplicationUserModelId", CharSet = CharSet.Unicode)]
    public static extern int GetApplicationUserModelId(IntPtr hProcess, ref uint applicationUserModelIdLength, char[] applicationUserModelId);

    // ── Kernel32 / Ntdll (process tree, agent detection) ─────────────

    [DllImport("kernel32.dll")]
    public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode)]
    public static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode)]
    public static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll")]
    public static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

    [DllImport("kernel32.dll")]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, nint nSize, out nint lpNumberOfBytesRead);

    [DllImport("ntdll.dll")]
    public static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref PROCESS_BASIC_INFORMATION processInformation, int processInformationLength, out int returnLength);

    // ── Helpers ───────────────────────────────────────────────────────

    /// <summary>Returns the title of a window, or an empty string when it has none.</summary>
    public static string GetWindowTitle(IntPtr hWnd)
    {
        var length = GetWindowTextLength(hWnd);
        if (length == 0)
            return string.Empty;

        var buffer = new char[length + 1];
        GetWindowText(hWnd, buffer, buffer.Length);
        return new string(buffer, 0, length);
    }

    /// <summary>True if the window has the WS_EX_TOOLWINDOW style.</summary>
    public static bool IsToolWindow(IntPtr hWnd)
    {
        var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        return (exStyle & WS_EX_TOOLWINDOW) != 0;
    }

    /// <summary>True if the window has the WS_EX_APPWINDOW style.</summary>
    public static bool IsAppWindow(IntPtr hWnd)
    {
        var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        return (exStyle & WS_EX_APPWINDOW) != 0;
    }

    /// <summary>
    /// Lists every running process with its parent PID and executable name,
    /// using a Toolhelp32 snapshot (a few milliseconds, no WMI).
    /// </summary>
    public static List<PROCESSENTRY32W> GetProcessEntries()
    {
        var entries = new List<PROCESSENTRY32W>();
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE)
            return entries;

        try
        {
            var entry = new PROCESSENTRY32W { Size = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
            if (!Process32First(snapshot, ref entry))
                return entries;

            do
            {
                entries.Add(entry);
            }
            while (Process32Next(snapshot, ref entry));

            return entries;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    /// <summary>
    /// Reads the current directory of another process from its PEB.
    /// Returns null for 32-bit targets, when WinGlance itself is not 64-bit, or when access is denied.
    /// </summary>
    public static string? TryGetProcessCurrentDirectory(int processId) =>
        TryReadProcessParametersString(processId, ProcessParametersCurrentDirectoryOffset) is { } directory
            // The PEB stores the directory with a trailing separator ("C:\work\")
            ? System.IO.Path.TrimEndingDirectorySeparator(directory)
            : null;

    /// <summary>
    /// Reads the command line of another process from its PEB.
    /// Returns null for 32-bit targets, when WinGlance itself is not 64-bit, or when access is denied.
    /// </summary>
    public static string? TryGetProcessCommandLine(int processId) =>
        TryReadProcessParametersString(processId, ProcessParametersCommandLineOffset);

    // Stable x64 layout of RTL_USER_PROCESS_PARAMETERS: UNICODE_STRING fields
    private const int ProcessParametersCurrentDirectoryOffset = 0x38;
    private const int ProcessParametersCommandLineOffset = 0x70;

    /// <summary>
    /// Reads a UNICODE_STRING field of another process's RTL_USER_PROCESS_PARAMETERS.
    /// Windows offers no public API for this; PEB.ProcessParameters sits at 0x20 on x64.
    /// </summary>
    private static string? TryReadProcessParametersString(int processId, int fieldOffset)
    {
        if (!Environment.Is64BitProcess)
            return null;

        var hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ, false, (uint)processId);
        if (hProcess == IntPtr.Zero)
            return null;

        try
        {
            // A WOW64 process has a 32-bit PEB with different offsets
            if (!IsWow64Process(hProcess, out var isWow64) || isWow64)
                return null;

            var info = new PROCESS_BASIC_INFORMATION();
            if (NtQueryInformationProcess(hProcess, ProcessBasicInformation, ref info,
                    Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(), out _) != 0)
                return null;

            var parameters = ReadPointer(hProcess, info.PebBaseAddress + 0x20);
            if (parameters == IntPtr.Zero)
                return null;

            // UNICODE_STRING: Length (bytes) at +0, Buffer at +8
            var unicodeString = new byte[16];
            if (!ReadProcessMemory(hProcess, parameters + fieldOffset, unicodeString, unicodeString.Length, out var read)
                || read != unicodeString.Length)
                return null;

            var length = BitConverter.ToUInt16(unicodeString, 0);
            var buffer = (IntPtr)BitConverter.ToInt64(unicodeString, 8);
            if (length == 0 || buffer == IntPtr.Zero)
                return null;

            var chars = new byte[length];
            if (!ReadProcessMemory(hProcess, buffer, chars, length, out read) || read != length)
                return null;

            return System.Text.Encoding.Unicode.GetString(chars);
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    private static IntPtr ReadPointer(IntPtr hProcess, IntPtr address)
    {
        var bytes = new byte[IntPtr.Size];
        return ReadProcessMemory(hProcess, address, bytes, bytes.Length, out var read) && read == bytes.Length
            ? (IntPtr)BitConverter.ToInt64(bytes, 0)
            : IntPtr.Zero;
    }

    /// <summary>
    /// True if DWM hides the window: on another virtual desktop, or a suspended or
    /// closed UWP frame kept alive by ApplicationFrameHost. Such windows give black thumbnails.
    /// </summary>
    public static bool IsCloaked(IntPtr hWnd) =>
        DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    /// <summary>
    /// Visible bounds of a window in physical pixels. GetWindowRect includes the invisible
    /// resize borders of Windows 10/11; DWM's extended frame bounds do not.
    /// </summary>
    public static bool TryGetVisibleBounds(IntPtr hWnd, out RECT bounds)
    {
        if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out bounds, Marshal.SizeOf<RECT>()) == 0
            && bounds.Right > bounds.Left && bounds.Bottom > bounds.Top)
            return true;

        return GetWindowRect(hWnd, out bounds) && bounds.Right > bounds.Left && bounds.Bottom > bounds.Top;
    }

    /// <summary>Bounds of every monitor, in physical pixels.</summary>
    public static List<RECT> GetMonitorBounds()
    {
        var monitors = new List<RECT>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr _, IntPtr _, ref RECT bounds, IntPtr _) =>
        {
            monitors.Add(bounds);
            return true;
        }, IntPtr.Zero);
        return monitors;
    }

    /// <summary>Bounds of the monitor showing (most of) the window, in physical pixels.</summary>
    public static bool TryGetMonitorBounds(IntPtr hWnd, out RECT bounds)
    {
        var info = new MONITORINFO { Size = Marshal.SizeOf<MONITORINFO>() };
        var ok = GetMonitorInfo(MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST), ref info);
        bounds = info.Monitor;
        return ok;
    }

    /// <summary>
    /// Encrypts data with DPAPI for the current Windows user. Returns null on failure.
    /// </summary>
    public static byte[]? ProtectForCurrentUser(byte[] data) => TransformWithDpapi(data, protect: true);

    /// <summary>
    /// Decrypts data encrypted by <see cref="ProtectForCurrentUser"/>. Returns null when the data
    /// was encrypted by another user or machine, or is corrupt.
    /// </summary>
    public static byte[]? UnprotectForCurrentUser(byte[] data) => TransformWithDpapi(data, protect: false);

    private static byte[]? TransformWithDpapi(byte[] data, bool protect)
    {
        const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;

        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var input = new DATA_BLOB { Size = data.Length, Data = handle.AddrOfPinnedObject() };
            var ok = protect
                ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out var output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out output);
            if (!ok)
                return null;

            try
            {
                var result = new byte[output.Size];
                Marshal.Copy(output.Data, result, 0, output.Size);
                return result;
            }
            finally
            {
                // The output buffer is allocated by crypt32 with LocalAlloc
                LocalFree(output.Data);
            }
        }
        finally
        {
            handle.Free();
        }
    }
}
