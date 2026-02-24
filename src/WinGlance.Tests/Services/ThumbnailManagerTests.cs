using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class ThumbnailManagerTests
{
    [Fact]
    public void Register_WithoutDestination_ReturnsZero()
    {
        using var manager = new ThumbnailManager();
        // No destination set — should return IntPtr.Zero
        var id = manager.Register((IntPtr)12345);
        Assert.Equal(IntPtr.Zero, id);
    }

    [Fact]
    public void Register_WithZeroSource_ReturnsZero()
    {
        using var manager = new ThumbnailManager();
        manager.SetDestination((IntPtr)1);
        var id = manager.Register(IntPtr.Zero);
        Assert.Equal(IntPtr.Zero, id);
    }

    [Fact]
    public void GetThumbnailId_Unregistered_ReturnsZero()
    {
        using var manager = new ThumbnailManager();
        var id = manager.GetThumbnailId((IntPtr)99999);
        Assert.Equal(IntPtr.Zero, id);
    }

    [Fact]
    public void Unregister_NonExistent_DoesNotThrow()
    {
        using var manager = new ThumbnailManager();
        // Should not throw even if never registered
        manager.Unregister((IntPtr)99999);
    }

    [Fact]
    public void UnregisterAll_EmptyManager_DoesNotThrow()
    {
        using var manager = new ThumbnailManager();
        manager.UnregisterAll();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var manager = new ThumbnailManager();
        manager.Dispose();
        manager.Dispose(); // second call should be safe
    }

    [Fact]
    public void Update_WithZeroId_ReturnsFalse()
    {
        using var manager = new ThumbnailManager();
        var result = manager.Update(IntPtr.Zero, default);
        Assert.False(result);
    }

    [Fact]
    public void QuerySourceSize_WithZeroId_ReturnsFalse()
    {
        using var manager = new ThumbnailManager();
        var result = manager.QuerySourceSize(IntPtr.Zero, out _);
        Assert.False(result);
    }
}
