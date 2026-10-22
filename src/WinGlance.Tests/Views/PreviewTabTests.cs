using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinGlance.Models;
using WinGlance.ViewModels;
using WinGlance.Views;
using static WinGlance.Tests.Views.ViewTestHelpers;

namespace WinGlance.Tests.Views;

/// <summary>
/// Renders the Preview view offscreen (no window, so no DWM thumbnail is registered)
/// to check that the agent badge and border triggers resolve as intended.
/// </summary>
public class PreviewTabTests
{
    private static readonly Color Blue = Color.FromArgb(0xCC, 0x00, 0x78, 0xD7);
    private static readonly Color Orange = Color.FromArgb(0xCC, 0xFF, 0x8C, 0x00);
    private static readonly Color Teal = Color.FromArgb(0xCC, 0x00, 0x89, 0x7B);
    private static readonly Color Gray = Color.FromArgb(0xCC, 0x55, 0x55, 0x55);

    [Fact]
    public void Badge_NoAgent_IsCollapsed()
    {
        RunOnSta(() =>
        {
            var badge = RenderAndFindBadge(new TrackedWindow((IntPtr)1, "Code", "VS Code"));

            Assert.Equal(Visibility.Collapsed, badge.Visibility);
        });
    }

    [Theory]
    [InlineData(AgentStatus.Busy, "Claude · working")]
    [InlineData(AgentStatus.Waiting, "Claude · permission prompt")]
    [InlineData(AgentStatus.Running, "OpenCode · running")]
    [InlineData(AgentStatus.Idle, "Claude · idle")]
    public void Badge_WithAgent_ShowsItsTextInTheStatusColor(AgentStatus status, string text)
    {
        var expected = status switch
        {
            AgentStatus.Busy => Blue,
            AgentStatus.Waiting => Orange,
            AgentStatus.Running => Teal,
            AgentStatus.Idle => Orange, // finished, waiting for the next prompt: needs the user
            _ => Gray,
        };

        RunOnSta(() =>
        {
            var window = new TrackedWindow((IntPtr)1, "Code", "VS Code")
            {
                Agent = new AgentBadge(status, text, "details"),
            };

            var badge = RenderAndFindBadge(window);

            Assert.Equal(Visibility.Visible, badge.Visibility);
            Assert.Equal(expected, Assert.IsType<SolidColorBrush>(badge.Background).Color);
            Assert.Equal(text, Assert.IsType<TextBlock>(badge.Child).Text);
            Assert.Equal("details", badge.ToolTip);
        });
    }

    [Fact]
    public void Badges_SitOutsideTheThumbnailArea()
    {
        // DWM draws the thumbnail over WPF content: anything overlapping it would be hidden
        RunOnSta(() =>
        {
            var view = Render(new TrackedWindow((IntPtr)1, "Code", "VS Code") { IsHung = true });
            var thumbnail = FindDescendants<WinGlance.Controls.ThumbnailControl>(view).Single();
            var titleRow = FindDescendants<DockPanel>(view).Single();

            var thumbnailBottom = thumbnail.TranslatePoint(new Point(0, thumbnail.ActualHeight), view).Y;
            var rowTop = titleRow.TranslatePoint(new Point(0, 0), view).Y;

            Assert.True(rowTop >= thumbnailBottom, $"badge row at {rowTop} overlaps thumbnail ending at {thumbnailBottom}");
        });
    }

    [Theory]
    [InlineData(true, null, Visibility.Visible, Visibility.Collapsed)]
    [InlineData(false, "awaiting_action", Visibility.Collapsed, Visibility.Visible)]
    [InlineData(false, "idle", Visibility.Collapsed, Visibility.Collapsed)]
    public void StateBadges_FollowHungAndLlmVerdict(bool isHung, string? verdict, Visibility hungBadge, Visibility awaitingBadge)
    {
        RunOnSta(() =>
        {
            var window = new TrackedWindow((IntPtr)1, "Code", "VS Code") { IsHung = isHung, LlmVerdict = verdict };

            var titleRow = FindDescendants<DockPanel>(Render(window)).Single();

            Assert.Equal(hungBadge, titleRow.Children[1].Visibility);
            Assert.Equal(awaitingBadge, titleRow.Children[2].Visibility);
        });
    }

    [Theory]
    [InlineData("idle", 0.5)]
    [InlineData("awaiting_action", 1.0)]
    [InlineData(null, 1.0)]
    public void Thumbnail_IdleVerdict_DimsTheDwmImage(string? verdict, double expectedOpacity)
    {
        RunOnSta(() =>
        {
            var view = Render(new TrackedWindow((IntPtr)1, "Code", "VS Code") { LlmVerdict = verdict });

            var thumbnail = FindDescendants<WinGlance.Controls.ThumbnailControl>(view).Single();

            Assert.Equal(expectedOpacity, thumbnail.ThumbnailOpacity);
        });
    }

    [Fact]
    public void ThumbnailBorder_NeedsAgentAttention_IsOrange()
    {
        RunOnSta(() =>
        {
            var window = new TrackedWindow((IntPtr)1, "Code", "VS Code") { NeedsAgentAttention = true };

            var view = Render(window);
            var frame = FindDescendants<Border>(view).Single(b => b.Cursor == Cursors.Hand);

            Assert.Equal(Color.FromRgb(255, 140, 0), Assert.IsType<SolidColorBrush>(frame.BorderBrush).Color);
        });
    }

    // ── Fit layout ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(900, 560, 5, 0)]
    [InlineData(900, 560, 5, 3)] // two applications: two group headers
    [InlineData(1600, 900, 8, 2)]
    [InlineData(500, 700, 3, 1)]
    public void FitLayout_FillsThePanelWithoutScrolling(double width, double height, int codeWindows, int terminalWindows)
    {
        RunOnSta(() =>
        {
            var viewModel = new PreviewViewModel { Layout = PreviewViewModel.FitLayout };
            for (var i = 0; i < codeWindows; i++)
                viewModel.Windows.Add(new TrackedWindow((IntPtr)(i + 1), "Code", "VS Code") { AspectRatio = 1.84 });
            for (var i = 0; i < terminalWindows; i++)
                viewModel.Windows.Add(new TrackedWindow((IntPtr)(100 + i), "WindowsTerminal", "Terminal") { AspectRatio = 1.6 });

            var view = new PreviewTab { DataContext = viewModel };
            for (var pass = 0; pass < 3; pass++) // size reported → thumbnails resized → layout again
            {
                view.Measure(new Size(width, height));
                view.Arrange(new Rect(0, 0, width, height));
                view.UpdateLayout();
            }

            var scroller = FindDescendants<ScrollViewer>(view).First();
            Assert.True(scroller.ExtentHeight <= scroller.ViewportHeight,
                $"content {scroller.ExtentHeight} px high in a {scroller.ViewportHeight} px panel");
            Assert.True(scroller.ExtentWidth <= scroller.ViewportWidth);
            // ...and the space is used: thumbnails are larger than the 200 px fixed default
            Assert.True(viewModel.DisplayThumbnailWidth > 200, $"thumbnails only {viewModel.DisplayThumbnailWidth} px wide");
        });
    }

    [Fact]
    public void FitLayout_LargeThumbnails_ScaleTheStatusRowText()
    {
        RunOnSta(() =>
        {
            var viewModel = new PreviewViewModel { Layout = PreviewViewModel.FitLayout };
            viewModel.Windows.Add(new TrackedWindow((IntPtr)1, "Code", "VS Code") { AspectRatio = 1.84, Title = "Readme - Project" });

            var view = new PreviewTab { DataContext = viewModel };
            for (var pass = 0; pass < 3; pass++)
            {
                view.Measure(new Size(1600, 900));
                view.Arrange(new Rect(0, 0, 1600, 900));
                view.UpdateLayout();
            }

            var row = FindDescendants<DockPanel>(view).Single();
            var scale = Assert.IsType<ScaleTransform>(row.LayoutTransform);
            Assert.True(viewModel.StatusRowScale > 1.5);
            Assert.Equal(viewModel.StatusRowScale, scale.ScaleX, precision: 6);
            Assert.Equal(viewModel.StatusRowScale, scale.ScaleY, precision: 6);
            // Scaled, the row spans exactly the thumbnail width
            Assert.Equal(viewModel.DisplayThumbnailWidth, row.ActualWidth * scale.ScaleX, precision: 3);
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static Border RenderAndFindBadge(TrackedWindow window)
    {
        var titleRow = FindDescendants<DockPanel>(Render(window)).Single();
        return Assert.IsType<Border>(titleRow.Children[0]);
    }

    private static PreviewTab Render(TrackedWindow window)
    {
        var viewModel = new PreviewViewModel();
        viewModel.Windows.Add(window);

        var view = new PreviewTab { DataContext = viewModel };
        Layout(view, 800, 600);
        return view;
    }
}
