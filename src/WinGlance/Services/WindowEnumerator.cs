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
    // A hung window must not stall the polling thread on WM_GETICON
    private const uint IconTimeoutMs = 100;

    /// <summary>
    /// Enumerates all visible windows belonging to the specified monitored applications.
    /// </summary>
    /// <param name="monitoredApps">Applications to look for; duplicate process names are ignored.</param>
    /// <param name="needsIcon">
    /// Tells whether a window still needs its icon extracted; null extracts every icon.
    /// Lets the caller skip windows whose icon it already has.
    /// </param>
    public List<TrackedWindow> GetWindows(IEnumerable<MonitoredApp> monitoredApps, Func<IntPtr, bool>? needsIcon = null)
    {
        // A hand-edited config can list the same process twice; the first entry wins
        var appLookup = new Dictionary<string, MonitoredApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in monitoredApps)
            appLookup.TryAdd(app.ProcessName, app);

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

            var resolvedName = ResolveAppName(hWnd, pid, processName);

            if (!appLookup.TryGetValue(resolvedName, out var app))
                return true; // not a monitored app

            var title = NativeMethods.GetWindowTitle(hWnd);
            if (string.IsNullOrWhiteSpace(title))
                return true;

            var window = new TrackedWindow(hWnd, app.ProcessName, app.DisplayName)
            {
                ProcessId = pid,
                Title = title,
                IsActive = hWnd == foregroundHwnd,
                Icon = needsIcon is null || needsIcon(hWnd) ? ExtractIcon(hWnd) : null,
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

            var resolvedName = ResolveAppName(hWnd, pid, processName);

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
    /// Determines if a window should appear in enumeration results, following the taskbar's rules:
    /// visible, titled, not a tool window, not owned by another window (dialogs), unless it has
    /// WS_EX_APPWINDOW; and not cloaked by DWM (other virtual desktop, ghost UWP frame).
    /// </summary>
    internal static bool IsEligibleWindow(IntPtr hWnd)
    {
        if (!NativeMethods.IsWindowVisible(hWnd))
            return false;

        // Skip windows with no title
        if (NativeMethods.GetWindowTextLength(hWnd) == 0)
            return false;

        if (!NativeMethods.IsAppWindow(hWnd))
        {
            // Tool windows and owned windows (dialogs, popups) have no taskbar button
            if (NativeMethods.IsToolWindow(hWnd))
                return false;
            if (NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER) != IntPtr.Zero)
                return false;
        }

        return !NativeMethods.IsCloaked(hWnd);
    }

    // ── Process resolution ──────────────────────────────────────────────

    /// <summary>Returns the process name for a PID, or null if the process has exited.</summary>
    private static string? GetProcessName(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch
        {
            return null; // process may have exited
        }
    }

    /// <summary>
    /// Returns the application name of a window: its process name, except for UWP windows,
    /// whose frame belongs to ApplicationFrameHost.exe and is resolved to the hosted app.
    /// </summary>
    private static string ResolveAppName(IntPtr hWnd, uint pid, string processName)
    {
        if (!string.Equals(processName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
            return processName;

        var appPid = FindHostedAppProcessId(hWnd, pid);
        return appPid == 0
            ? processName // suspended app: its core window has left the frame
            : ResolveUwpAppName(appPid) ?? GetProcessName(appPid) ?? processName;
    }

    /// <summary>
    /// Finds the process of the UWP app hosted in an ApplicationFrameHost frame: the frame's
    /// child core window belongs to the app process, not to the frame host. Returns 0 if none.
    /// </summary>
    private static uint FindHostedAppProcessId(IntPtr frameHwnd, uint framePid)
    {
        uint appPid = 0;
        NativeMethods.EnumChildWindows(frameHwnd, (child, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(child, out var childPid);
            if (childPid == framePid)
                return true; // frame's own child, keep looking

            appPid = childPid;
            return false; // found, stop
        }, IntPtr.Zero);

        return appPid;
    }

    /// <summary>
    /// Resolves the app name of a packaged (UWP) process via GetApplicationUserModelId.
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
        // The handle belongs to the window or its class: it is copied, never destroyed here
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
    }

    /// <summary>
    /// Tries multiple strategies to get a window icon handle:
    /// WM_GETICON (ICON_SMALL2 → ICON_SMALL → ICON_BIG) then class icon fallback.
    /// </summary>
    private static IntPtr GetWindowIcon(IntPtr hWnd)
    {
        foreach (var iconType in new[] { NativeMethods.ICON_SMALL2, NativeMethods.ICON_SMALL, NativeMethods.ICON_BIG })
        {
            var windowIcon = QueryIcon(hWnd, iconType);
            if (windowIcon != IntPtr.Zero)
                return windowIcon;
        }

        // Fallback to class icon
        var classIcon = NativeMethods.GetClassLongPtr(hWnd, NativeMethods.GCL_HICONSM);
        return classIcon != IntPtr.Zero
            ? classIcon
            : NativeMethods.GetClassLongPtr(hWnd, NativeMethods.GCL_HICON);
    }

    /// <summary>
    /// Sends WM_GETICON with a timeout: a plain SendMessage to a hung window never returns.
    /// </summary>
    private static IntPtr QueryIcon(IntPtr hWnd, int iconType)
    {
        var ok = NativeMethods.SendMessageTimeout(hWnd, NativeMethods.WM_GETICON, iconType, IntPtr.Zero,
            NativeMethods.SMTO_ABORTIFHUNG, IconTimeoutMs, out var hIcon);
        return ok == IntPtr.Zero ? IntPtr.Zero : hIcon;
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
