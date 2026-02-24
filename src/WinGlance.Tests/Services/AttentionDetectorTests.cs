using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class AttentionDetectorTests
{
    [Fact]
    public void IsFlashing_UnknownHandle_ReturnsFalse()
    {
        var detector = new AttentionDetector();

        Assert.False(detector.IsFlashing(new IntPtr(99999)));
    }

    [Fact]
    public void GetFlashingWindows_Empty_ByDefault()
    {
        var detector = new AttentionDetector();

        var result = detector.GetFlashingWindows();

        Assert.Empty(result);
    }

    [Fact]
    public void ClearFlashing_NonexistentHandle_DoesNotThrow()
    {
        var detector = new AttentionDetector();

        detector.ClearFlashing(new IntPtr(12345));

        Assert.False(detector.IsFlashing(new IntPtr(12345)));
    }

    [Fact]
    public void Dispose_WithoutInitialize_DoesNotThrow()
    {
        var detector = new AttentionDetector();

        detector.Dispose();
    }
}
