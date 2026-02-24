using System.Collections.ObjectModel;
using System.Windows.Input;
using WinGlance.Models;
using WinGlance.Services;

namespace WinGlance.ViewModels;

/// <summary>
/// ViewModel for the Prompt Editor screen. Provides an editable prompt text area,
/// a debug toggle, and a live debug log that shows LLM reasoning for each analyzed window.
/// </summary>
internal sealed class PromptEditorViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private string _promptText;
    private bool _isDebugEnabled;

    public PromptEditorViewModel(ConfigService configService, AppConfig config)
    {
        _configService = configService;
        _config = config;
        _promptText = config.Llm.Prompt;
        _isDebugEnabled = config.Llm.DebugEnabled;

        SaveCommand = new RelayCommand(_ => Save());
        ResetToDefaultCommand = new RelayCommand(_ => ResetToDefault());
        ClearLogCommand = new RelayCommand(_ => DebugLog.Clear());
    }

    /// <summary>The editable system prompt text sent to the LLM.</summary>
    public string PromptText
    {
        get => _promptText;
        set => SetProperty(ref _promptText, value);
    }

    /// <summary>When enabled, LLM responses are logged to the debug log area.</summary>
    public bool IsDebugEnabled
    {
        get => _isDebugEnabled;
        set
        {
            if (SetProperty(ref _isDebugEnabled, value))
            {
                _config.Llm.DebugEnabled = value;
            }
        }
    }

    /// <summary>Live debug log entries showing LLM analysis reasoning.</summary>
    public ObservableCollection<string> DebugLog { get; } = [];

    public ICommand SaveCommand { get; }
    public ICommand ResetToDefaultCommand { get; }
    public ICommand ClearLogCommand { get; }

    /// <summary>
    /// Appends a debug log entry. Called by the LLM service when debug mode is on.
    /// Thread-safe: can be called from background threads.
    /// </summary>
    public void AppendLog(string windowTitle, string verdict, string? reason)
    {
        if (!_isDebugEnabled)
            return;

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var reasonText = string.IsNullOrWhiteSpace(reason)
            ? "(no reason given)"
            : reason.Trim();

        var entry = $"[{timestamp}] \"{windowTitle}\" → {verdict} — {reasonText}";

        // Marshal to UI thread if needed
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher &&
            !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => AddEntry(entry));
        }
        else
        {
            AddEntry(entry);
        }
    }

    private void AddEntry(string entry)
    {
        DebugLog.Add(entry);

        // Keep log size reasonable (last 200 entries)
        while (DebugLog.Count > 200)
            DebugLog.RemoveAt(0);
    }

    private void Save()
    {
        _config.Llm.Prompt = PromptText;
        _config.Llm.DebugEnabled = IsDebugEnabled;
        _configService.Save(_config);
    }

    private void ResetToDefault()
    {
        PromptText = LlmConfig.DefaultPrompt;
    }
}
