using System.Drawing;
using System.Drawing.Imaging;
using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class ScreenshotComparerTests
{
    [Fact]
    public void ComputeAverageHash_WhiteBitmap_ReturnsConsistentHash()
    {
        using var bmp = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
            g.Clear(Color.White);

        var hash1 = ScreenshotComparer.ComputeAverageHash(bmp);
        var hash2 = ScreenshotComparer.ComputeAverageHash(bmp);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeAverageHash_BlackBitmap_ReturnsConsistentHash()
    {
        using var bmp = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
            g.Clear(Color.Black);

        var hash1 = ScreenshotComparer.ComputeAverageHash(bmp);
        var hash2 = ScreenshotComparer.ComputeAverageHash(bmp);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeAverageHash_DifferentImages_ReturnDifferentHashes()
    {
        using var white = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(white))
            g.Clear(Color.White);

        using var halfBlack = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(halfBlack))
        {
            g.Clear(Color.White);
            g.FillRectangle(Brushes.Black, 0, 0, 32, 64);
        }

        var hash1 = ScreenshotComparer.ComputeAverageHash(white);
        var hash2 = ScreenshotComparer.ComputeAverageHash(halfBlack);
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void HammingDistance_IdenticalHashes_ReturnsZero()
    {
        Assert.Equal(0, ScreenshotComparer.HammingDistance(0xFFFF, 0xFFFF));
    }

    [Fact]
    public void HammingDistance_OneBitDifference_ReturnsOne()
    {
        Assert.Equal(1, ScreenshotComparer.HammingDistance(0b1000, 0b0000));
    }

    [Fact]
    public void HammingDistance_AllBitsDifferent_Returns64()
    {
        Assert.Equal(64, ScreenshotComparer.HammingDistance(0UL, ulong.MaxValue));
    }

    [Fact]
    public void IsStale_FirstCall_ReturnsFalse()
    {
        // CaptureAndHash requires a real window handle, so we can't test IsStale directly.
        // This tests that the comparer doesn't crash when given an invalid handle.
        var comparer = new ScreenshotComparer();
        var result = comparer.IsStale(IntPtr.Zero, 30);
        Assert.False(result); // capture fails → not stale
    }

    [Fact]
    public void Remove_DoesNotThrow_ForUnknownHandle()
    {
        var comparer = new ScreenshotComparer();
        comparer.Remove(new IntPtr(999));
    }

    // ── Staleness over time ──────────────────────────────────────────────

    private static readonly IntPtr Window = new(42);
    private static readonly DateTime T0 = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsStale_SameContentPastTheThreshold_ReturnsTrue()
    {
        var comparer = new ScreenshotComparer();

        Assert.False(comparer.IsStale(Window, 7UL, 30, T0)); // first capture
        Assert.False(comparer.IsStale(Window, 7UL, 30, T0.AddSeconds(29)));
        Assert.True(comparer.IsStale(Window, 7UL, 30, T0.AddSeconds(30)));
    }

    [Fact]
    public void IsStale_ContentChange_RestartsTheClock()
    {
        var comparer = new ScreenshotComparer();
        comparer.IsStale(Window, 7UL, 30, T0);

        Assert.False(comparer.IsStale(Window, 8UL, 30, T0.AddSeconds(40))); // changed
        Assert.False(comparer.IsStale(Window, 8UL, 30, T0.AddSeconds(60)));
        Assert.True(comparer.IsStale(Window, 8UL, 30, T0.AddSeconds(70)));
    }

    [Fact]
    public void IsStale_FailedCapture_IsNeverStale()
    {
        var comparer = new ScreenshotComparer();
        comparer.IsStale(Window, 7UL, 30, T0);

        Assert.False(comparer.IsStale(Window, null, 30, T0.AddMinutes(10)));
    }

    [Fact]
    public void Remove_ForgetsTheWindowHistory()
    {
        var comparer = new ScreenshotComparer();
        comparer.IsStale(Window, 7UL, 30, T0);

        comparer.Remove(Window);

        Assert.False(comparer.IsStale(Window, 7UL, 30, T0.AddMinutes(10))); // first capture again
    }
}
