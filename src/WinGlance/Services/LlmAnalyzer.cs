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
/// Outcome of one LLM analysis: a verdict ("awaiting_action" or "idle") with the optional
/// reason given by the model, or an error describing why no verdict could be obtained.
/// </summary>
internal sealed record LlmResult(string? Verdict, string? Reason, string? Error)
{
    public static LlmResult Failure(string error) => new(null, null, error);
}

/// <summary>
/// Sends a window screenshot + title to an LLM provider (OpenAI, Google Gemini, or Ollama)
/// and parses the response as "awaiting_action" or "idle".
/// </summary>
internal sealed class LlmAnalyzer : IDisposable
{
    // Longest image side sent to the model: enough to read a dialog, far smaller than a 4K capture
    internal const int MaxImageSide = 1568;

    private static readonly HttpClient s_sharedClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly HttpClient _httpClient;
    private readonly LlmConfig _config;
    private readonly string _systemPrompt;

    public LlmAnalyzer(LlmConfig config, string systemPrompt)
        : this(config, systemPrompt, s_sharedClient)
    {
    }

    /// <summary>Creates an analyzer that sends its requests through the given client (tests).</summary>
    internal LlmAnalyzer(LlmConfig config, string systemPrompt, HttpClient httpClient)
    {
        _config = config;
        _systemPrompt = systemPrompt;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Analyzes a window screenshot. Failures (network, HTTP status, timeout, unexpected
    /// response) come back as an <see cref="LlmResult.Error"/>, never as a verdict: they used
    /// to be reported as "idle", which hid a wrong key or endpoint behind a dimmed thumbnail.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public async Task<LlmResult> AnalyzeWithReasonAsync(Bitmap screenshot, string windowTitle, CancellationToken cancellationToken = default)
    {
        try
        {
            var base64 = EncodeForUpload(screenshot);
            var response = _config.Provider.ToLowerInvariant() switch
            {
                "google" => await CallGeminiAsync(base64, windowTitle, cancellationToken),
                "ollama" => await CallOllamaAsync(base64, windowTitle, cancellationToken),
                _ => await CallOpenAiAsync(base64, windowTitle, cancellationToken),
            };

            var verdict = ParseVerdict(response);
            return verdict is null
                ? LlmResult.Failure($"Unrecognized response: {Truncate(response, 120)}")
                : new LlmResult(verdict, ParseReason(response), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return LlmResult.Failure($"No response within {_httpClient.Timeout.TotalSeconds:0} s");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException
                                       or IndexOutOfRangeException or InvalidOperationException)
        {
            return LlmResult.Failure(ex.Message);
        }
    }

    public void Dispose()
    {
        // HttpClient is static/shared, no per-instance disposal needed
    }

    // ── OpenAI-compatible API ────────────────────────────────────────────

    private async Task<string> CallOpenAiAsync(string imageBase64, string windowTitle, CancellationToken cancellationToken)
    {
        var endpoint = _config.Endpoint.TrimEnd('/') + "/chat/completions";

        // No max_tokens: newer reasoning models reject it, and the prompt asks for one word anyway
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
        };

        using var request = CreateRequest(endpoint, body);
        if (!string.IsNullOrEmpty(_config.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

        using var doc = await SendAsync(request, cancellationToken);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }

    // ── Google Gemini API ────────────────────────────────────────────────

    private async Task<string> CallGeminiAsync(string imageBase64, string windowTitle, CancellationToken cancellationToken)
    {
        // The key goes in a header, not the query string, where it would end up in proxy logs
        var endpoint = _config.Endpoint.TrimEnd('/') +
                       $"/models/{Uri.EscapeDataString(_config.Model)}:generateContent";

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

        using var request = CreateRequest(endpoint, body);
        if (!string.IsNullOrEmpty(_config.ApiKey))
            request.Headers.Add("x-goog-api-key", _config.ApiKey);

        using var doc = await SendAsync(request, cancellationToken);
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "";
    }

    // ── Ollama API ───────────────────────────────────────────────────────

    private async Task<string> CallOllamaAsync(string imageBase64, string windowTitle, CancellationToken cancellationToken)
    {
        var endpoint = _config.Endpoint.TrimEnd('/') + "/api/generate";

        var body = new
        {
            model = _config.Model,
            prompt = $"{_systemPrompt}\n\nWindow title: {windowTitle}",
            images = new[] { imageBase64 },
            stream = false,
        };

        using var request = CreateRequest(endpoint, body);
        using var doc = await SendAsync(request, cancellationToken);
        return doc.RootElement.GetProperty("response").GetString() ?? "";
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static HttpRequestMessage CreateRequest(string endpoint, object body) => new(HttpMethod.Post, endpoint)
    {
        Content = new StringContent(JsonSerializer.Serialize(body, s_jsonOptions), Encoding.UTF8, "application/json"),
    };

    /// <summary>
    /// Sends the request and parses the JSON response. An error status throws with the
    /// provider's message, which says why (bad key, unknown model…).
    /// </summary>
    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(json, 200)}");

        return JsonDocument.Parse(json);
    }

    /// <summary>
    /// Extracts the verdict: "awaiting_action" wins over "idle"; null when the response
    /// contains neither (an unusable answer, not an idle window).
    /// </summary>
    internal static string? ParseVerdict(string response)
    {
        var lower = response.ToLowerInvariant();
        if (lower.Contains("awaiting_action"))
            return "awaiting_action";
        if (lower.Contains("idle"))
            return "idle";
        return null;
    }

    /// <summary>
    /// Extracts the optional reason from the second line of the LLM response.
    /// </summary>
    internal static string? ParseReason(string response)
    {
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.Length > 1 ? lines[1].Trim() : null;
    }

    /// <summary>PNG, base64, scaled down to <see cref="MaxImageSide"/>.</summary>
    internal static string EncodeForUpload(Bitmap bitmap)
    {
        using var scaled = ScreenshotComparer.ScaleToFit(bitmap, MaxImageSide);
        using var ms = new MemoryStream();
        scaled.Save(ms, ImageFormat.Png);
        return Convert.ToBase64String(ms.ToArray());
    }

    private static string Truncate(string text, int maxLength)
    {
        var singleLine = text.ReplaceLineEndings(" ").Trim();
        return singleLine.Length <= maxLength ? singleLine : singleLine[..maxLength] + "…";
    }

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
