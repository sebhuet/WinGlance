using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using WinGlance.Models;

namespace WinGlance.Services;

/// <summary>
/// Lists Claude Code sessions with <c>claude agents --json</c>, the interface documented for
/// external tools (https://code.claude.com/docs/en/agent-view). The files under <c>~/.claude</c>
/// are deliberately not read: the documentation states they are not a stable interface.
/// </summary>
internal sealed class ClaudeAgentSource(string? executablePath)
{
    private const int TimeoutMs = 5000;

    /// <summary>Full path of the claude executable, or null when Claude Code is not installed.</summary>
    public string? ExecutablePath { get; } = executablePath;

    /// <summary>
    /// True when the CLI comes from npm (a .cmd shim): its sessions then run as node.exe,
    /// so the absence of a claude.exe process proves nothing.
    /// </summary>
    public bool IsScriptShim => ExecutablePath?.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Runs the CLI and returns the live sessions, or an empty list when the CLI is missing,
    /// fails, or does not answer within <see cref="TimeoutMs"/>.
    /// </summary>
    public List<AgentSession> GetSessions()
    {
        if (ExecutablePath is null)
            return [];

        var output = RunAgentsCommand(ExecutablePath);
        return output is null ? [] : Parse(output);
    }

    /// <summary>
    /// Finds claude.exe (native installer) or claude.cmd (npm) on PATH, then in the native
    /// installer's default folder, which may be missing from PATH when WinGlance starts with Windows.
    /// </summary>
    public static string? FindExecutable()
    {
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        directories.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin"));

        foreach (var fileName in new[] { "claude.exe", "claude.cmd" })
        {
            foreach (var directory in directories)
            {
                try
                {
                    var candidate = Path.Combine(directory, fileName);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch (ArgumentException)
                {
                    // Malformed PATH entry
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Parses the JSON array printed by <c>claude agents --json</c>. Entries without a
    /// <c>pid</c> (process no longer alive) are skipped: they cannot be tied to a window.
    /// </summary>
    internal static List<AgentSession> Parse(string json)
    {
        var sessions = new List<AgentSession>();

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return sessions;

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !item.TryGetProperty("pid", out var pid)
                    || pid.ValueKind != JsonValueKind.Number
                    || !pid.TryGetInt32(out var processId))
                    continue;

                sessions.Add(new AgentSession(
                    AgentKind.Claude,
                    processId,
                    GetString(item, "cwd"),
                    ParseStatus(GetString(item, "status")),
                    GetString(item, "waitingFor"),
                    GetString(item, "name")));
            }
        }
        catch (JsonException)
        {
            // Unexpected output (old CLI version, error text): no sessions
        }

        return sessions;
    }

    /// <summary>
    /// Maps the documented <c>status</c> values (busy, waiting, idle). Unknown or future values
    /// mean the process is alive in an unknown state.
    /// </summary>
    internal static AgentStatus ParseStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "busy" => AgentStatus.Busy,
        "waiting" => AgentStatus.Waiting,
        "idle" => AgentStatus.Idle,
        _ => AgentStatus.Running,
    };

    private static string? GetString(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? RunAgentsCommand(string executable)
    {
        var isShim = executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);
        var startInfo = new ProcessStartInfo
        {
            // A .cmd shim has to run through the command interpreter
            FileName = isShim ? Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe" : executable,
            Arguments = isShim ? $"/d /c \"\"{executable}\" agents --json\"" : "agents --json",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            // The listing is machine-wide; a neutral folder keeps it independent of WinGlance's own
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
                return null;

            process.StandardInput.Close(); // never block on an interactive prompt
            var stdout = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync(); // drained so a full pipe cannot stall the CLI

            if (!process.WaitForExit(TimeoutMs))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            // The read is bounded too: a child process inheriting the pipe would keep it open
            return process.ExitCode == 0 && stdout.Wait(TimeoutMs) ? stdout.Result : null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return null;
        }
    }
}
