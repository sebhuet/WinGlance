namespace WinGlance.ViewModels;

/// <summary>
/// Top-level ViewModel that orchestrates the three tabs and holds shared state.
/// Bound to <see cref="MainWindow"/> as its DataContext.
/// </summary>
internal class MainViewModel : ViewModelBase
{
    private int _selectedTabIndex;

    public MainViewModel()
    {
        PreviewViewModel = new PreviewViewModel();
    }

    /// <summary>Index of the currently selected tab (0=Preview, 1=Applications, 2=Settings).</summary>
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    /// <summary>ViewModel for the Preview tab.</summary>
    public PreviewViewModel PreviewViewModel { get; }
}
