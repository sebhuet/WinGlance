using System.IO;
using System.Media;

namespace WinGlance.Services;

/// <summary>
/// Plays a short, dry click placed in the stereo field: heard on the left, in the center or on
/// the right. A click rather than a tone, so as not to disturb people nearby. The sound is
/// generated in memory, so no audio file ships with the app. UI thread only.
/// </summary>
internal sealed class StereoClick : IDisposable
{
    private const int SampleRate = 44100;

    // A 2 kHz sine damped within a few milliseconds sounds like a "tic": sharp attack, no tail
    private const int Frequency = 2000;
    private const double DecaySeconds = 0.0025;
    private const int DurationMs = 25;
    private const double Volume = 0.3;

    // One player per position, kept alive: an asynchronous play reads from the player's buffer
    private readonly Dictionary<double, SoundPlayer> _players = [];

    /// <summary>Plays the click without waiting for it to end.</summary>
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
    /// Builds a 16-bit stereo PCM WAV file of an exponentially damped sine. It starts at zero
    /// (sin 0) and dies out on its own, so it needs no fades. Constant-power panning: the left
    /// and right gains are cos θ and sin θ with θ from 0 (left) to π/2 (right), so a centered
    /// click sounds as loud as a side one.
    /// </summary>
    internal static byte[] CreateWav(double pan, int durationMs = DurationMs)
    {
        var angle = (Math.Clamp(pan, -1, 1) + 1) * Math.PI / 4;
        var leftGain = Math.Cos(angle);
        var rightGain = Math.Sin(angle);

        var samples = SampleRate * durationMs / 1000;
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
            var t = (double)i / SampleRate;
            var value = Math.Sin(2 * Math.PI * Frequency * t) * Math.Exp(-t / DecaySeconds) * Volume * short.MaxValue;
            writer.Write((short)Math.Round(value * leftGain));
            writer.Write((short)Math.Round(value * rightGain));
        }

        return stream.ToArray();
    }
}
