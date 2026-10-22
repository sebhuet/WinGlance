using WinGlance.ViewModels;

namespace WinGlance.Tests.ViewModels;

public class RelayCommandTests
{
    [Fact]
    public void Execute_PassesTheParameterToTheAction()
    {
        object? received = null;
        var command = new RelayCommand(parameter => received = parameter);

        command.Execute("thumbnail");

        Assert.Equal("thumbnail", received);
    }

    [Fact]
    public void CanExecute_WithoutPredicate_IsAlwaysTrue()
    {
        var command = new RelayCommand(_ => { });

        Assert.True(command.CanExecute(null));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(null, false)]
    public void CanExecute_WithPredicate_AsksItForTheParameter(object? parameter, bool expected)
    {
        var command = new RelayCommand(_ => { }, p => p is not null);

        Assert.Equal(expected, command.CanExecute(parameter));
    }

    [Fact]
    public void CanExecuteChanged_SubscribeAndUnsubscribe_DoNotThrow()
    {
        // Relayed to CommandManager.RequerySuggested, which WPF raises when focus or input changes
        var command = new RelayCommand(_ => { });
        EventHandler handler = (_, _) => { };

        command.CanExecuteChanged += handler;
        command.CanExecuteChanged -= handler;
    }
}
