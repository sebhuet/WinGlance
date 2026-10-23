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
    private AgentBadge? _agent;
    private bool _needsAgentAttention;
    private string _statusAge = string.Empty;
    private bool _isEscalated;
    private string? _displayText;

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

    /// <summary>ID of the process owning the window; links agent processes to their window.</summary>
    public uint ProcessId { get; init; }

    /// <summary>
    /// Width / height of the window's client area (what the thumbnail shows); 0 when unknown,
    /// e.g. while minimized. Sizes the cells of the "fit" layout.
    /// </summary>
    public double AspectRatio { get; set; }

    // ── Observable properties (updated each polling cycle) ─────────────

    /// <summary>Window title text, truncated in the UI if needed.</summary>
    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value) && _displayText is null)
                OnPropertyChanged(nameof(DisplayText));
        }
    }

    /// <summary>
    /// Text shown under the thumbnail, built from the title template of the settings
    /// (<see cref="Services.WindowTitleFormat"/>); the title until a template is applied.
    /// </summary>
    public string DisplayText
    {
        get => _displayText ?? _title;
        set => SetProperty(ref _displayText, value);
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

    // ── Coding agents (FR-11) ──────────────────────────────────────────

    /// <summary>Claude Code / OpenCode sessions hosted by this window, or null when there are none.</summary>
    public AgentBadge? Agent
    {
        get => _agent;
        set => SetProperty(ref _agent, value);
    }

    /// <summary>
    /// True when an agent waits for the user, or finished a turn the user has not looked at yet.
    /// </summary>
    public bool NeedsAgentAttention
    {
        get => _needsAgentAttention;
        set => SetProperty(ref _needsAgentAttention, value);
    }

    // ── Status time and reminders ──────────────────────────────────────

    /// <summary>
    /// When the window's status last changed (UTC): agent status, or attention state for a
    /// window without agent (see <see cref="Services.WindowUrgency.StatusKey"/>).
    /// </summary>
    public DateTime StatusSince { get; set; }

    /// <summary>Status the <see cref="StatusSince"/> time refers to.</summary>
    internal string? StatusKey { get; set; }

    /// <summary>Time since the last status change, e.g. "12 min"; empty when not shown.</summary>
    public string StatusAge
    {
        get => _statusAge;
        set => SetProperty(ref _statusAge, value);
    }

    /// <summary>True when an agent has waited for an answer longer than the reminder delay: the border blinks.</summary>
    public bool IsEscalated
    {
        get => _isEscalated;
        set => SetProperty(ref _isEscalated, value);
    }

    /// <summary>True once the reminder notification of the current waiting period has been raised.</summary>
    internal bool ReminderRaised { get; set; }

    // ── Equality (by HWND) ──────────────────────────────────────────────

    public bool Equals(TrackedWindow? other) => other is not null && Hwnd == other.Hwnd;
    public override bool Equals(object? obj) => Equals(obj as TrackedWindow);
    public override int GetHashCode() => Hwnd.GetHashCode();
}
