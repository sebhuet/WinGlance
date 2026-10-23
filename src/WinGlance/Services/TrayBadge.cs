using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using WinGlance.NativeApi;

namespace WinGlance.Services;

/// <summary>
/// Tray icon counter: the number of agents that need the user, drawn as a badge over the
/// application icon, and the matching tooltip. Visible even while the panel is hidden.
/// </summary>
internal static class TrayBadge
{
    /// <summary>Side of the icon drawn, in pixels; Windows scales it to the tray size.</summary>
    internal const int IconSize = 32;

    /// <summary>Badge color when an agent waits for an answer (the red of the waiting badge).</summary>
    internal static readonly Color WaitingColor = Color.FromArgb(0xE8, 0x11, 0x23);

    /// <summary>Badge color when agents only finished their turn (the orange of the finished badge).</summary>
    internal static readonly Color FinishedColor = Color.FromArgb(0xFF, 0x8C, 0x00);

    /// <summary>
    /// Returns the application icon with a badge showing <paramref name="waiting"/> +
    /// <paramref name="finished"/> ("9+" above 9), red when an agent waits for an answer,
    /// orange otherwise. The caller disposes the returned icon.
    /// </summary>
    public static Icon Create(Icon applicationIcon, int waiting, int finished)
    {
        var count = waiting + finished;
        using var bitmap = new Bitmap(IconSize, IconSize);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (var sized = new Icon(applicationIcon, IconSize, IconSize))
                g.DrawIcon(sized, new Rectangle(0, 0, IconSize, IconSize));

            // Bottom right, large enough to read at 16 px
            var badge = new Rectangle(IconSize - 22, IconSize - 22, 22, 22);
            using (var fill = new SolidBrush(waiting > 0 ? WaitingColor : FinishedColor))
                g.FillEllipse(fill, badge);
            using (var outline = new Pen(Color.White, 2))
                g.DrawEllipse(outline, badge);

            var text = count > 9 ? "9+" : count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            using var font = new Font("Segoe UI", text.Length > 1 ? 9f : 12f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, font, Brushes.White, badge, format);
        }

        // GetHicon creates a handle the Icon does not own: copy it, then free it
        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    /// <summary>Tooltip of the tray icon, e.g. "WinGlance · 2 agents waiting for you, 1 finished".</summary>
    public static string Describe(int waiting, int finished)
    {
        var parts = new List<string>();
        if (waiting > 0)
            parts.Add(waiting == 1 ? "1 agent waiting for you" : $"{waiting} agents waiting for you");
        if (finished > 0)
            parts.Add(finished == 1 ? "1 finished" : $"{finished} finished");
        return parts.Count == 0 ? "WinGlance" : "WinGlance · " + string.Join(", ", parts);
    }
}
