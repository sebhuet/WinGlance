using WinGlance.ViewModels;

namespace WinGlance.Tests.ViewModels;

public class ViewModelBaseTests
{
    private sealed class TestViewModel : ViewModelBase
    {
        private string _name = string.Empty;
        private int _count;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public int Count
        {
            get => _count;
            set => SetProperty(ref _count, value);
        }
    }

    [Fact]
    public void SetProperty_RaisesPropertyChanged()
    {
        var vm = new TestViewModel();
        var raised = false;
        var raisedName = string.Empty;

        vm.PropertyChanged += (_, e) =>
        {
            raised = true;
            raisedName = e.PropertyName;
        };

        vm.Name = "test";

        Assert.True(raised);
        Assert.Equal("Name", raisedName);
        Assert.Equal("test", vm.Name);
    }

    [Fact]
    public void SetProperty_DoesNotRaise_WhenValueUnchanged()
    {
        var vm = new TestViewModel { Name = "test" };
        var raised = false;

        vm.PropertyChanged += (_, _) => raised = true;

        vm.Name = "test";

        Assert.False(raised);
    }

    [Fact]
    public void SetProperty_ReturnsFalse_WhenValueUnchanged()
    {
        var vm = new TestViewModel { Count = 5 };
        var raised = false;

        vm.PropertyChanged += (_, _) => raised = true;

        vm.Count = 5;

        Assert.False(raised);
    }

    [Fact]
    public void SetProperty_TracksMultipleProperties()
    {
        var vm = new TestViewModel();
        var changedProperties = new List<string>();

        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        vm.Name = "hello";
        vm.Count = 42;

        Assert.Equal(["Name", "Count"], changedProperties);
    }
}
