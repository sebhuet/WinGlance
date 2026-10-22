using WinGlance.Models;
using WinGlance.NativeApi;

namespace WinGlance.Services;

/// <summary>
/// Detects running OpenCode instances from the process list. OpenCode publishes no
/// session-state interface, so its sessions are reported as <see cref="AgentStatus.Running"/>
/// without busy/idle detail.
/// </summary>
internal sealed class OpenCodeAgentSource
{
    private const string ExeName = "opencode.exe";

    /// <summary>
    /// Returns one session per OpenCode instance, with the working directory read from the process.
    /// </summary>
    public List<AgentSession> GetSessions(ProcessTree tree) =>
        FindRootProcesses(tree)
            .Select(p => new AgentSession(
                AgentKind.OpenCode,
                p.ProcessId,
                NativeMethods.TryGetProcessCurrentDirectory(p.ProcessId),
                AgentStatus.Running))
            .ToList();

    /// <summary>
    /// Returns the OpenCode processes that do not descend from another OpenCode process,
    /// so that helpers spawned by an instance do not count as extra sessions.
    /// </summary>
    internal static List<ProcessInfo> FindRootProcesses(ProcessTree tree)
    {
        var openCodeIds = tree.Processes
            .Where(p => string.Equals(p.ExeName, ExeName, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.ProcessId)
            .ToHashSet();

        return tree.Processes
            .Where(p => openCodeIds.Contains(p.ProcessId)
                && !tree.GetLineage(p.ProcessId).Skip(1).Any(openCodeIds.Contains))
            .ToList();
    }
}
