using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using WinGlance.Models;
using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class LlmAnalyzerTests
{
    // ── ParseVerdict ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("awaiting_action", "awaiting_action")]
    [InlineData("idle", "idle")]
    [InlineData("The window is awaiting_action from the user.", "awaiting_action")]
    [InlineData("This window appears to be idle.", "idle")]
    [InlineData("AWAITING_ACTION", "awaiting_action")]
    [InlineData("IDLE", "idle")]
    public void ParseVerdict_ReturnsExpectedResult(string response, string expected)
    {
        Assert.Equal(expected, LlmAnalyzer.ParseVerdict(response));
    }

    [Theory]
    [InlineData("something unexpected")]
    [InlineData("")]
    public void ParseVerdict_UnrecognizedResponse_ReturnsNull(string response)
    {
        // An unusable answer is not an idle window
        Assert.Null(LlmAnalyzer.ParseVerdict(response));
    }

    [Fact]
    public void ParseVerdict_AwaitingTakesPriorityOverIdle()
    {
        // If both keywords appear, awaiting_action wins
        var result = LlmAnalyzer.ParseVerdict("awaiting_action or idle");
        Assert.Equal("awaiting_action", result);
    }

    [Theory]
    [InlineData("idle\nNothing to do here", "Nothing to do here")]
    [InlineData("idle", null)]
    public void ParseReason_ReturnsTheSecondLine(string response, string? expected)
    {
        Assert.Equal(expected, LlmAnalyzer.ParseReason(response));
    }

    // ── Requests and responses ───────────────────────────────────────────

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json) };

    private static (LlmAnalyzer Analyzer, FakeHandler Handler) Create(
        string provider, Func<HttpRequestMessage, HttpResponseMessage> respond, string model = "test-model")
    {
        var handler = new FakeHandler(respond);
        var config = new LlmConfig
        {
            Provider = provider,
            Endpoint = "https://llm.example/v1/",
            ApiKey = "secret-key",
            Model = model,
        };
        return (new LlmAnalyzer(config, "system prompt", new HttpClient(handler)), handler);
    }

    private static Bitmap Screenshot(int width = 64, int height = 32) => new(width, height);

    [Fact]
    public async Task OpenAi_SendsBearerKeyAndNoMaxTokens_AndParsesTheVerdict()
    {
        var (analyzer, handler) = Create("openai", _ => Json("""
            {"choices":[{"message":{"content":"awaiting_action\nA save dialog is open"}}]}
            """));
        using var screenshot = Screenshot();

        var result = await analyzer.AnalyzeWithReasonAsync(screenshot, "Untitled - Notepad");

        Assert.Equal(new LlmResult("awaiting_action", "A save dialog is open", null), result);
        Assert.Equal("https://llm.example/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer secret-key", handler.Request.Headers.Authorization!.ToString());
        Assert.DoesNotContain("max_tokens", handler.Body); // rejected by newer reasoning models
        Assert.Contains("data:image/png;base64,", handler.Body);
    }

    [Fact]
    public async Task Gemini_SendsTheKeyInAHeader_NotTheUrl()
    {
        var (analyzer, handler) = Create("google", _ => Json("""
            {"candidates":[{"content":{"parts":[{"text":"idle"}]}}]}
            """), model: "gemini 2/flash");
        using var screenshot = Screenshot();

        var result = await analyzer.AnalyzeWithReasonAsync(screenshot, "title");

        Assert.Equal("idle", result.Verdict);
        var url = handler.Request!.RequestUri!.AbsoluteUri;
        Assert.DoesNotContain("secret-key", url);
        Assert.Contains("/models/gemini%202%2Fflash:generateContent", url); // model escaped
        Assert.Equal("secret-key", handler.Request.Headers.GetValues("x-goog-api-key").Single());
    }

    [Fact]
    public async Task Ollama_PostsToApiGenerate()
    {
        var (analyzer, handler) = Create("ollama", _ => Json("""{"response":"idle"}"""));
        using var screenshot = Screenshot();

        var result = await analyzer.AnalyzeWithReasonAsync(screenshot, "title");

        Assert.Equal("idle", result.Verdict);
        Assert.Equal("https://llm.example/v1/api/generate", handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task ErrorStatus_IsReportedAsAnError_WithTheProviderMessage()
    {
        var (analyzer, _) = Create("openai", _ => Json("""{"error":"invalid api key"}""", HttpStatusCode.Unauthorized));
        using var screenshot = Screenshot();

        var result = await analyzer.AnalyzeWithReasonAsync(screenshot, "title");

        Assert.Null(result.Verdict); // used to become "idle"
        Assert.StartsWith("HTTP 401", result.Error);
        Assert.Contains("invalid api key", result.Error);
    }

    [Fact]
    public async Task UnrecognizedAnswer_IsReportedAsAnError()
    {
        var (analyzer, _) = Create("ollama", _ => Json("""{"response":"I cannot tell"}"""));
        using var screenshot = Screenshot();

        var result = await analyzer.AnalyzeWithReasonAsync(screenshot, "title");

        Assert.Null(result.Verdict);
        Assert.StartsWith("Unrecognized response: I cannot tell", result.Error);
    }

    [Fact]
    public async Task UnexpectedJsonShape_IsReportedAsAnError()
    {
        var (analyzer, _) = Create("openai", _ => Json("""{"choices":[]}"""));
        using var screenshot = Screenshot();

        var result = await analyzer.AnalyzeWithReasonAsync(screenshot, "title");

        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Timeout_IsReportedAsAnError()
    {
        var (analyzer, _) = Create("openai", _ => throw new TaskCanceledException("timeout"));
        using var screenshot = Screenshot();

        var result = await analyzer.AnalyzeWithReasonAsync(screenshot, "title");

        Assert.StartsWith("No response within", result.Error);
    }

    [Fact]
    public async Task Cancellation_IsNotSwallowed()
    {
        var (analyzer, _) = Create("openai", _ => Json("{}"));
        using var screenshot = Screenshot();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => analyzer.AnalyzeWithReasonAsync(screenshot, "title", cancellation.Token));
    }

    // ── Image encoding ───────────────────────────────────────────────────

    [Fact]
    public void EncodeForUpload_LargeCapture_IsScaledDown()
    {
        using var capture = Screenshot(4000, 2000);

        var base64 = LlmAnalyzer.EncodeForUpload(capture);

        using var decoded = new Bitmap(new MemoryStream(Convert.FromBase64String(base64)));
        Assert.Equal(LlmAnalyzer.MaxImageSide, decoded.Width);
        Assert.Equal(LlmAnalyzer.MaxImageSide / 2, decoded.Height);
    }

    [Fact]
    public void OpenAiBody_IsValidJson()
    {
        // Guard against hand-built JSON: the body must parse
        var (analyzer, handler) = Create("openai", _ => Json("""{"choices":[{"message":{"content":"idle"}}]}"""));
        using var screenshot = Screenshot();

        analyzer.AnalyzeWithReasonAsync(screenshot, "quote \" title").GetAwaiter().GetResult();

        using var doc = JsonDocument.Parse(handler.Body!);
        Assert.Equal("test-model", doc.RootElement.GetProperty("model").GetString());
    }
}
