namespace WinGlance.ViewModels;

public class MainViewModel : ViewModelBase
{
    private int _selectedTabIndex;

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }
}
