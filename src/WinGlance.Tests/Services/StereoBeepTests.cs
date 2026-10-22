using System.Text;
using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class StereoBeepTests
{
    /// <summary>Sum of squared samples of each channel of a 16-bit stereo PCM WAV.</summary>
    private static (double Left, double Right) ChannelEnergy(byte[] wav)
    {
        double left = 0, right = 0;
        for (var offset = 44; offset + 3 < wav.Length; offset += 4)
        {
            left += Math.Pow(BitConverter.ToInt16(wav, offset), 2);
            right += Math.Pow(BitConverter.ToInt16(wav, offset + 2), 2);
        }
        return (left, right);
    }

    [Fact]
    public void CreateWav_WritesAStereo16BitPcmFile()
    {
        var wav = StereoBeep.CreateWav(0, frequency: 880, durationMs: 100);

        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal(1, BitConverter.ToInt16(wav, 20)); // PCM
        Assert.Equal(2, BitConverter.ToInt16(wav, 22)); // stereo
        Assert.Equal(44100, BitConverter.ToInt32(wav, 24));
        Assert.Equal(16, BitConverter.ToInt16(wav, 34));
        Assert.Equal(wav.Length - 44, BitConverter.ToInt32(wav, 40)); // data size
        Assert.Equal(4410 * 4, wav.Length - 44); // 100 ms
    }

    [Fact]
    public void CreateWav_Left_IsSilentOnTheRight()
    {
        var (left, right) = ChannelEnergy(StereoBeep.CreateWav(-1));

        Assert.True(left > 0);
        Assert.Equal(0, right);
    }

    [Fact]
    public void CreateWav_Right_IsSilentOnTheLeft()
    {
        var (left, right) = ChannelEnergy(StereoBeep.CreateWav(1));

        Assert.Equal(0, left);
        Assert.True(right > 0);
    }

    [Fact]
    public void CreateWav_Center_IsBalanced_AndAsLoudAsASide()
    {
        var (left, right) = ChannelEnergy(StereoBeep.CreateWav(0));
        var (sideLeft, _) = ChannelEnergy(StereoBeep.CreateWav(-1));

        Assert.Equal(left, right, precision: 0);
        // Constant-power panning: total energy does not depend on the position
        Assert.Equal(1, (left + right) / sideLeft, precision: 2);
    }

    [Fact]
    public void CreateWav_FadesInAndOut_ToAvoidClicks()
    {
        var wav = StereoBeep.CreateWav(-1);

        Assert.Equal(0, BitConverter.ToInt16(wav, 44)); // first left sample
        Assert.Equal(0, BitConverter.ToInt16(wav, wav.Length - 4)); // last left sample
    }
}
