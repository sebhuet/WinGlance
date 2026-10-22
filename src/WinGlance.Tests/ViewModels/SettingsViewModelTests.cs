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
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    // Auto-start values passed to the registry writer; recorded instead of written to HKCU\...\Run
    private readonly List<bool> _autoStartCalls = [];

    private SettingsViewModel CreateVm(Action<double>? opacityCallback = null)
    {
        return new SettingsViewModel(_configService, _config, _previewVm, opacityCallback, _autoStartCalls.Add);
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

        vm.AgentDetectionEnabled = false;

        vm.Save();

        Assert.Equal([true], _autoStartCalls); // applied through the injected writer, not the registry
        var loaded = _configService.Load();
        Assert.False(loaded.AgentDetection.Enabled);
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

    // ── LLM provider ─────────────────────────────────────────────────

    [Theory]
    [InlineData("google", "https://generativelanguage.googleapis.com/v1beta", "gemini-2.0-flash")]
    [InlineData("ollama", "http://localhost:11434", "llava")]
    public void LlmProvider_Switch_AppliesTheProviderDefaults(string provider, string endpoint, string model)
    {
        var vm = CreateVm(); // openai defaults

        vm.LlmProvider = provider;

        Assert.Equal(endpoint, vm.LlmEndpoint);
        Assert.Equal(model, vm.LlmModel);
    }

    [Fact]
    public void LlmProvider_Switch_KeepsCustomValues()
    {
        var vm = CreateVm();
        vm.LlmEndpoint = "https://my-proxy.example/v1";
        vm.LlmModel = "my-model";

        vm.LlmProvider = "ollama";

        Assert.Equal("https://my-proxy.example/v1", vm.LlmEndpoint);
        Assert.Equal("my-model", vm.LlmModel);
    }

    [Fact]
    public void Save_LlmSettingChanged_NotifiesOnce()
    {
        var vm = CreateVm();
        var notifications = 0;
        vm.LlmSettingsChanged = () => notifications++;

        vm.LlmEnabled = true;
        vm.Save();
        vm.Save(); // nothing changed since

        Assert.Equal(1, notifications);
    }

    // ── Transparency (Options panel) ─────────────────────────────────

    [Theory]
    [InlineData(0.85, 15)]
    [InlineData(1.0, 0)]
    [InlineData(0.3, 70)]
    public void PanelTransparency_IsTheOpacityTheOtherWayRound(double opacity, int expected)
    {
        _config.PanelOpacity = opacity;

        Assert.Equal(expected, CreateVm().PanelTransparency);
    }

    [Fact]
    public void PanelTransparency_AppliesTheOpacityLive()
    {
        double? applied = null;
        var vm = CreateVm(opacity => applied = opacity);
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.PanelTransparency = 40;

        Assert.Equal(0.6, applied!.Value, precision: 6);
        Assert.Equal(0.6, vm.PanelOpacity, precision: 6);
        Assert.Contains(nameof(SettingsViewModel.PanelTransparency), raised);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(95, 70)] // never below 30 % opacity: the panel must stay visible
    public void PanelTransparency_IsClamped(int requested, int expected)
    {
        var vm = CreateVm();

        vm.PanelTransparency = requested;

        Assert.Equal(expected, vm.PanelTransparency);
    }

    [Fact]
    public void SavePanelOpacity_SavesTheOpacityAlone()
    {
        var vm = CreateVm();
        vm.PanelTransparency = 25;
        vm.Layout = "grid"; // pending in the Settings view, not saved by the Options panel

        vm.SavePanelOpacity();

        var saved = _configService.Load();
        Assert.Equal(0.75, saved.PanelOpacity, precision: 6);
        Assert.Equal("fit", saved.Layout);
        Assert.Empty(_autoStartCalls); // the registry is not touched either
    }

    // ── Highlight on switch ──────────────────────────────────────────

    [Fact]
    public void HighlightOnSwitch_IsReadFromConfig_AndSaved()
    {
        _config.HighlightOnSwitch = true;
        var vm = CreateVm();
        Assert.True(vm.HighlightOnSwitch);

        vm.HighlightOnSwitch = false;
        vm.Save();

        Assert.False(_configService.Load().HighlightOnSwitch);
    }

    // ── Hotkey ───────────────────────────────────────────────────────

    [Fact]
    public void Save_RegistersTheHotkeyImmediately()
    {
        var vm = CreateVm();
        string? applied = null;
        vm.ApplyHotkey = hotkey =>
        {
            applied = hotkey;
            return true;
        };
        vm.Hotkey = "Ctrl+Shift+W";

        vm.Save();

        Assert.Equal("Ctrl+Shift+W", applied);
        Assert.Equal(string.Empty, vm.HotkeyStatus);
    }

    [Fact]
    public void Save_HotkeyRejected_ShowsAStatusMessage()
    {
        var vm = CreateVm();
        vm.ApplyHotkey = _ => false;

        vm.Save();

        Assert.StartsWith("Not registered", vm.HotkeyStatus);
    }

    [Fact]
    public void ReportHotkeyResult_Success_ClearsTheStatus()
    {
        var vm = CreateVm();
        vm.ReportHotkeyResult(false);

        vm.ReportHotkeyResult(true);

        Assert.Equal(string.Empty, vm.HotkeyStatus);
    }

    // ── Coding agents ────────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_InitializesAgentDetectionFromConfig(bool enabled)
    {
        _config.AgentDetection.Enabled = enabled;

        Assert.Equal(enabled, CreateVm().AgentDetectionEnabled);
    }

    [Fact]
    public void AgentDetectionEnabled_TurnedOff_RemovesMonitorFromPreview()
    {
        _config.AgentDetection.Enabled = true;
        var vm = CreateVm();
        vm.AgentDetectionEnabled = false;
        Assert.Null(_previewVm.AgentMonitor);
    }

    [Fact]
    public void AgentDetectionEnabled_TurnedOn_GivesPreviewAMonitorWithConfiguredInterval()
    {
        _config.AgentDetection.Enabled = false;
        _config.AgentDetection.RefreshIntervalMs = 7000;
        var vm = CreateVm();

        vm.AgentDetectionEnabled = true;

        Assert.NotNull(_previewVm.AgentMonitor);
        Assert.Equal(TimeSpan.FromSeconds(7), _previewVm.AgentMonitor.RefreshInterval);
    }

    [Fact]
    public void AgentDetectionEnabled_RaisesPropertyChanged()
    {
        _config.AgentDetection.Enabled = false;
        var vm = CreateVm();
        var raised = false;
        vm.PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(SettingsViewModel.AgentDetectionEnabled);

        vm.AgentDetectionEnabled = true;

        Assert.True(raised);
    }
}
