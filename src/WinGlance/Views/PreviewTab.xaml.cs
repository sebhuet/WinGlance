using System.Windows;
using System.Windows.Controls;
using WinGlance.ViewModels;

namespace WinGlance.Views;

/// <summary>Preview view: the live thumbnails of the monitored windows.</summary>
public partial class PreviewTab : UserControl
{
    public PreviewTab()
    {
        InitializeComponent();

        // The "fit" layout sizes thumbnails from the space available to them. The outer size is
        // reported, not the viewport: a scrollbar appearing must not change the computed size,
        // or the layout could flip between two sizes.
        Scroller.SizeChanged += (_, e) => ReportSize(e.NewSize);
        DataContextChanged += (_, _) => ReportSize(Scroller.RenderSize);

        // Thumbnails are not reordered under the mouse: the user may be about to click one
        Scroller.MouseEnter += (_, _) => SetPointerOver(true);
        Scroller.MouseLeave += (_, _) => SetPointerOver(false);
    }

    private void SetPointerOver(bool isOver)
    {
        if (DataContext is PreviewViewModel viewModel)
            viewModel.IsPointerOver = isOver;
    }

    private void ReportSize(Size size)
    {
        // The vertical scrollbar's width is always kept free: if it showed up during a layout
        // pass, it would take one thumbnail per row away, add a row, and stay visible for good
        var usableWidth = size.Width - SystemParameters.VerticalScrollBarWidth;
        if (DataContext is PreviewViewModel viewModel && usableWidth > 0 && size.Height > 0)
            viewModel.SetViewportSize(new Size(usableWidth, size.Height));
    }
}
