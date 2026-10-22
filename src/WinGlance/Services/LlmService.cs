using WinGlance.Models;
using WinGlance.ViewModels;

namespace WinGlance.Services;

/// <summary>
/// Orchestrates screenshot comparison and LLM analysis for tracked windows.
/// Call <see cref="EvaluateAsync"/> from the polling cycle to detect stale
/// windows and trigger LLM calls when needed. Thread-safe: evaluation runs on a
/// background thread while the polling thread removes closed windows.
/// </summary>
internal sealed class LlmService : IDisposable
{
    // A failed call (bad key, endpoint down) is retried after this delay, not on every tick
    internal static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    private readonly ScreenshotComparer _comparer = new();
    private readonly HashSet<IntPtr> _pendingAnalysis = [];
    private readonly Dictionary<IntPtr, DateTime> _retryAfter = [];
    private readonly object _stateLock = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly LlmConfig _config;
    private LlmAnalyzer? _analyzer;

    /// <summary>
    /// Optional reference to the prompt editor for debug logging.
    /// </summary>
    public PromptEditorViewModel? DebugLogger { get; set; }

    public LlmService(LlmConfig config)
    {
        _config = config;
        ReloadPrompt(config.Prompt);
    }

    /// <summary>
    /// Evaluates a window: checks for staleness and triggers LLM analysis if needed.
    /// Updates <see cref="TrackedWindow.IsStale"/> and <see cref="TrackedWindow.LlmVerdict"/>.
    /// </summary>
    public async Task EvaluateAsync(TrackedWindow window)
    {
        // PrintWindow sends WM_PRINT: a hung window would block the capture
        if (!_config.Enabled || window.IsHung || _cancellation.IsCancellationRequested)
            return;

        // Captured outside the lock: a capture can be slow
        var hash = ScreenshotComparer.CaptureAndHash(window.Hwnd);
        var now = DateTime.UtcNow;

        lock (_stateLock)
        {
            var isStale = _comparer.IsStale(window.Hwnd, hash, _config.StaleThresholdSeconds, now);
            window.IsStale = isStale;

            if (!isStale)
            {
                // Content changed — reset verdict and allow re-analysis
                window.LlmVerdict = null;
                _retryAfter.Remove(window.Hwnd);
                return;
            }

            // Already analyzed, in flight, or waiting to retry after a failure
            if (window.LlmVerdict is not null
                || _pendingAnalysis.Contains(window.Hwnd)
                || (_retryAfter.TryGetValue(window.Hwnd, out var retryAt) && now < retryAt))
                return;

            _pendingAnalysis.Add(window.Hwnd);
        }

        try
        {
            var result = await AnalyzeAsync(window).ConfigureAwait(false);
            if (result is null)
                return;

            if (result.Error is null)
            {
                window.LlmVerdict = result.Verdict;
            }
            else
            {
                lock (_stateLock)
                    _retryAfter[window.Hwnd] = DateTime.UtcNow + RetryDelay;
            }
        }
        finally
        {
            lock (_stateLock)
                _pendingAnalysis.Remove(window.Hwnd);
        }
    }

    /// <summary>
    /// Forces a single LLM analysis on a window, bypassing staleness and pending checks.
    /// Used by the "Run" button in the Prompt Editor for manual testing.
    /// </summary>
    public async Task ForceEvaluateAsync(TrackedWindow window)
    {
        var result = await AnalyzeAsync(window).ConfigureAwait(false);
        if (result is null)
            DebugLogger?.AppendLog(window.Title, "—", "Could not capture the window (minimized or closed)");
        else if (result.Error is null)
            window.LlmVerdict = result.Verdict;
    }

    /// <summary>
    /// Removes tracking state for a window that is no longer monitored.
    /// </summary>
    public void Remove(IntPtr hwnd)
    {
        lock (_stateLock)
        {
            _comparer.Remove(hwnd);
            _pendingAnalysis.Remove(hwnd);
            _retryAfter.Remove(hwnd);
        }
    }

    /// <summary>
    /// Uses a new system prompt from the next analysis on (called when the prompt is saved).
    /// </summary>
    public void ReloadPrompt(string prompt)
    {
        var effectivePrompt = string.IsNullOrWhiteSpace(prompt) ? LlmConfig.DefaultPrompt : prompt;
        _analyzer = new LlmAnalyzer(_config, effectivePrompt);
    }

    /// <summary>Cancels the calls in flight; the service does nothing afterwards.</summary>
    public void Dispose()
    {
        // Not disposed: a pass still running may read the token after this point
        _cancellation.Cancel();
    }

    /// <summary>
    /// Captures the window and asks the model, logging the outcome. Returns null when the
    /// window cannot be captured or the service was disposed.
    /// </summary>
    private async Task<LlmResult?> AnalyzeAsync(TrackedWindow window)
    {
        var analyzer = _analyzer;
        if (analyzer is null || _cancellation.IsCancellationRequested)
            return null;

        using var screenshot = ScreenshotComparer.CaptureWindow(window.Hwnd);
        if (screenshot is null)
            return null;

        LlmResult result;
        try
        {
            result = await analyzer.AnalyzeWithReasonAsync(screenshot, window.Title, _cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null; // service disposed (LLM turned off or settings changed)
        }

        if (result.Error is null)
        {
            DebugLogger?.AppendLog(window.Title, result.Verdict!, result.Reason);
        }
        else
        {
            Log.Warning($"LLM analysis failed for \"{window.Title}\": {result.Error}");
            DebugLogger?.AppendLog(window.Title, "error", result.Error);
        }

        return result;
    }
}
