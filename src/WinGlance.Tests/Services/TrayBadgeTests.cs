using System.Drawing;
using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class TrayBadgeTests
{
    [Theory]
    [InlineData(0, 0, "WinGlance")]
    [InlineData(1, 0, "WinGlance · 1 agent waiting for you")]
    [InlineData(2, 1, "WinGlance · 2 agents waiting for you, 1 finished")]
    [InlineData(0, 3, "WinGlance · 3 finished")]
    public void Describe_ListsWhatNeedsTheUser(int waiting, int finished, string expected)
    {
        Assert.Equal(expected, TrayBadge.Describe(waiting, finished));
    }

    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(1, 2, true)] // a waiting agent makes the badge red
    [InlineData(0, 2, false)]
    public void Create_BadgeIsRedWhenAnAgentWaits_OrangeOtherwise(int waiting, int finished, bool red)
    {
        using var icon = TrayBadge.Create(SystemIcons.Application, waiting, finished);
        using var bitmap = icon.ToBitmap();

        // Inside the badge circle (bottom right), left of the digits
        var pixel = bitmap.GetPixel(TrayBadge.IconSize - 19, TrayBadge.IconSize - 11);
        var expected = red ? TrayBadge.WaitingColor : TrayBadge.FinishedColor;

        Assert.Equal(TrayBadge.IconSize, icon.Width);
        Assert.Equal((expected.R, expected.G, expected.B), (pixel.R, pixel.G, pixel.B));
    }

    [Fact]
    public void Create_ManyAgents_DoesNotThrow()
    {
        using var icon = TrayBadge.Create(SystemIcons.Application, 12, 3); // drawn "9+"

        Assert.NotNull(icon);
    }
}
