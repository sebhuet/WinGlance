using WinGlance.ViewModels;

namespace WinGlance.Tests.ViewModels;

public class MainViewModelTests
{
    [Fact]
    public void SelectedTabIndex_DefaultsToZero()
    {
        var vm = new MainViewModel();

        Assert.Equal(0, vm.SelectedTabIndex);
    }

    [Fact]
    public void SelectedTabIndex_RaisesPropertyChanged()
    {
        var vm = new MainViewModel();
        var raised = false;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedTabIndex))
                raised = true;
        };

        vm.SelectedTabIndex = 2;

        Assert.True(raised);
        Assert.Equal(2, vm.SelectedTabIndex);
    }

    [Fact]
    public void SelectedTabIndex_DoesNotRaise_WhenSameValue()
    {
        var vm = new MainViewModel();
        var raised = false;

        vm.PropertyChanged += (_, _) => raised = true;

        vm.SelectedTabIndex = 0;

        Assert.False(raised);
    }
}
