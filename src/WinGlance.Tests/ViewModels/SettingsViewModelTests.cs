using System.IO;
using WinGlance.Models;
using WinGlance.Services;
using WinGlance.ViewModels;

namespace WinGlance.Tests.ViewModels;

public class SettingsViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private readonly PreviewViewModel _previewVm;

    public SettingsViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"WinGlance_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _configService = new ConfigService(_tempDir);
        _config = new AppConfig();
        _previewVm = new PreviewViewModel();
    }

    public void Dispose()
    {
        _previewVm.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort */ }
    }

    private SettingsViewModel CreateVm(Action<double>? opacityCallback = null)
    {
        return new SettingsViewModel(_configService, _config, _previewVm, opacityCallback);
    }

    // ── Defaults from config ─────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesFromConfig()
    {
        _config.Layout = "grid";
        _config.ThumbnailWidth = 300;
        _config.ThumbnailHeight = 200;
        _config.PanelOpacity = 0.7;
        _config.PollingIntervalMs = 2000;
        _config.Hotkey = "Ctrl+Shift+G";

        var vm = CreateVm();

        Assert.Equal("grid", vm.Layout);
        Assert.Equal(300, vm.ThumbnailWidth);
        Assert.Equal(200, vm.ThumbnailHeight);
        Assert.Equal(0.7, vm.PanelOpacity);
        Assert.Equal(2000, vm.PollingIntervalMs);
        Assert.Equal("Ctrl+Shift+G", vm.Hotkey);
    }

    [Fact]
    public void Constructor_InitializesLlmFromConfig()
    {
        _config.Llm.Enabled = true;
        _config.Llm.Provider = "google";
        _config.Llm.Model = "gemini-pro";

        var vm = CreateVm();

        Assert.True(vm.LlmEnabled);
        Assert.Equal("google", vm.LlmProvider);
        Assert.Equal("gemini-pro", vm.LlmModel);
    }

    // ── Live-apply to PreviewViewModel ───────────────────────────────

    [Fact]
    public void Layout_Change_AppliesLiveToPreview()
    {
        var vm = CreateVm();

        vm.Layout = "vertical";

        Assert.Equal("vertical", _previewVm.Layout);
    }

    [Fact]
    public void ThumbnailWidth_Change_AppliesLiveToPreview()
    {
        var vm = CreateVm();

        vm.ThumbnailWidth = 350;

        Assert.Equal(350, _previewVm.ThumbnailWidth);
    }

    [Fact]
    public void ThumbnailHeight_Change_AppliesLiveToPreview()
    {
        var vm = CreateVm();

        vm.ThumbnailHeight = 250;

        Assert.Equal(250, _previewVm.ThumbnailHeight);
    }

    [Fact]
    public void PollingIntervalMs_Change_AppliesLiveToPreview()
    {
        var vm = CreateVm();

        vm.PollingIntervalMs = 3000;

        Assert.Equal(3000, _previewVm.PollingIntervalMs);
    }

    [Fact]
    public void PanelOpacity_Change_InvokesCallback()
    {
        double? received = null;
        var vm = CreateVm(opacity => received = opacity);

        vm.PanelOpacity = 0.5;

        Assert.Equal(0.5, received);
    }

    // ── PropertyChanged ──────────────────────────────────────────────

    [Theory]
    [InlineData(nameof(SettingsViewModel.Layout), "vertical")]
    [InlineData(nameof(SettingsViewModel.Hotkey), "Alt+G")]
    [InlineData(nameof(SettingsViewModel.RememberPosition), false)]
    [InlineData(nameof(SettingsViewModel.CloseToTray), false)]
    [InlineData(nameof(SettingsViewModel.AutoStart), true)]
    [InlineData(nameof(SettingsViewModel.LlmEnabled), true)]
    [InlineData(nameof(SettingsViewModel.LlmProvider), "ollama")]
    [InlineData(nameof(SettingsViewModel.LlmModel), "llama3")]
    public void Property_RaisesPropertyChanged(string propertyName, object newValue)
    {
        var vm = CreateVm();
        var raised = false;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == propertyName)
                raised = true;
        };

        var prop = typeof(SettingsViewModel).GetProperty(propertyName)!;
        prop.SetValue(vm, newValue);

        Assert.True(raised, $"{propertyName} did not raise PropertyChanged");
    }

    // ── Save ─────────────────────────────────────────────────────────

    [Fact]
    public void Save_PersistsAllSettings()
    {
        var vm = CreateVm();
        vm.Layout = "grid";
        vm.ThumbnailWidth = 400;
        vm.ThumbnailHeight = 300;
        vm.PanelOpacity = 0.6;
        vm.PollingIntervalMs = 1500;
        vm.RememberPosition = false;
        vm.AutoStart = true;
        vm.CloseToTray = false;
        vm.Hotkey = "Alt+W";
        vm.LlmEnabled = true;
        vm.LlmProvider = "ollama";
        vm.LlmEndpoint = "http://localhost:11434";
        vm.LlmApiKey = "test-key";
        vm.LlmModel = "llama3";
        vm.LlmStaleThresholdSeconds = 45;

        vm.Save();

        var loaded = _configService.Load();
        Assert.Equal("grid", loaded.Layout);
        Assert.Equal(400, loaded.ThumbnailWidth);
        Assert.Equal(300, loaded.ThumbnailHeight);
        Assert.Equal(0.6, loaded.PanelOpacity);
        Assert.Equal(1500, loaded.PollingIntervalMs);
        Assert.False(loaded.RememberPosition);
        Assert.True(loaded.AutoStart);
        Assert.False(loaded.CloseToTray);
        Assert.Equal("Alt+W", loaded.Hotkey);
        Assert.True(loaded.Llm.Enabled);
        Assert.Equal("ollama", loaded.Llm.Provider);
        Assert.Equal("http://localhost:11434", loaded.Llm.Endpoint);
        Assert.Equal("test-key", loaded.Llm.ApiKey);
        Assert.Equal("llama3", loaded.Llm.Model);
        Assert.Equal(45, loaded.Llm.StaleThresholdSeconds);
    }

    [Fact]
    public void SaveCommand_IsNotNull()
    {
        var vm = CreateVm();

        Assert.NotNull(vm.SaveCommand);
    }

    [Fact]
    public void EditPromptCommand_IsNotNull()
    {
        var vm = CreateVm();

        Assert.NotNull(vm.EditPromptCommand);
    }
}
