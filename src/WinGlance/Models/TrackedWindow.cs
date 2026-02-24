using System.Windows.Media;

namespace WinGlance.Models;

/// <summary>
/// Represents a single window being tracked in the preview panel.
/// Identity is based on the window handle (HWND); mutable properties
/// (title, state flags) are updated on each polling cycle via data binding.
/// </summary>
public class TrackedWindow : ViewModels.ViewModelBase, IEquatable<TrackedWindow>
{
    private string _title = string.Empty;
    private ImageSource? _icon;
    private bool _isActive;
    private bool _isFlashing;
    private bool _isHung;
    private bool _isModalBlocked;
    private bool _isStale;
    private string? _llmVerdict;

    /// <summary>
    /// Creates a new tracked window with immutable identity properties.
    /// </summary>
    /// <param name="hwnd">Native window handle.</param>
    /// <param name="processName">Process name (e.g., "Code").</param>
    /// <param name="displayName">User-friendly name (e.g., "VS Code").</param>
    public TrackedWindow(IntPtr hwnd, string processName, string displayName)
    {
        Hwnd = hwnd;
        ProcessName = processName;
        DisplayName = displayName;
    }

    // ── Immutable identity ──────────────────────────────────────────────

    /// <summary>Native window handle (HWND). Used as the unique identifier.</summary>
    public IntPtr Hwnd { get; }

    /// <summary>Process name without extension (e.g., "Code", "notepad++").</summary>
    public string ProcessName { get; }

    /// <summary>User-friendly display name (e.g., "VS Code").</summary>
    public string DisplayName { get; }

    // ── Observable properties (updated each polling cycle) ─────────────

    /// <summary>Window title text, truncated in the UI if needed.</summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>Application icon extracted from the window or its class.</summary>
    public ImageSource? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    /// <summary>True if this window is the current foreground window.</summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    // ── Attention state (FR-9) ─────────────────────────────────────────

    /// <summary>True if the window is flashing in the taskbar (HSHELL_FLASH).</summary>
    public bool IsFlashing
    {
        get => _isFlashing;
        set => SetProperty(ref _isFlashing, value);
    }

    /// <summary>True if the window has stopped responding (IsHungAppWindow).</summary>
    public bool IsHung
    {
        get => _isHung;
        set => SetProperty(ref _isHung, value);
    }

    /// <summary>True if the window is disabled by a modal dialog (IsWindowEnabled == false).</summary>
    public bool IsModalBlocked
    {
        get => _isModalBlocked;
        set => SetProperty(ref _isModalBlocked, value);
    }

    // ── LLM analysis state (FR-10) ─────────────────────────────────────

    /// <summary>True if the window content has not changed for longer than the stale threshold.</summary>
    public bool IsStale
    {
        get => _isStale;
        set => SetProperty(ref _isStale, value);
    }

    /// <summary>
    /// "awaiting_action", "idle", or null (not yet analyzed).
    /// </summary>
    public string? LlmVerdict
    {
        get => _llmVerdict;
        set => SetProperty(ref _llmVerdict, value);
    }

    // ── Equality (by HWND) ──────────────────────────────────────────────

    public bool Equals(TrackedWindow? other) => other is not null && Hwnd == other.Hwnd;
    public override bool Equals(object? obj) => Equals(obj as TrackedWindow);
    public override int GetHashCode() => Hwnd.GetHashCode();
}
