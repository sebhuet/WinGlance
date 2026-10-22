using System.IO;
using System.Text.Json;
using WinGlance.Models;

namespace WinGlance.Services;

/// <summary>
/// Loads and persists <see cref="AppConfig"/> as a JSON file.
/// Uses atomic writes (temp file → rename) to prevent corruption.
/// </summary>
internal sealed class ConfigService
{
    private const string ConfigFileName = "config.json";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _configPath;

    /// <summary>Creates the service on the real config file: next to the executable when that folder is writable, otherwise under <c>%LOCALAPPDATA%\WinGlance</c>.</summary>
    public ConfigService()
    {
        _configPath = ResolveConfigPath(
            Path.GetDirectoryName(Environment.ProcessPath),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            IsDirectoryWritable);
    }

    /// <summary>
    /// Creates a ConfigService that reads/writes from a specific directory.
    /// Used for testing.
    /// </summary>
    internal ConfigService(string configDirectory)
    {
        _configPath = Path.Combine(configDirectory, ConfigFileName);
    }

    /// <summary>The full path to the configuration file.</summary>
    public string ConfigPath => _configPath;

    /// <summary>
    /// Loads the configuration from disk. Returns defaults if the file does not exist,
    /// cannot be read or contains invalid JSON; missing sections and out-of-range values
    /// are replaced by defaults (see <see cref="Normalize"/>).
    /// </summary>
    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(_configPath))
                return new AppConfig();

            var json = File.ReadAllText(_configPath);
            return Normalize(JsonSerializer.Deserialize<AppConfig>(json, s_jsonOptions) ?? new AppConfig());
        }
        catch (JsonException ex)
        {
            Log.Warning($"Corrupt config file at {_configPath}, using defaults", ex);
            return new AppConfig();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning($"Failed to read config file at {_configPath}, using defaults", ex);
            return new AppConfig();
        }
    }

    /// <summary>
    /// Persists the configuration to disk using an atomic write
    /// (write to temp file, then rename).
    /// </summary>
    public void Save(AppConfig config)
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (directory is not null)
            Directory.CreateDirectory(directory);

        var tmpPath = _configPath + ".tmp";
        var json = JsonSerializer.Serialize(config, s_jsonOptions);
        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, _configPath, overwrite: true);
    }

    /// <summary>
    /// Repairs a deserialized configuration so that the rest of the app can trust it: a
    /// hand-edited file can hold <c>null</c> sections (which crashed startup) or values the
    /// UI cannot produce (an opacity of 0 made the panel invisible).
    /// </summary>
    internal static AppConfig Normalize(AppConfig config)
    {
        var defaults = new AppConfig();

        config.MonitoredApps = (config.MonitoredApps ?? [])
            .Where(a => a is not null && !string.IsNullOrWhiteSpace(a.ProcessName))
            .Select(a => new MonitoredAppConfig
            {
                ProcessName = a.ProcessName.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(a.DisplayName) ? a.ProcessName.Trim() : a.DisplayName,
            })
            .ToList();

        config.Layout = config.Layout?.ToLowerInvariant() is "fit" or "horizontal" or "vertical" or "grid"
            ? config.Layout.ToLowerInvariant()
            : defaults.Layout;
        config.PanelWidth = double.IsFinite(config.PanelWidth) ? Math.Clamp(config.PanelWidth, 320, 10000) : defaults.PanelWidth;
        config.PanelHeight = double.IsFinite(config.PanelHeight) ? Math.Clamp(config.PanelHeight, 220, 10000) : defaults.PanelHeight;
        config.ThumbnailWidth = Math.Clamp(config.ThumbnailWidth, 100, 500);
        config.ThumbnailHeight = Math.Clamp(config.ThumbnailHeight, 75, 400);
        config.PanelOpacity = double.IsFinite(config.PanelOpacity)
            ? Math.Clamp(config.PanelOpacity, 0.3, 1.0)
            : defaults.PanelOpacity;
        config.PollingIntervalMs = Math.Clamp(config.PollingIntervalMs, 500, 5000);
        config.Hotkey = string.IsNullOrWhiteSpace(config.Hotkey) ? defaults.Hotkey : config.Hotkey;
        config.NextAgentHotkey ??= defaults.NextAgentHotkey; // empty means "no hotkey"

        config.Llm ??= new LlmConfig();
        config.Llm.Provider = string.IsNullOrWhiteSpace(config.Llm.Provider) ? defaults.Llm.Provider : config.Llm.Provider;
        config.Llm.Endpoint ??= string.Empty;
        config.Llm.ApiKey ??= string.Empty;
        config.Llm.Model ??= string.Empty;
        config.Llm.Prompt = string.IsNullOrWhiteSpace(config.Llm.Prompt) ? LlmConfig.DefaultPrompt : config.Llm.Prompt;
        config.Llm.StaleThresholdSeconds = Math.Clamp(config.Llm.StaleThresholdSeconds, 10, 120);

        config.AgentDetection ??= new AgentDetectionConfig();
        config.AgentDetection.RefreshIntervalMs = Math.Clamp(config.AgentDetection.RefreshIntervalMs, 1000, 60000);

        return config;
    }

    /// <summary>
    /// Determines the config file path: next to the executable when that folder is writable
    /// (portable use), otherwise <c>%LOCALAPPDATA%\WinGlance\</c>.
    /// </summary>
    internal static string ResolveConfigPath(string? exeDirectory, string localAppData, Func<string, bool> isWritable)
    {
        if (exeDirectory is not null && isWritable(exeDirectory))
            return Path.Combine(exeDirectory, ConfigFileName);

        var fallbackDir = Path.Combine(localAppData, "WinGlance");
        Directory.CreateDirectory(fallbackDir);
        return Path.Combine(fallbackDir, ConfigFileName);
    }

    /// <summary>
    /// True if a file can be created in the directory. Creating the directory itself is no
    /// proof: it succeeds on an existing read-only folder such as Program Files.
    /// </summary>
    internal static bool IsDirectoryWritable(string directory)
    {
        var probe = Path.Combine(directory, $".winglance-write-test-{Guid.NewGuid():N}");
        try
        {
            using (File.Create(probe, 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
