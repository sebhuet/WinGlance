using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinGlance.Models;

namespace WinGlance.Services;

/// <summary>
/// Sends a window screenshot + title to an LLM provider (OpenAI, Google Gemini, or Ollama)
/// and parses the response as "awaiting_action" or "idle".
/// </summary>
internal sealed class LlmAnalyzer : IDisposable
{
    private static readonly HttpClient s_httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly LlmConfig _config;
    private readonly string _systemPrompt;

    public LlmAnalyzer(LlmConfig config, string systemPrompt)
    {
        _config = config;
        _systemPrompt = systemPrompt;
    }

    /// <summary>
    /// Analyzes a window screenshot and returns "awaiting_action" or "idle".
    /// Returns null if the call fails.
    /// </summary>
    public async Task<string?> AnalyzeAsync(Bitmap screenshot, string windowTitle)
    {
        var (verdict, _) = await AnalyzeWithReasonAsync(screenshot, windowTitle);
        return verdict;
    }

    /// <summary>
    /// Analyzes a window screenshot and returns (verdict, reason).
    /// The reason is the optional second line from the LLM response.
    /// </summary>
    public async Task<(string? Verdict, string? Reason)> AnalyzeWithReasonAsync(Bitmap screenshot, string windowTitle)
    {
        try
        {
            var base64 = BitmapToBase64(screenshot);
            var response = _config.Provider.ToLowerInvariant() switch
            {
                "google" => await CallGeminiAsync(base64, windowTitle),
                "ollama" => await CallOllamaAsync(base64, windowTitle),
                _ => await CallOpenAiAsync(base64, windowTitle),
            };
            var verdict = ParseVerdict(response);
            var reason = ParseReason(response);
            return (verdict, reason);
        }
        catch
        {
            return (null, null);
        }
    }

    public void Dispose()
    {
        // HttpClient is static/shared, no per-instance disposal needed
    }

    // ── OpenAI-compatible API ────────────────────────────────────────────

    private async Task<string> CallOpenAiAsync(string imageBase64, string windowTitle)
    {
        var endpoint = _config.Endpoint.TrimEnd('/') + "/chat/completions";

        var body = new
        {
            model = _config.Model,
            messages = new object[]
            {
                new { role = "system", content = _systemPrompt },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = $"Window title: {windowTitle}" },
                        new
                        {
                            type = "image_url",
                            image_url = new { url = $"data:image/png;base64,{imageBase64}" },
                        },
                    },
                },
            },
            max_tokens = 50,
        };

        var json = JsonSerializer.Serialize(body, s_jsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        if (!string.IsNullOrEmpty(_config.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

        using var response = await s_httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }

    // ── Google Gemini API ────────────────────────────────────────────────

    private async Task<string> CallGeminiAsync(string imageBase64, string windowTitle)
    {
        var endpoint = _config.Endpoint.TrimEnd('/') +
                       $"/models/{_config.Model}:generateContent?key={_config.ApiKey}";

        var body = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = $"{_systemPrompt}\n\nWindow title: {windowTitle}" },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = "image/png",
                                data = imageBase64,
                            },
                        },
                    },
                },
            },
        };

        var json = JsonSerializer.Serialize(body, s_jsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await s_httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "";
    }

    // ── Ollama API ───────────────────────────────────────────────────────

    private async Task<string> CallOllamaAsync(string imageBase64, string windowTitle)
    {
        var endpoint = _config.Endpoint.TrimEnd('/') + "/api/generate";

        var body = new
        {
            model = _config.Model,
            prompt = $"{_systemPrompt}\n\nWindow title: {windowTitle}",
            images = new[] { imageBase64 },
            stream = false,
        };

        var json = JsonSerializer.Serialize(body, s_jsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await s_httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.GetProperty("response").GetString() ?? "";
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    internal static string ParseVerdict(string response)
    {
        var lower = response.ToLowerInvariant().Trim();
        if (lower.Contains("awaiting_action"))
            return "awaiting_action";
        if (lower.Contains("idle"))
            return "idle";
        // Default to idle if unrecognized
        return "idle";
    }

    /// <summary>
    /// Extracts the optional reason from the second line of the LLM response.
    /// </summary>
    internal static string? ParseReason(string response)
    {
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.Length > 1 ? lines[1].Trim() : null;
    }

    private static string BitmapToBase64(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return Convert.ToBase64String(ms.ToArray());
    }

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
