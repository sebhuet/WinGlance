using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using WinGlance.NativeApi;
using WinGlance.Services;

namespace WinGlance.Controls;

/// <summary>
/// Custom control that hosts a DWM thumbnail for a source window.
/// Tracks its position within the parent window and updates the DWM
/// destination rect on layout/size changes.
/// </summary>
internal sealed class ThumbnailControl : FrameworkElement
{
    private IntPtr _thumbnailId;
    private ThumbnailManager? _manager;
    private Window? _hostWindow;

    // ── Dependency properties ───────────────────────────────────────────

    public static readonly DependencyProperty SourceHwndProperty =
        DependencyProperty.Register(
            nameof(SourceHwnd), typeof(IntPtr), typeof(ThumbnailControl),
            new PropertyMetadata(IntPtr.Zero, OnSourceHwndChanged));

    public static readonly DependencyProperty ThumbnailManagerProperty =
        DependencyProperty.Register(
            nameof(ThumbnailManager), typeof(ThumbnailManager), typeof(ThumbnailControl),
            new PropertyMetadata(null, OnManagerChanged));

    public IntPtr SourceHwnd
    {
        get => (IntPtr)GetValue(SourceHwndProperty);
        set => SetValue(SourceHwndProperty, value);
    }

    public ThumbnailManager? ThumbnailManager
    {
        get => (ThumbnailManager?)GetValue(ThumbnailManagerProperty);
        set => SetValue(ThumbnailManagerProperty, value);
    }

    // ── Lifecycle ───────────────────────────────────────────────────────

    public ThumbnailControl()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hostWindow = Window.GetWindow(this);
        RegisterAndUpdate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnregisterThumbnail();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateThumbnail();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue && IsLoaded)
        {
            // Re-register after becoming visible (e.g., returning to Preview tab).
            // The manager may have called UnregisterAll() while we were hidden.
            _thumbnailId = IntPtr.Zero;
            RegisterAndUpdate();
        }
        else if (!(bool)e.NewValue)
        {
            // Clear stale handle — the manager may unregister all while we're invisible.
            _thumbnailId = IntPtr.Zero;
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        // DWM paints the thumbnail directly — we just need to reserve the space.
        // Paint a transparent background so hit-testing works.
        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
    }

    // ── DWM integration ─────────────────────────────────────────────────

    private void RegisterAndUpdate()
    {
        _manager = ThumbnailManager;
        if (_manager is null || SourceHwnd == IntPtr.Zero || _hostWindow is null)
            return;

        _thumbnailId = _manager.Register(SourceHwnd);
        UpdateThumbnail();
    }

    private void UpdateThumbnail()
    {
        if (_thumbnailId == IntPtr.Zero || _manager is null || _hostWindow is null)
            return;

        // Get this control's position relative to the host window
        try
        {
            var transform = TransformToAncestor(_hostWindow);
            var topLeft = transform.Transform(new Point(0, 0));
            var bottomRight = transform.Transform(new Point(ActualWidth, ActualHeight));

            // Convert from WPF device-independent pixels to physical pixels
            var source = PresentationSource.FromVisual(_hostWindow);
            if (source?.CompositionTarget is null)
                return;

            var dpiX = source.CompositionTarget.TransformToDevice.M11;
            var dpiY = source.CompositionTarget.TransformToDevice.M22;

            var dest = new NativeMethods.RECT
            {
                Left = (int)(topLeft.X * dpiX),
                Top = (int)(topLeft.Y * dpiY),
                Right = (int)(bottomRight.X * dpiX),
                Bottom = (int)(bottomRight.Y * dpiY),
            };

            _manager.Update(_thumbnailId, dest);
        }
        catch (InvalidOperationException)
        {
            // TransformToAncestor can fail if the visual tree is disconnected
        }
    }

    private void UnregisterThumbnail()
    {
        if (_manager is not null && SourceHwnd != IntPtr.Zero)
        {
            _manager.Unregister(SourceHwnd);
        }
        _thumbnailId = IntPtr.Zero;
    }

    // ── Property change handlers ────────────────────────────────────────

    private static void OnSourceHwndChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ThumbnailControl control && control.IsLoaded)
        {
            control.UnregisterThumbnail();
            control.RegisterAndUpdate();
        }
    }

    private static void OnManagerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ThumbnailControl control && control.IsLoaded)
        {
            control.UnregisterThumbnail();
            control._manager = e.NewValue as ThumbnailManager;
            control.RegisterAndUpdate();
        }
    }
}
