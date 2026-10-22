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
/// Renders the Settings view offscreen with stand-in theme brushes, to check that controls
/// whose default Windows template is light follow the theme instead.
/// </summary>
public class SettingsTabTests
{
    [Fact]
    public void ProviderComboBox_ThemeInputBackground_DrawsItsBox()
    {
        RunOnSta(() =>
        {
            var inputBackground = new SolidColorBrush(Colors.DarkSlateGray);
            var config = new AppConfig();
            config.Llm.Enabled = true; // the provider fields only show with LLM analysis on
            using var preview = new PreviewViewModel();
            var viewModel = new SettingsViewModel(
                new ConfigService(Path.Combine(Path.GetTempPath(), $"WinGlance_Test_{Guid.NewGuid():N}")),
                config,
                preview,
                applyAutoStart: _ => { });

            var view = new SettingsTab { DataContext = viewModel };
            view.Resources["InputBackground"] = inputBackground;
            Layout(view, 600, 1400);

            var comboBox = FindDescendants<ComboBox>(view).Single();
            Assert.Contains(FindDescendants<Border>(comboBox), border => border.Background == inputBackground);
        });
    }
}
