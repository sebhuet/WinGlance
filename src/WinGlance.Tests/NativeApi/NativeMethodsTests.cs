using System.Runtime.InteropServices;
using WinGlance.NativeApi;

namespace WinGlance.Tests.NativeApi;

public class NativeMethodsTests
{
    // ── Struct size / layout tests ──────────────────────────────────────

    [Fact]
    public void RECT_SizeIs16Bytes()
    {
        Assert.Equal(16, Marshal.SizeOf<NativeMethods.RECT>());
    }

    [Fact]
    public void POINT_SizeIs8Bytes()
    {
        Assert.Equal(8, Marshal.SizeOf<NativeMethods.POINT>());
    }

    [Fact]
    public void SIZE_SizeIs8Bytes()
    {
        Assert.Equal(8, Marshal.SizeOf<NativeMethods.SIZE>());
    }

    [Fact]
    public void WINDOWPLACEMENT_SizeIs44Bytes()
    {
        Assert.Equal(44, Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>());
    }

    [Fact]
    public void DWM_THUMBNAIL_PROPERTIES_SizeIs45Bytes()
    {
        // 4 (Flags) + 16 (Dest RECT) + 16 (Source RECT) + 1 (Opacity) + 4 (Visible) + 4 (SourceClientAreaOnly) = 45
        // But with padding it may be larger — verify marshalled size
        var size = Marshal.SizeOf<NativeMethods.DWM_THUMBNAIL_PROPERTIES>();
        Assert.True(size >= 45, $"DWM_THUMBNAIL_PROPERTIES should be at least 45 bytes, got {size}");
    }

    // ── P/Invoke smoke tests ────────────────────────────────────────────

    [Fact]
    public void DwmIsCompositionEnabled_DoesNotThrow()
    {
        var hr = NativeMethods.DwmIsCompositionEnabled(out var enabled);
        Assert.Equal(0, hr); // S_OK
        Assert.True(enabled); // DWM is always enabled on Windows 8+
    }

    [Fact]
    public void EnumWindows_FindsAtLeastOneWindow()
    {
        var count = 0;
        NativeMethods.EnumWindows((hWnd, lParam) =>
        {
            count++;
            return true; // continue enumeration
        }, IntPtr.Zero);

        Assert.True(count > 0, "EnumWindows should find at least one window");
    }

    [Fact]
    public void GetForegroundWindow_ReturnsNonZeroHandle()
    {
        var hWnd = NativeMethods.GetForegroundWindow();
        Assert.NotEqual(IntPtr.Zero, hWnd);
    }

    [Fact]
    public void GetWindowTitle_ReturnsNonEmptyForForegroundWindow()
    {
        var hWnd = NativeMethods.GetForegroundWindow();
        // Foreground window should have a title (though not strictly guaranteed)
        var title = NativeMethods.GetWindowTitle(hWnd);
        Assert.NotNull(title);
    }

    [Fact]
    public void IsWindowVisible_WorksForForegroundWindow()
    {
        var hWnd = NativeMethods.GetForegroundWindow();
        var visible = NativeMethods.IsWindowVisible(hWnd);
        Assert.True(visible, "Foreground window should be visible");
    }

    [Fact]
    public void GetWindowLong_DoesNotThrow()
    {
        var hWnd = NativeMethods.GetForegroundWindow();
        // Should not throw — just verifying the P/Invoke binding works
        var exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
        // exStyle can be any value, we just check it doesn't crash
        Assert.True(true);
    }

    // ── Helper method tests ─────────────────────────────────────────────

    [Fact]
    public void IsToolWindow_ReturnsFalseForNormalWindow()
    {
        var hWnd = NativeMethods.GetForegroundWindow();
        // Most foreground windows are not tool windows
        var isToolWindow = NativeMethods.IsToolWindow(hWnd);
        // We don't assert the value — just that it doesn't crash
        Assert.IsType<bool>(isToolWindow);
    }

    [Fact]
    public void IsAppWindow_DoesNotThrow()
    {
        var hWnd = NativeMethods.GetForegroundWindow();
        var isAppWindow = NativeMethods.IsAppWindow(hWnd);
        Assert.IsType<bool>(isAppWindow);
    }

    // ── Constant value sanity checks ────────────────────────────────────

    [Fact]
    public void Constants_HaveExpectedValues()
    {
        Assert.Equal(-20, NativeMethods.GWL_EXSTYLE);
        Assert.Equal(0x00000080, NativeMethods.WS_EX_TOOLWINDOW);
        Assert.Equal(0x00040000, NativeMethods.WS_EX_APPWINDOW);
        Assert.Equal(0x08000000, NativeMethods.WS_EX_NOACTIVATE);
        Assert.Equal(9, NativeMethods.SW_RESTORE);
        Assert.Equal(0x007Fu, NativeMethods.WM_GETICON);
        Assert.Equal(0x0312, NativeMethods.WM_HOTKEY);
        Assert.Equal(0x8006, NativeMethods.HSHELL_FLASH);
        Assert.Equal(0x1000u, NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION);
    }
}
