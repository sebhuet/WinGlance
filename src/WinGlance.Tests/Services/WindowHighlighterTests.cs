using System.Runtime.ExceptionServices;
using WinGlance.NativeApi;
using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class WindowHighlighterTests
{
    [Fact]
    public void IsFrameShownAt_AlternatesForEachBlink()
    {
        var pattern = Enumerable.Range(0, WindowHighlighter.Blinks * 2).Select(WindowHighlighter.IsFrameShownAt);

        Assert.Equal([true, false, true, false, true, false], pattern);
    }

    [Fact]
    public void Flash_WindowThatDoesNotExist_StopsAtOnce()
    {
        RunOnSta(() =>
        {
            using var highlighter = new WindowHighlighter();

            highlighter.Flash(new IntPtr(0x7FFFFFF0));

            Assert.False(highlighter.IsFlashing);
        });
    }

    [Fact]
    public void TryGetVisibleBounds_InvalidHandle_ReturnsFalse()
    {
        Assert.False(NativeMethods.TryGetVisibleBounds(IntPtr.Zero, out _));
    }

    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }
}
