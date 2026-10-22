using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using WinGlance.Models;
using WinGlance.Services;
using WinGlance.ViewModels;

namespace WinGlance.Tests.Services;

/// <summary>
/// Drives <see cref="LlmService"/> with a fake capture, a fake clock and an analyzer whose
/// HTTP calls are answered by a stub, to check when the model is called and what is kept.
/// </summary>
public class LlmServiceTests
{
    private static readonly DateTime Start = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

    private const string AwaitingAction = """{"choices":[{"message":{"content":"awaiting_action\nA save dialog is open"}}]}""";

    // ── Staleness ────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAsync_ContentUnchangedPastThreshold_AnalyzesAndStoresTheVerdict()
    {
        var fixture = new Fixture(_ => Json(AwaitingAction));
        var window = Window();

        await fixture.Service.EvaluateAsync(window); // first capture: the reference
        fixture.Now = Start.AddSeconds(31);
        await fixture.Service.EvaluateAsync(window);

        Assert.True(window.IsStale);
        Assert.Equal("awaiting_action", window.LlmVerdict);
        Assert.Equal(1, fixture.Requests);
    }

    [Fact]
    public async Task EvaluateAsync_BeforeThreshold_DoesNotCallTheModel()
    {
        var fixture = new Fixture(_ => Json(AwaitingAction));
        var window = Window();

        await fixture.Service.EvaluateAsync(window);
        fixture.Now = Start.AddSeconds(29);
        await fixture.Service.EvaluateAsync(window);

        Assert.False(window.IsStale);
        Assert.Null(window.LlmVerdict);
        Assert.Equal(0, fixture.Requests);
    }

    [Fact]
    public async Task EvaluateAsync_ContentChanges_ClearsTheVerdict()
    {
        var fixture = new Fixture(_ => Json(AwaitingAction));
        var window = Window();
        await fixture.MakeStaleAndAnalyze(window);

        fixture.Hash = 2; // the window shows something else
        await fixture.Service.EvaluateAsync(window);

        Assert.False(window.IsStale);
        Assert.Null(window.LlmVerdict);
    }

    [Fact]
    public async Task EvaluateAsync_VerdictKnown_DoesNotCallTheModelAgain()
    {
        var fixture = new Fixture(_ => Json(AwaitingAction));
        var window = Window();
        await fixture.MakeStaleAndAnalyze(window);

        fixture.Now = Start.AddMinutes(10);
        await fixture.Service.EvaluateAsync(window);

        Assert.Equal(1, fixture.Requests);
    }

    [Fact]
    public async Task EvaluateAsync_CaptureFails_IsNeverStale()
    {
        var fixture = new Fixture(_ => Json(AwaitingAction)) { Hash = null };
        var window = Window();

        await fixture.Service.EvaluateAsync(window);
        fixture.Now = Start.AddMinutes(5);
        await fixture.Service.EvaluateAsync(window);

        Assert.False(window.IsStale);
        Assert.Equal(0, fixture.Requests);
    }

    [Fact]
    public async Task EvaluateAsync_Disabled_DoesNotCaptureTheWindow()
    {
        var fixture = new Fixture(_ => Json(AwaitingAction));
        fixture.Config.Enabled = false;

        await fixture.Service.EvaluateAsync(Window());

        Assert.Equal(0, fixture.HashCalls);
    }

    [Fact]
    public async Task EvaluateAsync_HungWindow_IsNotCaptured()
    {
        // Capturing sends WM_PRINT, which a hung window would never answer
        var fixture = new Fixture(_ => Json(AwaitingAction));

        await fixture.Service.EvaluateAsync(Window(isHung: true));

        Assert.Equal(0, fixture.HashCalls);
    }

    // ── Failures and concurrency ─────────────────────────────────────────

    [Fact]
    public async Task EvaluateAsync_FailedCall_RetriesOnlyAfterTheDelay()
    {
        var fixture = new Fixture(_ => Json("""{"error":"bad key"}""", HttpStatusCode.Unauthorized));
        var window = Window();
        await fixture.MakeStaleAndAnalyze(window);
        Assert.Null(window.LlmVerdict);
        Assert.Equal(1, fixture.Requests);

        fixture.Now += LlmService.RetryDelay - TimeSpan.FromSeconds(1);
        await fixture.Service.EvaluateAsync(window);
        Assert.Equal(1, fixture.Requests);

        fixture.Now += TimeSpan.FromSeconds(2);
        await fixture.Service.EvaluateAsync(window);
        Assert.Equal(2, fixture.Requests);
    }

    [Fact]
    public async Task EvaluateAsync_FailedCall_IsWrittenToTheDebugLog()
    {
        var fixture = new Fixture(_ => Json("""{"error":"bad key"}""", HttpStatusCode.Unauthorized));
        var log = fixture.AttachDebugLog();

        await fixture.MakeStaleAndAnalyze(Window());

        var entry = Assert.Single(log.DebugLog);
        Assert.Contains("\"Readme - Project\" → error", entry);
    }

    [Fact]
    public async Task EvaluateAsync_AnalysisInFlight_DoesNotStartASecondOne()
    {
        var release = new TaskCompletionSource();
        var fixture = new Fixture(_ =>
        {
            release.Task.Wait();
            return Json(AwaitingAction);
        });
        var window = Window();
        await fixture.Service.EvaluateAsync(window);
        fixture.Now = Start.AddSeconds(31);

        var first = Task.Run(() => fixture.Service.EvaluateAsync(window));
        await fixture.WaitForRequests(1);
        await fixture.Service.EvaluateAsync(window); // the next polling tick
        release.SetResult();
        await first;

        Assert.Equal(1, fixture.Requests);
        Assert.Equal("awaiting_action", window.LlmVerdict);
    }

    // ── Manual run, removal, prompt, disposal ────────────────────────────

    [Fact]
    public async Task ForceEvaluateAsync_IgnoresTheStaleThreshold()
    {
        var fixture = new Fixture(_ => Json(AwaitingAction));
        var window = Window();

        await fixture.Service.ForceEvaluateAsync(window);

        Assert.Equal("awaiting_action", window.LlmVerdict);
        Assert.Equal(1, fixture.Requests);
    }

    [Fact]
    public async Task ForceEvaluateAsync_WindowCannotBeCaptured_LogsItWithoutCallingTheModel()
    {
        var fixture = new Fixture(_ => Json(AwaitingAction)) { CaptureSucceeds = false };
        var log = fixture.AttachDebugLog();

        await fixture.Service.ForceEvaluateAsync(Window());

        Assert.Equal(0, fixture.Requests);
        Assert.Contains("Could not capture the window", Assert.Single(log.DebugLog));
    }

    [Fact]
    public async Task Remove_ForgetsTheWindowHistory()
    {
        var fixture = new Fixture(_ => Json(AwaitingAction));
        var window = Window();
        await fixture.Service.EvaluateAsync(window);

        fixture.Service.Remove(window.Hwnd);
        fixture.Now = Start.AddSeconds(31);
        await fixture.Service.EvaluateAsync(window);

        // The capture after the removal is a new reference, not 31 s of unchanged content
        Assert.False(window.IsStale);
        Assert.Equal(0, fixture.Requests);
    }

    [Fact]
    public async Task ReloadPrompt_SendsTheNewPromptFromTheNextAnalysis()
    {
        var fixture = new Fixture(_ => Json(AwaitingAction));

        fixture.Service.ReloadPrompt("Custom classification rules");
        await fixture.Service.ForceEvaluateAsync(Window());

        Assert.Contains("Custom classification rules", fixture.LastBody);
    }

    [Fact]
    public async Task ReloadPrompt_Blank_FallsBackToTheDefaultPrompt()
    {
        var fixture = new Fixture(_ => Json(AwaitingAction));

        fixture.Service.ReloadPrompt("   ");
        await fixture.Service.ForceEvaluateAsync(Window());

        Assert.Contains("desktop window analyst", fixture.LastBody);
    }

    [Fact]
    public async Task Dispose_StopsFurtherAnalyses()
    {
        var fixture = new Fixture(_ => Json(AwaitingAction));
        var window = Window();

        fixture.Service.Dispose();
        await fixture.Service.ForceEvaluateAsync(window);
        await fixture.Service.EvaluateAsync(window);

        Assert.Equal(0, fixture.Requests);
        Assert.Null(window.LlmVerdict);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static TrackedWindow Window(bool isHung = false) =>
        new((IntPtr)42, "Code", "VS Code") { Title = "Readme - Project", IsHung = isHung };

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json) };

    /// <summary>A service with a controllable capture, clock and HTTP stub.</summary>
    private sealed class Fixture
    {
        private readonly StubHandler _handler;

        public Fixture(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _handler = new StubHandler(respond);
            Config = new LlmConfig
            {
                Enabled = true,
                Provider = "openai",
                Endpoint = "https://llm.example/v1/",
                ApiKey = "secret-key",
                Model = "test-model",
                StaleThresholdSeconds = 30,
            };
            Service = new LlmService(
                Config,
                _ =>
                {
                    HashCalls++;
                    return Hash;
                },
                _ => CaptureSucceeds ? new Bitmap(16, 16) : null,
                prompt => new LlmAnalyzer(Config, prompt, new HttpClient(_handler)),
                () => Now);
        }

        public LlmConfig Config { get; }
        public LlmService Service { get; }
        public DateTime Now { get; set; } = Start;
        public ulong? Hash { get; set; } = 1;
        public bool CaptureSucceeds { get; set; } = true;
        public int HashCalls { get; private set; }
        public int Requests => _handler.Requests;
        public string? LastBody => _handler.LastBody;

        /// <summary>First capture at <see cref="Start"/>, then a stale one 31 s later.</summary>
        public async Task MakeStaleAndAnalyze(TrackedWindow window)
        {
            Now = Start;
            await Service.EvaluateAsync(window);
            Now = Start.AddSeconds(31);
            await Service.EvaluateAsync(window);
        }

        public PromptEditorViewModel AttachDebugLog()
        {
            var config = new AppConfig { Llm = { DebugEnabled = true } };
            var log = new PromptEditorViewModel(
                new ConfigService(Path.Combine(Path.GetTempPath(), $"WinGlance_Test_{Guid.NewGuid():N}")), config);
            Service.DebugLogger = log;
            return log;
        }

        public async Task WaitForRequests(int count)
        {
            for (var i = 0; i < 200 && Requests < count; i++)
                await Task.Delay(10);
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        private int _requests;

        public int Requests => Volatile.Read(ref _requests);
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Interlocked.Increment(ref _requests);
            return respond(request);
        }
    }
}
