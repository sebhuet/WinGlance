using System.IO;
using System.Media;

namespace WinGlance.Services;

/// <summary>
/// Plays a short tone placed in the stereo field: heard on the left, in the center or on the
/// right. The sound is generated in memory, so no audio file ships with the app. UI thread only.
/// </summary>
internal sealed class StereoBeep : IDisposable
{
    private const int SampleRate = 44100;
    private const int Frequency = 880;
    private const int DurationMs = 180;
    private const int FadeMs = 12; // avoids clicks at the start and end of the tone
    private const double Volume = 0.35;

    // One player per position, kept alive: an asynchronous play reads from the player's buffer
    private readonly Dictionary<double, SoundPlayer> _players = [];

    /// <summary>Plays the tone without waiting for it to end.</summary>
    /// <param name="pan">-1 (left) to +1 (right).</param>
    public void Play(double pan)
    {
        pan = Math.Round(Math.Clamp(double.IsFinite(pan) ? pan : 0, -1, 1), 2);

        try
        {
            if (!_players.TryGetValue(pan, out var player))
            {
                player = new SoundPlayer(new MemoryStream(CreateWav(pan)));
                player.Load();
                _players[pan] = player;
            }

            player.Play();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            Log.Warning("Could not play the switch sound", ex);
        }
    }

    public void Dispose()
    {
        foreach (var player in _players.Values)
            player.Dispose();
        _players.Clear();
    }

    /// <summary>
    /// Builds a 16-bit stereo PCM WAV file of a sine tone. Constant-power panning: the left and
    /// right gains are cos θ and sin θ with θ from 0 (left) to π/2 (right), so a centered tone
    /// sounds as loud as a side one.
    /// </summary>
    internal static byte[] CreateWav(double pan, int frequency = Frequency, int durationMs = DurationMs)
    {
        var angle = (Math.Clamp(pan, -1, 1) + 1) * Math.PI / 4;
        var leftGain = Math.Cos(angle);
        var rightGain = Math.Sin(angle);

        var samples = SampleRate * durationMs / 1000;
        var fadeSamples = SampleRate * FadeMs / 1000;
        const int channels = 2;
        const int bytesPerSample = 2;
        var dataSize = samples * channels * bytesPerSample;

        using var stream = new MemoryStream(44 + dataSize);
        using var writer = new BinaryWriter(stream);

        // RIFF header and "fmt " chunk (PCM)
        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(SampleRate);
        writer.Write(SampleRate * channels * bytesPerSample);
        writer.Write((short)(channels * bytesPerSample));
        writer.Write((short)(bytesPerSample * 8));

        writer.Write("data"u8);
        writer.Write(dataSize);
        for (var i = 0; i < samples; i++)
        {
            var envelope = Math.Min(1, Math.Min(i, samples - 1 - i) / (double)fadeSamples);
            var value = Math.Sin(2 * Math.PI * frequency * i / SampleRate) * Volume * envelope * short.MaxValue;
            writer.Write((short)Math.Round(value * leftGain));
            writer.Write((short)Math.Round(value * rightGain));
        }

        return stream.ToArray();
    }
}
