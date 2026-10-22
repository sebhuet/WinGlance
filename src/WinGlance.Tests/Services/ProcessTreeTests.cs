using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class ProcessTreeTests
{
    private static ProcessInfo P(int id, int parent, string exe = "app.exe") => new(id, parent, exe);

    // ── GetLineage ───────────────────────────────────────────────────────

    [Fact]
    public void GetLineage_ReturnsProcessThenAncestors_NearestFirst()
    {
        var tree = new ProcessTree([P(1, 0), P(10, 1), P(20, 10), P(30, 20)]);

        Assert.Equal([30, 20, 10, 1], tree.GetLineage(30));
    }

    [Fact]
    public void GetLineage_UnknownProcess_ReturnsOnlyItself()
    {
        var tree = new ProcessTree([P(1, 0)]);

        Assert.Equal([99], tree.GetLineage(99));
    }

    [Fact]
    public void GetLineage_StopsAfterAParentMissingFromTheSnapshot()
    {
        // 10's parent (5) has exited: it is reported, but nothing above it can be known
        var tree = new ProcessTree([P(10, 5), P(20, 10)]);

        Assert.Equal([20, 10, 5], tree.GetLineage(20));
    }

    [Fact]
    public void GetLineage_StopsOnCycle()
    {
        // PID reuse can make a process look like its own ancestor
        var tree = new ProcessTree([P(10, 20), P(20, 10)]);

        Assert.Equal([10, 20], tree.GetLineage(10));
    }

    [Fact]
    public void GetLineage_IsBoundedInDepth()
    {
        var chain = Enumerable.Range(1, 100).Select(i => P(i, i - 1));
        var tree = new ProcessTree(chain);

        Assert.Equal(32, tree.GetLineage(100).Count);
    }

    [Fact]
    public void Processes_ExposesEverySnapshotEntry()
    {
        var tree = new ProcessTree([P(1, 0, "a.exe"), P(2, 1, "b.exe")]);

        Assert.Equal(["a.exe", "b.exe"], tree.Processes.Select(p => p.ExeName).Order());
    }

    // ── Capture (real machine) ───────────────────────────────────────────

    [Fact]
    public void Capture_ContainsCurrentProcess_WithItsParent()
    {
        var tree = ProcessTree.Capture();

        var lineage = tree.GetLineage(Environment.ProcessId);
        Assert.Contains(tree.Processes, p => p.ProcessId == Environment.ProcessId);
        Assert.True(lineage.Count >= 2, "the test host should have a parent process");
    }
}
