using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using WinGlance.Models;
using WinGlance.NativeApi;
using WinGlance.ViewModels;

namespace WinGlance.Services;

/// <summary>
/// Orchestrates screenshot comparison and LLM analysis for tracked windows.
/// Call <see cref="EvaluateAsync"/> from the polling cycle to detect stale
/// windows and trigger LLM calls when needed.
/// </summary>
internal sealed class LlmService : IDisposable
{
    private const uint PW_RENDERFULLCONTENT = 0x00000002;

    private readonly ScreenshotComparer _comparer = new();
    private readonly HashSet<IntPtr> _pendingAnalysis = [];
    private LlmAnalyzer? _analyzer;
    private LlmConfig _config;

    /// <summary>
    /// Optional reference to the prompt editor for debug logging.
    /// Set after construction by the MainWindow.
    /// </summary>
    public PromptEditorViewModel? DebugLogger { get; set; }

    public LlmService(LlmConfig config)
    {
        _config = config;
        var prompt = string.IsNullOrWhiteSpace(config.Prompt)
            ? LlmConfig.DefaultPrompt
            : config.Prompt;
        if (config.Enabled)
            _analyzer = new LlmAnalyzer(config, prompt);
    }

    /// <summary>
    /// Evaluates a window: checks for staleness and triggers LLM analysis if needed.
    /// Updates <see cref="TrackedWindow.IsStale"/> and <see cref="TrackedWindow.LlmVerdict"/>.
    /// </summary>
    public async Task EvaluateAsync(TrackedWindow window)
    {
        if (!_config.Enabled)
            return;

        var isStale = _comparer.IsStale(window.Hwnd, _config.StaleThresholdSeconds);
        window.IsStale = isStale;

        if (!isStale)
        {
            // Content changed — reset verdict and allow re-analysis
            if (window.LlmVerdict is not null)
            {
                window.LlmVerdict = null;
                _pendingAnalysis.Remove(window.Hwnd);
            }
            return;
        }

        // Already analyzed or in-flight — skip
        if (window.LlmVerdict is not null || _pendingAnalysis.Contains(window.Hwnd))
            return;

        _pendingAnalysis.Add(window.Hwnd);

        try
        {
            var screenshot = CaptureWindow(window.Hwnd);
            if (screenshot is null || _analyzer is null)
            {
                _pendingAnalysis.Remove(window.Hwnd);
                return;
            }

            var (verdict, reason) = await _analyzer.AnalyzeWithReasonAsync(screenshot, window.Title);
            screenshot.Dispose();

            var finalVerdict = verdict ?? "idle";
            window.LlmVerdict = finalVerdict;

            // Emit debug log
            DebugLogger?.AppendLog(window.Title, finalVerdict, reason);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"LLM analysis failed for {window.Hwnd}: {ex.Message}");
            window.LlmVerdict = "idle";
            DebugLogger?.AppendLog(window.Title, "idle", $"Error: {ex.Message}");
        }
        finally
        {
            _pendingAnalysis.Remove(window.Hwnd);
        }
    }

    /// <summary>
    /// Removes tracking state for a window that is no longer monitored.
    /// </summary>
    public void Remove(IntPtr hwnd)
    {
        _comparer.Remove(hwnd);
        _pendingAnalysis.Remove(hwnd);
    }

    /// <summary>
    /// Reloads the prompt from the current config (called after Save in prompt editor).
    /// </summary>
    public void ReloadPrompt(string prompt)
    {
        var effectivePrompt = string.IsNullOrWhiteSpace(prompt) ? LlmConfig.DefaultPrompt : prompt;
        _analyzer?.Dispose();
        _analyzer = _config.Enabled ? new LlmAnalyzer(_config, effectivePrompt) : null;
    }

    public void Dispose()
    {
        _analyzer?.Dispose();
    }

    private static Bitmap? CaptureWindow(IntPtr hwnd)
    {
        var placement = new NativeMethods.WINDOWPLACEMENT { Length = (uint)Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>() };
        NativeMethods.GetWindowPlacement(hwnd, ref placement);
        var rect = placement.NormalPosition;
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
            return null;

        width = Math.Min(width, 1920);
        height = Math.Min(height, 1080);

        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        var hdc = g.GetHdc();
        var ok = NativeMethods.PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT);
        g.ReleaseHdc(hdc);

        if (!ok)
        {
            bmp.Dispose();
            return null;
        }

        return bmp;
    }
}
