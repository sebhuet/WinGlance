using WinGlance.NativeApi;

namespace WinGlance.Services;

/// <summary>
/// Process ID, parent process ID and executable name of one running process.
/// </summary>
internal sealed record ProcessInfo(int ProcessId, int ParentProcessId, string ExeName);

/// <summary>
/// Immutable snapshot of the parent/child relationships between running processes,
/// used to find which window hosts an agent process.
/// </summary>
internal sealed class ProcessTree
{
    // Bounds the walk: a parent PID recycled by a younger process can create a cycle
    private const int MaxDepth = 32;

    private readonly Dictionary<int, ProcessInfo> _byId = [];
    private readonly Dictionary<int, List<ProcessInfo>> _childrenById = [];

    public ProcessTree(IEnumerable<ProcessInfo> processes)
    {
        foreach (var process in processes)
            _byId[process.ProcessId] = process;

        foreach (var process in _byId.Values)
        {
            if (!_childrenById.TryGetValue(process.ParentProcessId, out var children))
                _childrenById[process.ParentProcessId] = children = [];
            children.Add(process);
        }
    }

    /// <summary>Direct children of a process (empty if none or unknown).</summary>
    public IReadOnlyList<ProcessInfo> GetChildren(int processId) =>
        _childrenById.TryGetValue(processId, out var children) ? children : [];

    /// <summary>All processes in the snapshot.</summary>
    public IEnumerable<ProcessInfo> Processes => _byId.Values;

    /// <summary>Captures the processes currently running on the machine.</summary>
    public static ProcessTree Capture() =>
        new(NativeMethods.GetProcessEntries()
            .Select(e => new ProcessInfo((int)e.ProcessId, (int)e.ParentProcessId, e.ExeFile)));

    /// <summary>
    /// Returns the process followed by its ancestors, nearest first. The walk stops at the
    /// first parent missing from the snapshot (exited), at PID 0, or on a cycle.
    /// </summary>
    public List<int> GetLineage(int processId)
    {
        var lineage = new List<int>();
        var visited = new HashSet<int>();
        var current = processId;

        while (lineage.Count < MaxDepth && current != 0 && visited.Add(current))
        {
            lineage.Add(current);
            if (!_byId.TryGetValue(current, out var info))
                break;
            current = info.ParentProcessId;
        }

        return lineage;
    }
}
