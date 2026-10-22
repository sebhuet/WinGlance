using WinGlance.NativeApi;

namespace WinGlance.Services;

/// <summary>
/// Manages DWM thumbnail registrations between source windows and a destination window.
/// Each thumbnail is identified by a handle returned from DwmRegisterThumbnail.
/// Thread-safe: the polling thread unregisters closed windows while the UI thread
/// registers and updates visible ones.
/// </summary>
internal sealed class ThumbnailManager : IDisposable
{
    private readonly Dictionary<IntPtr, IntPtr> _thumbnails = []; // sourceHwnd → thumbnailId
    private readonly object _lock = new();
    private IntPtr _destinationHwnd;
    private bool _disposed;

    /// <summary>
    /// Sets the destination window handle. Must be called before any Register calls.
    /// </summary>
    public void SetDestination(IntPtr hwnd)
    {
        _destinationHwnd = hwnd;
    }

    /// <summary>
    /// Registers a DWM thumbnail for the given source window.
    /// Returns the thumbnail handle, or IntPtr.Zero on failure.
    /// </summary>
    public IntPtr Register(IntPtr sourceHwnd)
    {
        if (_destinationHwnd == IntPtr.Zero || sourceHwnd == IntPtr.Zero)
            return IntPtr.Zero;

        lock (_lock)
        {
            // Already registered — return existing
            if (_thumbnails.TryGetValue(sourceHwnd, out var existing))
                return existing;

            var hr = NativeMethods.DwmRegisterThumbnail(_destinationHwnd, sourceHwnd, out var thumbnailId);
            if (hr != 0)
                return IntPtr.Zero;

            _thumbnails[sourceHwnd] = thumbnailId;
            return thumbnailId;
        }
    }

    /// <summary>
    /// Updates the thumbnail properties (destination rect, source rect, opacity, visibility).
    /// </summary>
    /// <param name="source">Part of the source to show; null shows all of it.</param>
    public bool Update(
        IntPtr thumbnailId,
        NativeMethods.RECT destination,
        NativeMethods.RECT? source = null,
        byte opacity = 255,
        bool visible = true,
        bool sourceClientAreaOnly = true)
    {
        if (thumbnailId == IntPtr.Zero)
            return false;

        var props = new NativeMethods.DWM_THUMBNAIL_PROPERTIES
        {
            Flags = NativeMethods.DWM_TNP_RECTDESTINATION
                    | NativeMethods.DWM_TNP_VISIBLE
                    | NativeMethods.DWM_TNP_OPACITY
                    | NativeMethods.DWM_TNP_SOURCECLIENTAREAONLY,
            Destination = destination,
            Opacity = opacity,
            Visible = visible ? 1 : 0,
            SourceClientAreaOnly = sourceClientAreaOnly ? 1 : 0,
        };

        if (source is { } sourceRect)
        {
            props.Flags |= NativeMethods.DWM_TNP_RECTSOURCE;
            props.Source = sourceRect;
        }

        var hr = NativeMethods.DwmUpdateThumbnailProperties(thumbnailId, ref props);
        return hr == 0;
    }

    /// <summary>
    /// Queries the source window size for the given thumbnail, in physical pixels.
    /// </summary>
    public bool QuerySourceSize(IntPtr thumbnailId, out NativeMethods.SIZE size)
    {
        size = default;
        if (thumbnailId == IntPtr.Zero)
            return false;

        return NativeMethods.DwmQueryThumbnailSourceSize(thumbnailId, out size) == 0;
    }

    /// <summary>
    /// Unregisters a single thumbnail by source window handle.
    /// </summary>
    public void Unregister(IntPtr sourceHwnd)
    {
        lock (_lock)
        {
            if (_thumbnails.Remove(sourceHwnd, out var thumbnailId))
                NativeMethods.DwmUnregisterThumbnail(thumbnailId);
        }
    }

    /// <summary>
    /// Unregisters all thumbnails.
    /// </summary>
    public void UnregisterAll()
    {
        lock (_lock)
        {
            foreach (var thumbnailId in _thumbnails.Values)
                NativeMethods.DwmUnregisterThumbnail(thumbnailId);
            _thumbnails.Clear();
        }
    }

    /// <summary>
    /// Gets the thumbnail handle for a source window, or IntPtr.Zero if not registered.
    /// </summary>
    public IntPtr GetThumbnailId(IntPtr sourceHwnd)
    {
        lock (_lock)
            return _thumbnails.GetValueOrDefault(sourceHwnd);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            UnregisterAll();
            _disposed = true;
        }
    }
}
