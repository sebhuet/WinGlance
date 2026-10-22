using WinGlance.Models;
using WinGlance.Services;
using WinGlance.ViewModels;

namespace WinGlance.Tests.ViewModels;

public class PromptEditorViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigService _configService;
    private readonly AppConfig _config = new();

    public PromptEditorViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"WinGlance_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _configService = new ConfigService(_tempDir);
    }

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

    private PromptEditorViewModel CreateVm() => new(_configService, _config);

    [Fact]
    public void Save_PersistsThePrompt_AndNotifiesTheRunningService()
    {
        var vm = CreateVm();
        string? reloaded = null;
        vm.PromptSaved = prompt => reloaded = prompt;
        vm.PromptText = "Custom prompt";

        vm.Save();

        Assert.Equal("Custom prompt", reloaded); // hot-reload, as the specification asks
        Assert.Equal("Custom prompt", _configService.Load().Llm.Prompt);
    }

    [Fact]
    public void ResetToDefault_RestoresTheBuiltInPrompt()
    {
        var vm = CreateVm();
        vm.PromptText = "Custom prompt";

        vm.ResetToDefaultCommand.Execute(null);

        Assert.Equal(LlmConfig.DefaultPrompt, vm.PromptText);
    }

    [Fact]
    public void AppendLog_DebugOff_AddsNothing()
    {
        var vm = CreateVm();

        vm.AppendLog("title", "idle", "reason");

        Assert.Empty(vm.DebugLog);
    }

    [Fact]
    public void AppendLog_KeepsTheLast200Entries()
    {
        var vm = CreateVm();
        vm.IsDebugEnabled = true;

        for (var i = 0; i < 250; i++)
            vm.AppendLog($"window {i}", "idle", null);

        Assert.Equal(200, vm.DebugLog.Count);
        Assert.Contains("window 249", vm.DebugLog[^1]);
        Assert.Contains("window 50", vm.DebugLog[0]);
    }

    [Fact]
    public async Task RunAnalysis_WithoutLlmService_ExplainsWhyEvenWithDebugOff()
    {
        // The message used to be dropped: the debug log was only enabled afterwards
        var vm = CreateVm();

        await vm.RunAnalysis();

        Assert.Contains("Enable LLM analysis in Settings first", Assert.Single(vm.DebugLog));
    }

    [Fact]
    public async Task RunAnalysis_RunsTheAnalysis_AndIsNotRunningAfterwards()
    {
        var vm = CreateVm();
        var ran = false;
        vm.RunAnalysisFunc = () =>
        {
            ran = true;
            return Task.CompletedTask;
        };

        await vm.RunAnalysis();

        Assert.True(ran);
        Assert.False(vm.IsRunning);
    }
}
