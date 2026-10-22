using WinGlance.NativeApi;

namespace WinGlance.Services;

/// <summary>
/// Stereo position of a monitor among all the monitors, from the leftmost (-1) to the
/// rightmost (+1): with three monitors, left, center and right.
/// </summary>
internal static class ScreenSide
{
    /// <summary>
    /// Pan of the given monitor: monitors are ordered by the horizontal position of their
    /// center and spread evenly from -1 to +1. A single monitor is centered (0).
    /// </summary>
    public static double PanOf(NativeMethods.RECT monitor, IReadOnlyList<NativeMethods.RECT> monitors)
    {
        if (monitors.Count <= 1)
            return 0;

        var ordered = monitors.OrderBy(CenterX).ThenBy(CenterY).ToList();
        var index = ordered.FindIndex(m => SameBounds(m, monitor));
        if (index < 0)
        {
            // Monitors changed since the bounds were read: take the closest one
            index = ordered.IndexOf(ordered.MinBy(m => Math.Abs(CenterX(m) - CenterX(monitor))));
        }

        return -1 + (2.0 * index / (ordered.Count - 1));
    }

    /// <summary>Pan of the monitor showing the window, read from the current monitor layout.</summary>
    public static double PanOfWindow(IntPtr hwnd) =>
        NativeMethods.TryGetMonitorBounds(hwnd, out var monitor)
            ? PanOf(monitor, NativeMethods.GetMonitorBounds())
            : 0;

    private static double CenterX(NativeMethods.RECT r) => (r.Left + r.Right) / 2.0;

    private static double CenterY(NativeMethods.RECT r) => (r.Top + r.Bottom) / 2.0;

    private static bool SameBounds(NativeMethods.RECT a, NativeMethods.RECT b) =>
        a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
}
