namespace WinGlance.Models;

/// <summary>
/// Represents an application the user has chosen to monitor.
/// Equality is based on <see cref="ProcessName"/> (case-insensitive)
/// so that the same process is never added twice.
/// </summary>
public class MonitoredApp : IEquatable<MonitoredApp>
{
    /// <param name="processName">Process name without extension (e.g., "Code").</param>
    /// <param name="displayName">User-friendly name (e.g., "VS Code").</param>
    public MonitoredApp(string processName, string displayName)
    {
        ProcessName = processName;
        DisplayName = displayName;
    }

    /// <summary>Process name used for matching (case-insensitive). Immutable.</summary>
    public string ProcessName { get; }

    /// <summary>User-facing label shown in the Applications tab. Mutable.</summary>
    public string DisplayName { get; set; }

    public bool Equals(MonitoredApp? other) =>
        other is not null &&
        string.Equals(ProcessName, other.ProcessName, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as MonitoredApp);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(ProcessName);

    public override string ToString() => $"{DisplayName} ({ProcessName})";
}
