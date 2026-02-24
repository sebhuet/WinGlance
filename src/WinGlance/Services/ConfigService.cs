using System.Diagnostics;
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

    public ConfigService()
    {
        _configPath = GetConfigPath();
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
    /// Loads the configuration from disk. Returns defaults if the file
    /// does not exist or contains invalid JSON.
    /// </summary>
    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(_configPath))
                return new AppConfig();

            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<AppConfig>(json, s_jsonOptions) ?? new AppConfig();
        }
        catch (JsonException ex)
        {
            Trace.TraceWarning($"Corrupt config file at {_configPath}: {ex.Message}");
            return new AppConfig();
        }
        catch (IOException ex)
        {
            Trace.TraceWarning($"Failed to read config file at {_configPath}: {ex.Message}");
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
    /// Determines the config file path. Prefers the executable directory;
    /// falls back to <c>%LOCALAPPDATA%\WinGlance\</c> if unavailable.
    /// </summary>
    private static string GetConfigPath()
    {
        // Try the executable's directory first
        var exePath = Environment.ProcessPath;
        if (exePath is not null)
        {
            var exeDir = Path.GetDirectoryName(exePath);
            if (exeDir is not null)
            {
                var candidate = Path.Combine(exeDir, ConfigFileName);
                try
                {
                    // Test writability by checking/creating the directory
                    Directory.CreateDirectory(exeDir);
                    return candidate;
                }
                catch
                {
                    // Not writable — fall through to LOCALAPPDATA
                }
            }
        }

        // Fallback: %LOCALAPPDATA%\WinGlance\
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var fallbackDir = Path.Combine(localAppData, "WinGlance");
        Directory.CreateDirectory(fallbackDir);
        return Path.Combine(fallbackDir, ConfigFileName);
    }
}
