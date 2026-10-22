using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using WinGlance.Models;
using WinGlance.Services;
using WinGlance.ViewModels;
using WinGlance.Views;
using static WinGlance.Tests.Views.ViewTestHelpers;

namespace WinGlance.Tests.Views;

/// <summary>
/// Renders the Applications view offscreen with stand-in theme brushes.
/// </summary>
public class ApplicationsTabTests
{
    [Fact]
    public void ColumnHeaders_ThemeTitleBarBackground_AreDrawnWithIt()
    {
        RunOnSta(() =>
        {
            var headerBackground = new SolidColorBrush(Colors.DarkSlateGray);
            var viewModel = new ApplicationsViewModel(
                new ConfigService(Path.Combine(Path.GetTempPath(), $"WinGlance_Test_{Guid.NewGuid():N}")),
                new AppConfig(),
                _ => { },
                () => [new DiscoveredApp("Code", 2)]);
            viewModel.Refresh();

            var view = new ApplicationsTab { DataContext = viewModel };
            view.Resources["TitleBarBackground"] = headerBackground;
            Layout(view, 600, 400);

            // The named columns and the filler header after them
            var headers = FindDescendants<GridViewColumnHeader>(view).ToList();
            Assert.NotEmpty(headers);
            Assert.All(headers, header => Assert.Same(headerBackground, header.Background));
        });
    }
}
