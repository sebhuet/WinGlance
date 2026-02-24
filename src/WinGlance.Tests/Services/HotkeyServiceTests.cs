using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class HotkeyServiceTests
{
    [Theory]
    [InlineData("Ctrl+Alt+G", 0x0003u, 0x47u)] // MOD_CONTROL | MOD_ALT, VK_G
    [InlineData("Ctrl+Shift+F1", 0x0006u, 0x70u)] // MOD_CONTROL | MOD_SHIFT, VK_F1
    [InlineData("Alt+G", 0x0001u, 0x47u)] // MOD_ALT, VK_G
    [InlineData("Ctrl+1", 0x0002u, 0x31u)] // MOD_CONTROL, VK_1
    public void TryParseHotkey_ValidInput_ReturnsTrue(string input, uint expectedMods, uint expectedVk)
    {
        var result = HotkeyService.TryParseHotkey(input, out var modifiers, out var vk);

        Assert.True(result);
        Assert.Equal(expectedMods, modifiers);
        Assert.Equal(expectedVk, vk);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Unknown+G")]
    public void TryParseHotkey_InvalidInput_ReturnsFalse(string input)
    {
        var result = HotkeyService.TryParseHotkey(input, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParseHotkey_Null_ReturnsFalse()
    {
        var result = HotkeyService.TryParseHotkey(null!, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParseHotkey_CaseInsensitive()
    {
        var result1 = HotkeyService.TryParseHotkey("ctrl+alt+g", out var mod1, out var vk1);
        var result2 = HotkeyService.TryParseHotkey("CTRL+ALT+G", out var mod2, out var vk2);

        Assert.True(result1);
        Assert.True(result2);
        Assert.Equal(mod1, mod2);
        Assert.Equal(vk1, vk2);
    }

    [Fact]
    public void TryParseHotkey_WithSpaces()
    {
        var result = HotkeyService.TryParseHotkey("Ctrl + Alt + G", out var modifiers, out var vk);

        Assert.True(result);
        Assert.Equal(0x0003u, modifiers); // MOD_CONTROL | MOD_ALT
        Assert.Equal(0x47u, vk); // VK_G
    }

    [Fact]
    public void TryParseHotkey_ControlAlias()
    {
        var result = HotkeyService.TryParseHotkey("Control+G", out var modifiers, out _);

        Assert.True(result);
        Assert.Equal(0x0002u, modifiers); // MOD_CONTROL
    }

    [Fact]
    public void TryParseHotkey_Win_Modifier()
    {
        var result = HotkeyService.TryParseHotkey("Win+G", out var modifiers, out _);

        Assert.True(result);
        Assert.Equal(0x0008u, modifiers); // MOD_WIN
    }
}
