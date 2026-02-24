using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinGlance.Models;
using WinGlance.NativeApi;

namespace WinGlance.Services;

/// <summary>
/// Discovers and enumerates visible application windows using the Win32 EnumWindows API.
/// Handles UWP app resolution, icon extraction, and window state detection (hung, modal).
/// </summary>
internal sealed class WindowEnumerator
{
    /// <summary>
    /// Enumerates all visible windows belonging to the specified monitored applications.
    /// </summary>
    public List<TrackedWindow> GetWindows(IEnumerable<MonitoredApp> monitoredApps)
    {
        var appLookup = monitoredApps.ToDictionary(
            a => a.ProcessName,
            a => a,
            StringComparer.OrdinalIgnoreCase);

        var foregroundHwnd = NativeMethods.GetForegroundWindow();
        var results = new List<TrackedWindow>();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!IsEligibleWindow(hWnd))
                return true; // skip, continue enumeration

            NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
            var processName = GetProcessName(pid);
            if (processName is null)
                return true;

            // Resolve UWP apps hosted by ApplicationFrameHost.exe
            var resolvedName = string.Equals(processName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase)
                ? ResolveUwpAppName(pid) ?? processName
                : processName;

            if (!appLookup.TryGetValue(resolvedName, out var app))
                return true; // not a monitored app

            var title = NativeMethods.GetWindowTitle(hWnd);
            if (string.IsNullOrWhiteSpace(title))
                return true;

            var window = new TrackedWindow(hWnd, app.ProcessName, app.DisplayName)
            {
                Title = title,
                IsActive = hWnd == foregroundHwnd,
                Icon = ExtractIcon(hWnd),
                IsHung = NativeMethods.IsHungAppWindow(hWnd),
                IsModalBlocked = !NativeMethods.IsWindowEnabled(hWnd),
            };

            results.Add(window);
            return true; // continue
        }, IntPtr.Zero);

        return results;
    }

    /// <summary>
    /// Enumerates all visible application windows (for the Applications tab discovery).
    /// Returns one entry per unique process name.
    /// </summary>
    public List<DiscoveredApp> DiscoverRunningApps()
    {
        var apps = new Dictionary<string, DiscoveredApp>(StringComparer.OrdinalIgnoreCase);

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!IsEligibleWindow(hWnd))
                return true;

            NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
            var processName = GetProcessName(pid);
            if (processName is null)
                return true;

            var resolvedName = string.Equals(processName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase)
                ? ResolveUwpAppName(pid) ?? processName
                : processName;

            if (apps.TryGetValue(resolvedName, out var existing))
            {
                existing.WindowCount++;
            }
            else
            {
                apps[resolvedName] = new DiscoveredApp(resolvedName, windowCount: 1);
            }

            return true;
        }, IntPtr.Zero);

        return apps.Values.OrderBy(a => a.ProcessName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ── Filtering ───────────────────────────────────────────────────────

    /// <summary>
    /// Determines if a window should appear in enumeration results.
    /// Filters out invisible windows, tool windows (unless WS_EX_APPWINDOW), and untitled windows.
    /// </summary>
    internal static bool IsEligibleWindow(IntPtr hWnd)
    {
        if (!NativeMethods.IsWindowVisible(hWnd))
            return false;

        // Skip tool windows unless they also have WS_EX_APPWINDOW
        if (NativeMethods.IsToolWindow(hWnd) && !NativeMethods.IsAppWindow(hWnd))
            return false;

        // Skip windows with no title
        if (NativeMethods.GetWindowTextLength(hWnd) == 0)
            return false;

        return true;
    }

    // ── Process resolution ──────────────────────────────────────────────

    /// <summary>Returns the process name for a PID, or null if the process has exited.</summary>
    private static string? GetProcessName(uint pid)
    {
        try
        {
            var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch
        {
            return null; // process may have exited
        }
    }

    /// <summary>
    /// Resolves the real app name for a UWP process via GetApplicationUserModelId.
    /// UWP windows run under ApplicationFrameHost.exe; this extracts the actual package name.
    /// </summary>
    private static string? ResolveUwpAppName(uint pid)
    {
        var hProcess = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);

        if (hProcess == IntPtr.Zero)
            return null;

        try
        {
            uint length = 0;
            // First call to get the required buffer length
            NativeMethods.GetApplicationUserModelId(hProcess, ref length, null!);
            if (length == 0)
                return null;

            var buffer = new char[length];
            var hr = NativeMethods.GetApplicationUserModelId(hProcess, ref length, buffer);
            if (hr != 0)
                return null;

            var aumid = new string(buffer, 0, (int)length - 1); // exclude null terminator
            // Extract the app name from the AUMID (e.g., "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App" → "WindowsCalculator")
            return ExtractAppNameFromAumid(aumid);
        }
        finally
        {
            NativeMethods.CloseHandle(hProcess);
        }
    }

    /// <summary>
    /// Extracts the app name from an Application User Model ID (AUMID).
    /// Example: "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App" returns "WindowsCalculator".
    /// </summary>
    internal static string? ExtractAppNameFromAumid(string aumid)
    {
        // AUMID format: "Publisher.AppName_hash!EntryPoint"
        // We want the AppName portion
        var bangIndex = aumid.IndexOf('!');
        var packagePart = bangIndex >= 0 ? aumid[..bangIndex] : aumid;

        var underscoreIndex = packagePart.LastIndexOf('_');
        var nameWithPublisher = underscoreIndex >= 0 ? packagePart[..underscoreIndex] : packagePart;

        // Remove publisher prefix (e.g., "Microsoft." prefix)
        var dotIndex = nameWithPublisher.IndexOf('.');
        return dotIndex >= 0 ? nameWithPublisher[(dotIndex + 1)..] : nameWithPublisher;
    }

    // ── Icon extraction ─────────────────────────────────────────────────

    /// <summary>
    /// Extracts the application icon for a window handle.
    /// The returned <see cref="BitmapSource"/> is frozen for cross-thread usage.
    /// </summary>
    internal static ImageSource? ExtractIcon(IntPtr hWnd)
    {
        var hIcon = GetWindowIcon(hWnd);
        if (hIcon == IntPtr.Zero)
            return null;

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze(); // make cross-thread safe
            return source;
        }
        catch
        {
            return null;
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }

    /// <summary>
    /// Tries multiple strategies to get a window icon handle:
    /// WM_GETICON (ICON_SMALL2 → ICON_SMALL → ICON_BIG) then class icon fallback.
    /// </summary>
    private static IntPtr GetWindowIcon(IntPtr hWnd)
    {
        // Try WM_GETICON with ICON_SMALL first
        var hIcon = NativeMethods.SendMessage(hWnd, NativeMethods.WM_GETICON,
            (IntPtr)NativeMethods.ICON_SMALL2, IntPtr.Zero);

        if (hIcon != IntPtr.Zero)
            return hIcon;

        hIcon = NativeMethods.SendMessage(hWnd, NativeMethods.WM_GETICON,
            (IntPtr)NativeMethods.ICON_SMALL, IntPtr.Zero);

        if (hIcon != IntPtr.Zero)
            return hIcon;

        // Try WM_GETICON with ICON_BIG
        hIcon = NativeMethods.SendMessage(hWnd, NativeMethods.WM_GETICON,
            (IntPtr)NativeMethods.ICON_BIG, IntPtr.Zero);

        if (hIcon != IntPtr.Zero)
            return hIcon;

        // Fallback to class icon
        hIcon = NativeMethods.GetClassLongPtr(hWnd, NativeMethods.GCL_HICONSM);
        if (hIcon != IntPtr.Zero)
            return hIcon;

        hIcon = NativeMethods.GetClassLongPtr(hWnd, NativeMethods.GCL_HICON);
        return hIcon;
    }
}

/// <summary>
/// Lightweight result type for the app discovery scan.
/// </summary>
internal sealed class DiscoveredApp(string processName, int windowCount)
{
    public string ProcessName { get; } = processName;
    public int WindowCount { get; set; } = windowCount;
}
