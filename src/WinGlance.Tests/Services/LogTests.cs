using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class LogTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"WinGlance_Test_{Guid.NewGuid():N}");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    [Fact]
    public void Append_WritesTimestampLevelMessageAndException()
    {
        var path = Path.Combine(_tempDir, "winglance.log");

        Log.Append(path, "WARN", "Scan failed", new InvalidOperationException("boom"), new DateTime(2026, 10, 22, 9, 5, 7, 123));

        var text = File.ReadAllText(path);
        Assert.StartsWith("2026-10-22 09:05:07.123 [WARN] Scan failed", text);
        Assert.Contains("InvalidOperationException: boom", text);
    }

    [Fact]
    public void Append_KeepsPreviousEntries()
    {
        var path = Path.Combine(_tempDir, "winglance.log");

        Log.Append(path, "INFO", "first", null, DateTime.Now);
        Log.Append(path, "INFO", "second", null, DateTime.Now);

        Assert.Equal(2, File.ReadAllLines(path).Length);
    }

    [Fact]
    public void Append_PastOneMegabyte_RotatesTheFile()
    {
        var path = Path.Combine(_tempDir, "winglance.log");
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(path, new string('x', (1024 * 1024) + 1));

        Log.Append(path, "INFO", "fresh", null, DateTime.Now);

        Assert.True(File.Exists(Path.Combine(_tempDir, "winglance.old.log")));
        Assert.EndsWith("fresh" + Environment.NewLine, File.ReadAllText(path));
        Assert.True(new FileInfo(path).Length < 100);
    }

    [Fact]
    public void Append_UnwritablePath_DoesNotThrow()
    {
        // The "directory" is an existing file: creating it fails
        Directory.CreateDirectory(_tempDir);
        var blocker = Path.Combine(_tempDir, "blocker");
        File.WriteAllText(blocker, "");

        var exception = Record.Exception(() =>
            Log.Append(Path.Combine(blocker, "winglance.log"), "INFO", "x", null, DateTime.Now));

        Assert.Null(exception);
    }

    [Fact]
    public void Warning_BeforeInitialize_DoesNotWriteAnyFile()
    {
        // Tests never call Initialize, so nothing lands in the user's real log
        Assert.Null(Log.FilePath);
        Log.Warning("not written");
    }
}
