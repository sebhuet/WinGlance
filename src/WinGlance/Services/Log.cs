using System.Diagnostics;
using System.IO;

namespace WinGlance.Services;

/// <summary>
/// Minimal application log: <c>%LOCALAPPDATA%\WinGlance\winglance.log</c>, rotated to
/// <c>winglance.old.log</c> past 1 MB. Writes go to <see cref="Trace"/> only until
/// <see cref="Initialize"/> is called, so tests never write to the user's log.
/// </summary>
internal static class Log
{
    private const long MaxFileSize = 1024 * 1024;
    private static readonly object s_lock = new();
    private static string? s_filePath;

    /// <summary>Path of the log file, or null before <see cref="Initialize"/>.</summary>
    public static string? FilePath => s_filePath;

    /// <summary>Starts writing to the log file under the given folder.</summary>
    public static void Initialize(string directory)
    {
        s_filePath = Path.Combine(directory, "winglance.log");
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warning(string message, Exception? exception = null) => Write("WARN", message, exception);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        Trace.WriteLine($"[{level}] {message}{(exception is null ? "" : $": {exception.Message}")}");

        if (s_filePath is { } path)
            Append(path, level, message, exception, DateTime.Now);
    }

    /// <summary>
    /// Appends one entry to the given file, rotating it when it exceeds 1 MB.
    /// Never throws: logging must not be the reason the app fails.
    /// </summary>
    internal static void Append(string path, string level, string message, Exception? exception, DateTime timestamp)
    {
        var entry = $"{timestamp:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        if (exception is not null)
            entry += Environment.NewLine + exception;

        try
        {
            lock (s_lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                var file = new FileInfo(path);
                if (file.Exists && file.Length > MaxFileSize)
                    File.Move(path, Path.ChangeExtension(path, ".old.log"), overwrite: true);

                File.AppendAllText(path, entry + Environment.NewLine);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"Log write failed: {ex.Message}");
        }
    }
}
