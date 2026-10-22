using WinGlance.NativeApi;
using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class AttentionDetectorTests
{
    private static readonly IntPtr Window = new(0x1234);

    [Fact]
    public void IsFlashing_UnknownHandle_ReturnsFalse()
    {
        var detector = new AttentionDetector();

        Assert.False(detector.IsFlashing(Window));
    }

    [Fact]
    public void GetFlashingWindows_Empty_ByDefault()
    {
        var detector = new AttentionDetector();

        Assert.Empty(detector.GetFlashingWindows());
    }

    [Fact]
    public void ClearFlashing_NonexistentHandle_DoesNotThrow()
    {
        var detector = new AttentionDetector();

        var exception = Record.Exception(() => detector.ClearFlashing(Window));

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_WithoutInitialize_DoesNotThrow()
    {
        var detector = new AttentionDetector();

        var exception = Record.Exception(detector.Dispose);

        Assert.Null(exception);
    }

    // ── Shell hook notifications ─────────────────────────────────────────

    [Fact]
    public void HandleShellMessage_Flash_MarksTheWindowAsFlashing()
    {
        var detector = new AttentionDetector();

        detector.HandleShellMessage(NativeMethods.HSHELL_FLASH, Window);

        Assert.True(detector.IsFlashing(Window));
    }

    [Fact]
    public void HandleShellMessage_RedrawAfterFlash_ClearsIt()
    {
        // The shell sends a plain HSHELL_REDRAW when the window stops flashing
        var detector = new AttentionDetector();
        detector.HandleShellMessage(NativeMethods.HSHELL_FLASH, Window);

        detector.HandleShellMessage(NativeMethods.HSHELL_REDRAW, Window);

        Assert.False(detector.IsFlashing(Window));
    }

    [Fact]
    public void HandleShellMessage_OtherCodes_AreIgnored()
    {
        var detector = new AttentionDetector();
        detector.HandleShellMessage(NativeMethods.HSHELL_FLASH, Window);

        detector.HandleShellMessage(1, Window); // HSHELL_WINDOWCREATED

        Assert.True(detector.IsFlashing(Window));
    }

    [Fact]
    public void PruneClosedWindows_ForgetsHandlesThatNoLongerExist()
    {
        var detector = new AttentionDetector();
        detector.HandleShellMessage(NativeMethods.HSHELL_FLASH, new IntPtr(0x7FFFFFF0)); // not a window

        detector.PruneClosedWindows();

        Assert.Empty(detector.GetFlashingWindows());
    }
}
