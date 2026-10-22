using System.Runtime.InteropServices;

namespace WinGlance.NativeApi;

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

    public const int GW_ENABLEDPOPUP = 6;

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

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE
    {
        public int Width;
        public int Height;
    }

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

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint Type;
        public InputUnion Union;

        public static int Size => Marshal.SizeOf<INPUT>();
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT Keyboard;
    }

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

    // ── Delegates ─────────────────────────────────────────────────────

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

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

    public static string GetWindowTitle(IntPtr hWnd)
    {
        var length = GetWindowTextLength(hWnd);
        if (length == 0)
            return string.Empty;

        var buffer = new char[length + 1];
        GetWindowText(hWnd, buffer, buffer.Length);
        return new string(buffer, 0, length);
    }

    public static bool IsToolWindow(IntPtr hWnd)
    {
        var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        return (exStyle & WS_EX_TOOLWINDOW) != 0;
    }

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
    /// Windows offers no public API for this; the offsets below are the stable x64 layout
    /// (PEB.ProcessParameters at 0x20, RTL_USER_PROCESS_PARAMETERS.CurrentDirectory at 0x38).
    /// Returns null for 32-bit targets, when WinGlance itself is not 64-bit, or when access is denied.
    /// </summary>
    public static string? TryGetProcessCurrentDirectory(int processId)
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

            // CurrentDirectory.DosPath is a UNICODE_STRING: Length (bytes) at +0, Buffer at +8
            var unicodeString = new byte[16];
            if (!ReadProcessMemory(hProcess, parameters + 0x38, unicodeString, unicodeString.Length, out var read)
                || read != unicodeString.Length)
                return null;

            var length = BitConverter.ToUInt16(unicodeString, 0);
            var buffer = (IntPtr)BitConverter.ToInt64(unicodeString, 8);
            if (length == 0 || buffer == IntPtr.Zero)
                return null;

            var chars = new byte[length];
            if (!ReadProcessMemory(hProcess, buffer, chars, length, out read) || read != length)
                return null;

            // The PEB stores the directory with a trailing separator ("C:\work\")
            return System.IO.Path.TrimEndingDirectorySeparator(System.Text.Encoding.Unicode.GetString(chars));
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
}
