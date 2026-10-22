using System.Text;
using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class StereoClickTests
{
    private const int SampleRate = 44100;

    /// <summary>Left or right samples of a 16-bit stereo PCM WAV.</summary>
    private static short[] Channel(byte[] wav, int channel) =>
        Enumerable.Range(0, (wav.Length - 44) / 4)
            .Select(i => BitConverter.ToInt16(wav, 44 + (i * 4) + (channel * 2)))
            .ToArray();

    private static double Energy(short[] samples) => samples.Sum(s => (double)s * s);

    [Fact]
    public void CreateWav_WritesAStereo16BitPcmFile()
    {
        var wav = StereoClick.CreateWav(0, durationMs: 100);

        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal(1, BitConverter.ToInt16(wav, 20)); // PCM
        Assert.Equal(2, BitConverter.ToInt16(wav, 22)); // stereo
        Assert.Equal(SampleRate, BitConverter.ToInt32(wav, 24));
        Assert.Equal(16, BitConverter.ToInt16(wav, 34));
        Assert.Equal(wav.Length - 44, BitConverter.ToInt32(wav, 40)); // data size
        Assert.Equal(4410 * 4, wav.Length - 44); // 100 ms
    }

    [Fact]
    public void CreateWav_IsAShortClick_NotABeep()
    {
        var samples = Channel(StereoClick.CreateWav(-1), channel: 0);

        Assert.True(samples.Length <= SampleRate * 30 / 1000, $"{samples.Length} samples is longer than 30 ms");
    }

    [Fact]
    public void CreateWav_HasASharpAttack_AndDiesOut()
    {
        var samples = Channel(StereoClick.CreateWav(-1), channel: 0);

        var peakIndex = Enumerable.Range(0, samples.Length).MaxBy(i => Math.Abs((int)samples[i]));
        Assert.InRange(peakIndex, 0, SampleRate / 1000); // loudest within the first millisecond

        // The last third is practically silent: a dry "tic", no ringing tail
        var third = samples.Length / 3;
        Assert.True(Energy(samples[^third..]) < Energy(samples[..third]) / 10000);
        Assert.Equal(0, samples[0]); // starts at zero: no pop
    }

    [Fact]
    public void CreateWav_Left_IsSilentOnTheRight()
    {
        var wav = StereoClick.CreateWav(-1);

        Assert.True(Energy(Channel(wav, 0)) > 0);
        Assert.Equal(0, Energy(Channel(wav, 1)));
    }

    [Fact]
    public void CreateWav_Right_IsSilentOnTheLeft()
    {
        var wav = StereoClick.CreateWav(1);

        Assert.Equal(0, Energy(Channel(wav, 0)));
        Assert.True(Energy(Channel(wav, 1)) > 0);
    }

    [Fact]
    public void CreateWav_Center_IsBalanced_AndAsLoudAsASide()
    {
        var center = StereoClick.CreateWav(0);
        var side = StereoClick.CreateWav(-1);
        var (left, right) = (Energy(Channel(center, 0)), Energy(Channel(center, 1)));

        Assert.Equal(1, left / right, precision: 3);
        // Constant-power panning: total energy does not depend on the position
        Assert.Equal(1, (left + right) / Energy(Channel(side, 0)), precision: 2);
    }
}
