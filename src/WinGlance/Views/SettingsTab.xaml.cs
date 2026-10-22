using System.Windows;
using System.Windows.Controls;
using WinGlance.ViewModels;

namespace WinGlance.Views;

/// <summary>Settings view: layout, thumbnail size, opacity, polling, general options, hotkeys, coding agents and LLM analysis.</summary>
public partial class SettingsTab : UserControl
{
    public SettingsTab()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // PasswordBox.Password is not a dependency property, so the API key cannot be data-bound:
    // it is copied in when the view model arrives and copied back on every change.
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        ApiKeyBox.Password = (DataContext as SettingsViewModel)?.LlmApiKey ?? string.Empty;
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
            viewModel.LlmApiKey = ApiKeyBox.Password;
    }
}
