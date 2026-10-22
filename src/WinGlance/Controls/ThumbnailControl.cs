using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinGlance.NativeApi;
using WinGlance.Services;

namespace WinGlance.Controls;

/// <summary>
/// Destination and source rectangles of one DWM thumbnail, in physical pixels.
/// </summary>
/// <param name="Destination">Where the thumbnail is drawn in the host window.</param>
/// <param name="Source">Part of the source window to draw; null draws all of it.</param>
/// <param name="Visible">False when the thumbnail lies entirely outside the visible area.</param>
internal readonly record struct ThumbnailLayout(NativeMethods.RECT Destination, NativeMethods.RECT? Source, bool Visible);

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
    private ThumbnailLayout? _lastLayout;
    private byte _lastOpacity;

    // ── Dependency properties ───────────────────────────────────────────

    /// <summary>Handle of the window whose thumbnail is drawn; changing it registers a new thumbnail.</summary>
    public static readonly DependencyProperty SourceHwndProperty =
        DependencyProperty.Register(
            nameof(SourceHwnd), typeof(IntPtr), typeof(ThumbnailControl),
            new PropertyMetadata(IntPtr.Zero, OnSourceHwndChanged));

    /// <summary>Service that registers the DWM thumbnails; the control draws nothing until it is set.</summary>
    public static readonly DependencyProperty ThumbnailManagerProperty =
        DependencyProperty.Register(
            nameof(ThumbnailManager), typeof(ThumbnailManager), typeof(ThumbnailControl),
            new PropertyMetadata(null, OnManagerChanged));

    /// <summary>
    /// Opacity of the DWM thumbnail (0–1). WPF's own <see cref="UIElement.Opacity"/> has no
    /// effect on it: DWM draws the thumbnail above the WPF content.
    /// </summary>
    public static readonly DependencyProperty ThumbnailOpacityProperty =
        DependencyProperty.Register(
            nameof(ThumbnailOpacity), typeof(double), typeof(ThumbnailControl),
            new PropertyMetadata(1.0, OnThumbnailOpacityChanged));

    /// <summary>Window shown by the thumbnail (<see cref="SourceHwndProperty"/>).</summary>
    public IntPtr SourceHwnd
    {
        get => (IntPtr)GetValue(SourceHwndProperty);
        set => SetValue(SourceHwndProperty, value);
    }

    /// <summary>Service used to register the thumbnail (<see cref="ThumbnailManagerProperty"/>).</summary>
    public ThumbnailManager? ThumbnailManager
    {
        get => (ThumbnailManager?)GetValue(ThumbnailManagerProperty);
        set => SetValue(ThumbnailManagerProperty, value);
    }

    /// <summary>Opacity of the DWM thumbnail, 0-1 (<see cref="ThumbnailOpacityProperty"/>).</summary>
    public double ThumbnailOpacity
    {
        get => (double)GetValue(ThumbnailOpacityProperty);
        set => SetValue(ThumbnailOpacityProperty, value);
    }

    // ── Lifecycle ───────────────────────────────────────────────────────

    /// <summary>Hooks the load, size and visibility events that register, move and hide the thumbnail.</summary>
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
        // Any layout pass can move this control without resizing it (an item removed
        // before it, scrolling): the DWM rect must follow
        LayoutUpdated += OnLayoutUpdated;
        RegisterAndUpdate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;
        UnregisterThumbnail();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateThumbnail();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        UpdateThumbnail();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // The manager may unregister all thumbnails while we are hidden: forget the handle
        _thumbnailId = IntPtr.Zero;
        _lastLayout = null;

        if ((bool)e.NewValue && IsLoaded)
            RegisterAndUpdate(); // e.g., returning to the Preview view
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
        // Hidden views keep their items loaded: never draw a thumbnail for them
        if (_manager is null || SourceHwnd == IntPtr.Zero || _hostWindow is null || !IsVisible)
            return;

        _thumbnailId = _manager.Register(SourceHwnd);
        _lastLayout = null;
        UpdateThumbnail();
    }

    private void UpdateThumbnail()
    {
        if (_thumbnailId == IntPtr.Zero || _manager is null || _hostWindow is null)
            return;

        try
        {
            var source = PresentationSource.FromVisual(_hostWindow);
            if (source?.CompositionTarget is null)
                return;

            // From device-independent pixels relative to the host window to physical pixels
            var toDevice = source.CompositionTarget.TransformToDevice;
            var bounds = Rect.Transform(
                TransformToAncestor(_hostWindow).TransformBounds(new Rect(0, 0, ActualWidth, ActualHeight)),
                toDevice);

            Rect? viewport = null;
            if (FindAncestor<ScrollViewer>(this) is { } scrollViewer)
            {
                viewport = Rect.Transform(
                    scrollViewer.TransformToAncestor(_hostWindow)
                        .TransformBounds(new Rect(0, 0, scrollViewer.ViewportWidth, scrollViewer.ViewportHeight)),
                    toDevice);
            }

            var sourceSize = _manager.QuerySourceSize(_thumbnailId, out var size)
                ? new Size(size.Width, size.Height)
                : Size.Empty;

            var layout = ComputeLayout(bounds, sourceSize, viewport);
            var opacity = (byte)Math.Round(Math.Clamp(ThumbnailOpacity, 0, 1) * 255);

            // LayoutUpdated fires for any layout pass in the window: skip unchanged updates
            if (layout == _lastLayout && opacity == _lastOpacity)
                return;

            if (_manager.Update(_thumbnailId, layout.Destination, layout.Source, opacity, layout.Visible))
            {
                _lastLayout = layout;
                _lastOpacity = opacity;
            }
        }
        catch (InvalidOperationException)
        {
            // TransformToAncestor can fail if the visual tree is disconnected
        }
    }

    /// <summary>
    /// Fits the source into the control bounds, keeping its aspect ratio and centering it,
    /// then clips the result to the scroll viewport: DWM thumbnails are not clipped by WPF,
    /// so a partly scrolled-out thumbnail would otherwise draw over the rest of the panel.
    /// </summary>
    /// <param name="bounds">Control bounds in the host window, in physical pixels.</param>
    /// <param name="sourceSize">Source size reported by DWM; <see cref="Size.Empty"/> if unknown.</param>
    /// <param name="viewport">Visible area of the enclosing scroll viewer, or null if none.</param>
    internal static ThumbnailLayout ComputeLayout(Rect bounds, Size sourceSize, Rect? viewport)
    {
        var hasSourceSize = !sourceSize.IsEmpty && sourceSize.Width > 0 && sourceSize.Height > 0;
        var destination = bounds;

        if (hasSourceSize && bounds.Width > 0 && bounds.Height > 0)
        {
            var scale = Math.Min(bounds.Width / sourceSize.Width, bounds.Height / sourceSize.Height);
            var width = sourceSize.Width * scale;
            var height = sourceSize.Height * scale;
            destination = new Rect(
                bounds.X + ((bounds.Width - width) / 2),
                bounds.Y + ((bounds.Height - height) / 2),
                width,
                height);
        }

        if (viewport is not { } clip || clip.Contains(destination))
            return new ThumbnailLayout(ToRect(destination), null, Visible: true);

        var visiblePart = Rect.Intersect(destination, clip);
        if (visiblePart.IsEmpty || visiblePart.Width < 1 || visiblePart.Height < 1 || !hasSourceSize)
            return new ThumbnailLayout(ToRect(destination), null, Visible: false);

        // Show only the matching part of the source
        var scaleX = sourceSize.Width / destination.Width;
        var scaleY = sourceSize.Height / destination.Height;
        var sourcePart = new Rect(
            (visiblePart.X - destination.X) * scaleX,
            (visiblePart.Y - destination.Y) * scaleY,
            visiblePart.Width * scaleX,
            visiblePart.Height * scaleY);

        return new ThumbnailLayout(ToRect(visiblePart), ToRect(sourcePart), Visible: true);
    }

    // Midpoints round away from zero: banker's rounding would turn 62.5–137.5 into 62–138
    private static NativeMethods.RECT ToRect(Rect rect) => new()
    {
        Left = (int)Math.Round(rect.Left, MidpointRounding.AwayFromZero),
        Top = (int)Math.Round(rect.Top, MidpointRounding.AwayFromZero),
        Right = (int)Math.Round(rect.Right, MidpointRounding.AwayFromZero),
        Bottom = (int)Math.Round(rect.Bottom, MidpointRounding.AwayFromZero),
    };

    private static T? FindAncestor<T>(DependencyObject element) where T : DependencyObject
    {
        for (var current = VisualTreeHelper.GetParent(element); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match)
                return match;
        }

        return null;
    }

    private void UnregisterThumbnail()
    {
        if (_manager is not null && SourceHwnd != IntPtr.Zero)
        {
            _manager.Unregister(SourceHwnd);
        }
        _thumbnailId = IntPtr.Zero;
        _lastLayout = null;
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

    private static void OnThumbnailOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ThumbnailControl control)
            control.UpdateThumbnail();
    }
}
