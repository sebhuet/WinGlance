using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;

namespace WinGlance.Tests.Views;

/// <summary>
/// Shared helpers for the tests that render WPF views offscreen.
/// </summary>
internal static class ViewTestHelpers
{
    /// <summary>Measures and arranges a view at the given size, so that its templates are applied.</summary>
    public static void Layout(UIElement view, double width, double height)
    {
        view.Measure(new Size(width, height));
        view.Arrange(new Rect(0, 0, width, height));
        view.UpdateLayout();
    }

    /// <summary>Every visual descendant of the given type, depth first.</summary>
    public static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;
            foreach (var descendant in FindDescendants<T>(child))
                yield return descendant;
        }
    }

    /// <summary>
    /// Runs the action on an STA thread and rethrows its exception: WPF elements must be
    /// created on an STA thread, and xUnit runs tests on MTA threads.
    /// </summary>
    public static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }
}
