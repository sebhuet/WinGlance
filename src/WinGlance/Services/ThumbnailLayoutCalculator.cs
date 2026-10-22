using System.Windows;

namespace WinGlance.Services;

/// <summary>
/// Sizes thumbnails for the "fit" layout: the largest size at which every tracked window
/// fits in the Preview area, as the view lays them out (rows wrapping at the panel width,
/// a header row per application group).
/// </summary>
internal static class ThumbnailLayoutCalculator
{
    // Space around each thumbnail in PreviewTab.xaml: Border margin 4+4, border 2+2, padding 2+2.
    // Keep in sync with the item template.
    internal const double ItemChromeWidth = 16;
    internal const double ItemFrameHeight = 16;

    // Badge/title row below the image, before scaling (see StatusRowScale)
    internal const double StatusRowHeight = 20;

    // The status row keeps its base size up to this thumbnail width, then grows with it
    internal const double StatusRowBaseWidth = 260;
    internal const double MaxStatusRowScale = 2.5;

    // Group header row (icon + app name), margins included
    internal const double GroupHeaderHeight = 26;

    // Kept free so that rounding never makes the last row overflow
    internal const double SafetyMargin = 4;

    internal const double MinThumbnailWidth = 80;
    internal const double DefaultAspectRatio = 16.0 / 10;

    /// <summary>
    /// Returns the largest thumbnail size (image area, without chrome) at which the groups fit
    /// in <paramref name="available"/>, or null when there is nothing to lay out.
    /// </summary>
    /// <param name="available">Size of the Preview area, in device-independent pixels.</param>
    /// <param name="groupSizes">Number of windows in each application group.</param>
    /// <param name="aspectRatio">Width / height of a thumbnail cell.</param>
    public static Size? Fit(Size available, IReadOnlyList<int> groupSizes, double aspectRatio)
    {
        var groups = groupSizes.Where(n => n > 0).ToList();
        if (groups.Count == 0 || available.Width <= 0 || available.Height <= 0)
            return null;

        if (!double.IsFinite(aspectRatio) || aspectRatio <= 0)
            aspectRatio = DefaultAspectRatio;

        // Fits() only gets harder as thumbnails grow: binary search for the largest width
        var low = 0;
        var high = (int)Math.Floor(available.Width - SafetyMargin - ItemChromeWidth);
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (Fits(available, groups, middle, aspectRatio))
                low = middle;
            else
                high = middle - 1;
        }

        var thumbnailWidth = Math.Max(MinThumbnailWidth, low);
        return new Size(thumbnailWidth, Math.Floor(thumbnailWidth / aspectRatio));
    }

    /// <summary>
    /// True if thumbnails of the given width fit, laid out as the view does: as many items per
    /// row as the width allows, every group starting a new row under its header.
    /// </summary>
    internal static bool Fits(Size available, IReadOnlyList<int> groupSizes, double thumbnailWidth, double aspectRatio)
    {
        if (thumbnailWidth < 1)
            return false;

        var perRow = (int)Math.Floor((available.Width - SafetyMargin) / (thumbnailWidth + ItemChromeWidth));
        if (perRow < 1)
            return false;

        var itemHeight = ItemHeight(thumbnailWidth, aspectRatio);
        var rows = groupSizes.Sum(n => (n + perRow - 1) / perRow);
        var height = (groupSizes.Count(n => n > 0) * GroupHeaderHeight) + (rows * itemHeight);
        return height <= available.Height - SafetyMargin;
    }

    /// <summary>
    /// Scale of the badge/title row under a thumbnail: 1 up to <see cref="StatusRowBaseWidth"/>,
    /// then proportional to the thumbnail width, so that the text stays readable on large
    /// thumbnails, up to <see cref="MaxStatusRowScale"/>.
    /// </summary>
    public static double StatusRowScale(double thumbnailWidth) =>
        double.IsFinite(thumbnailWidth)
            ? Math.Clamp(thumbnailWidth / StatusRowBaseWidth, 1, MaxStatusRowScale)
            : 1;

    /// <summary>Full height of one thumbnail item: image, frame and scaled status row.</summary>
    internal static double ItemHeight(double thumbnailWidth, double aspectRatio) =>
        Math.Floor(thumbnailWidth / aspectRatio) + ItemFrameHeight + (StatusRowHeight * StatusRowScale(thumbnailWidth));

    /// <summary>
    /// Cell aspect ratio for a set of windows: the median of their known ratios, so that the
    /// typical window fills its cell. Unknown ratios (0, minimized windows) are ignored.
    /// </summary>
    public static double TypicalAspectRatio(IEnumerable<double> aspectRatios)
    {
        var known = aspectRatios.Where(r => double.IsFinite(r) && r > 0).Order().ToList();
        if (known.Count == 0)
            return DefaultAspectRatio;

        var median = known.Count % 2 == 1
            ? known[known.Count / 2]
            : (known[(known.Count / 2) - 1] + known[known.Count / 2]) / 2;

        // Extreme ratios (a thin tool strip) would make every cell unusable
        return Math.Clamp(median, 0.5, 3.5);
    }
}
