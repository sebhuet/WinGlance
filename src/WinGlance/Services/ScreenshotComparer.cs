using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using WinGlance.NativeApi;

namespace WinGlance.Services;

/// <summary>
/// Captures window screenshots via <c>PrintWindow</c> and compares them using
/// a simple perceptual hash (average hash). Tracks when each window was last
/// seen to change so that stale windows can be detected.
/// </summary>
internal sealed class ScreenshotComparer
{
    private const int HashSize = 8; // 8×8 = 64-bit hash
    private const uint PW_RENDERFULLCONTENT = 0x00000002;

    private readonly Dictionary<IntPtr, ulong> _lastHash = [];
    private readonly Dictionary<IntPtr, DateTime> _lastChangeTime = [];

    /// <summary>
    /// Captures the window, computes a perceptual hash, and returns whether the
    /// window is stale (unchanged for longer than <paramref name="staleThresholdSeconds"/>).
    /// </summary>
    public bool IsStale(IntPtr hwnd, int staleThresholdSeconds)
    {
        var hash = CaptureAndHash(hwnd);
        if (hash is null)
            return false; // capture failed, treat as not stale

        var now = DateTime.UtcNow;

        if (_lastHash.TryGetValue(hwnd, out var prevHash))
        {
            if (hash.Value == prevHash)
            {
                // Content unchanged — check threshold
                if (!_lastChangeTime.ContainsKey(hwnd))
                    _lastChangeTime[hwnd] = now;

                return (now - _lastChangeTime[hwnd]).TotalSeconds >= staleThresholdSeconds;
            }
        }

        // Content changed or first capture — record and reset
        _lastHash[hwnd] = hash.Value;
        _lastChangeTime[hwnd] = now;
        return false;
    }

    /// <summary>
    /// Removes tracking data for a window that is no longer being monitored.
    /// </summary>
    public void Remove(IntPtr hwnd)
    {
        _lastHash.Remove(hwnd);
        _lastChangeTime.Remove(hwnd);
    }

    /// <summary>
    /// Captures the window into a bitmap, downscales to 8×8 grayscale,
    /// and returns a 64-bit average hash. Returns null if capture fails.
    /// </summary>
    internal static ulong? CaptureAndHash(IntPtr hwnd)
    {
        var placement = new NativeMethods.WINDOWPLACEMENT
        {
            Length = (uint)Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>(),
        };
        NativeMethods.GetWindowPlacement(hwnd, ref placement);
        var rect = placement.NormalPosition;
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
            return null;

        // Cap size to avoid excessive memory use
        width = Math.Min(width, 1920);
        height = Math.Min(height, 1080);

        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            var hdc = g.GetHdc();
            var ok = NativeMethods.PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT);
            g.ReleaseHdc(hdc);
            if (!ok)
                return null;
        }

        return ComputeAverageHash(bmp);
    }

    /// <summary>
    /// Average hash: downscale to 8×8 grayscale, compute mean brightness,
    /// set bits above the mean → 64-bit hash.
    /// </summary>
    internal static ulong ComputeAverageHash(Bitmap source)
    {
        using var small = new Bitmap(HashSize, HashSize, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
            g.DrawImage(source, 0, 0, HashSize, HashSize);
        }

        var pixels = new byte[HashSize * HashSize];
        long total = 0;

        var data = small.LockBits(
            new Rectangle(0, 0, HashSize, HashSize),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            for (var y = 0; y < HashSize; y++)
            {
                for (var x = 0; x < HashSize; x++)
                {
                    var offset = y * data.Stride + x * 4;
                    var b = Marshal.ReadByte(data.Scan0, offset);
                    var g2 = Marshal.ReadByte(data.Scan0, offset + 1);
                    var r = Marshal.ReadByte(data.Scan0, offset + 2);

                    // ITU-R BT.601 luma
                    var gray = (byte)(0.299 * r + 0.587 * g2 + 0.114 * b);
                    pixels[y * HashSize + x] = gray;
                    total += gray;
                }
            }
        }
        finally
        {
            small.UnlockBits(data);
        }

        var mean = total / (HashSize * HashSize);
        ulong hash = 0;
        for (var i = 0; i < pixels.Length; i++)
        {
            if (pixels[i] >= mean)
                hash |= 1UL << i;
        }

        return hash;
    }

    /// <summary>
    /// Hamming distance between two hashes (number of differing bits).
    /// </summary>
    internal static int HammingDistance(ulong a, ulong b)
    {
        var diff = a ^ b;
        var count = 0;
        while (diff != 0)
        {
            count++;
            diff &= diff - 1;
        }
        return count;
    }
}
