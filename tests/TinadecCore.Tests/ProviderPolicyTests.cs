using System.Net;
using System.Text;
using Tinadec.Contracts.Models;
using TinadecCore.Services;
using TinadecCore.Storage;

namespace TinadecCore.Tests;

public sealed class ProviderPolicyTests
{
    [Fact]
    public async Task HttpRuntimeCancellationMapsToCancelledError()
    {
        var handler = new DelayedHttpMessageHandler(TimeSpan.FromSeconds(10));
        var runtime = new OpenAiCompatibleProviderRuntime(new OpenAiCompatibleClient(new HttpClient(handler)));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var result = await runtime.GenerateAsync(CreateHttpContext(), "secret", CreateMessages(), cts.Token);

        Assert.Equal("failed", result.Status);
        Assert.Equal(ProviderErrorCategory.Cancelled, result.ErrorCategory);
        Assert.False(result.IsRetryable);
    }

    [Fact]
    public async Task CliRuntimeTimeoutReturnsTimeoutError()
    {
        using var fixture = CliFixture.Create("sleep 10");
        var runtime = new CliProviderRuntime(TimeSpan.FromMilliseconds(150));

        var result = await runtime.GenerateAsync(CreateCliContext(fixture), null, CreateMessages(), CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Equal(ProviderErrorCategory.Timeout, result.ErrorCategory);
        Assert.True(result.IsRetryable);
    }

    [Theory]
    [InlineData(429, true)]
    [InlineData(503, true)]
    [InlineData(408, true)]
    [InlineData(401, false)]
    [InlineData(400, false)]
    public async Task RetryableErrorsAreRetriedAndNonRetryableErrorsAreNot(int statusCode, bool shouldRetry)
    {
        var attempts = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)statusCode)
            {
                Content = JsonContent("{\"error\":\"test\"}")
            });
        });
        var runtime = new OpenAiCompatibleProviderRuntime(new OpenAiCompatibleClient(new HttpClient(handler)));

        var result = await runtime.GenerateAsync(CreateHttpContext(), "secret", CreateMessages(), CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Equal(ProviderErrorMapper.IsRetryable(result.ErrorCategory!.Value), result.IsRetryable);
        Assert.Equal(shouldRetry ? 3 : 1, attempts);
    }

    [Fact]
    public async Task RetryableErrorsDoNotExceedConfiguredMaxAttempts()
    {
        var attempts = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });
        var runtime = new OpenAiCompatibleProviderRuntime(
            new OpenAiCompatibleClient(new HttpClient(handler)),
            maxRetryAttempts: 2);

        var result = await runtime.GenerateAsync(CreateHttpContext(), "secret", CreateMessages(), CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Equal(ProviderErrorCategory.ProviderUnavailable, result.ErrorCategory);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task RetryableFailureUpdatesProviderHealthStatus()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"tinadec-provider-policy-{Guid.NewGuid():N}.db");
        var store = new CoreStore(dbPath);
        store.Initialize();
        SeedProvider(store);

        var attempts = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });

        var runtime = new OpenAiCompatibleProviderRuntime(
            new OpenAiCompatibleClient(new HttpClient(handler)),
            store,
            maxRetryAttempts: 1);

        var result = await runtime.GenerateAsync(CreateHttpContext(), "secret", CreateMessages(), CancellationToken.None);
        var saved = store.GetStoredModelProviderInstance("provider-openai");

        Assert.Equal("failed", result.Status);
        Assert.Equal(1, attempts);
        Assert.NotNull(saved);
        Assert.Equal(ProviderHealthStatus.Cooldown, saved!.HealthStatus);
        Assert.True(saved.FailureCount >= 1);
        Assert.Equal(ProviderErrorCategory.ProviderUnavailable, saved.LastErrorCategory);
    }

    [Fact]
    public async Task RetryableFailureThatSucceedsOnRetryDoesNotUpdateProviderHealthStatus()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"tinadec-provider-policy-{Guid.NewGuid():N}.db");
        var store = new CoreStore(dbPath);
        store.Initialize();
        SeedProvider(store);

        var attempts = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            attempts++;
            return Task.FromResult(attempts == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent("{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}")
                });
        });

        var runtime = new OpenAiCompatibleProviderRuntime(
            new OpenAiCompatibleClient(new HttpClient(handler)),
            store,
            maxRetryAttempts: 2);

        var result = await runtime.GenerateAsync(CreateHttpContext(), "secret", CreateMessages(), CancellationToken.None);
        var saved = store.GetStoredModelProviderInstance("provider-openai");

        Assert.Equal("executed", result.Status);
        Assert.Equal(2, attempts);
        Assert.NotNull(saved);
        Assert.Equal(ProviderHealthStatus.Healthy, saved!.HealthStatus);
        Assert.Equal(0, saved.FailureCount);
        Assert.Null(saved.LastErrorCategory);
    }

    private static MessageDto[] CreateMessages()
    {
        return
        [
            new MessageDto("msg_1", "sess_1", "user", "Hello", DateTimeOffset.UtcNow)
        ];
    }

    private static ResolvedModelInvocationContextDto CreateHttpContext()
    {
        return new ResolvedModelInvocationContextDto(
            "planner",
            null,
            null,
            "https://api.openai.test/v1",
            "gpt-test",
            null,
            "openai-compatible",
            "http",
            "provider-openai",
            false);
    }

    private static ResolvedModelInvocationContextDto CreateCliContext(CliFixture fixture)
    {
        var provider = new ModelProviderInstanceDto(
            "provider-cli",
            "codex-cli",
            "Codex CLI",
            "cli",
            null,
            "gpt-test",
            false,
            fixture.Executable,
            fixture.Workspace,
            null,
            fixture.LaunchArgs,
            ["tools"],
            true,
            "ready",
            "Ready",
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        return new ResolvedModelInvocationContextDto(
            "planner",
            null,
            provider,
            string.Empty,
            "gpt-test",
            null,
            "codex-cli",
            "cli",
            "provider-cli",
            false);
    }

    private static void SeedProvider(CoreStore store)
    {
        store.SaveModelProviderInstance(
            new SaveModelProviderInstanceRequest(
                "provider-openai",
                "openai-compatible",
                "OpenAI",
                "api-key",
                "https://api.openai.test/v1",
                "gpt-test",
                null,
                false,
                null,
                null,
                null,
                null,
                ["chat"],
                true),
            "encrypted-key");
    }

    private static StringContent JsonContent(string value)
    {
        return new StringContent(value, Encoding.UTF8, "application/json");
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return handle(request);
        }
    }

    private sealed class DelayedHttpMessageHandler(TimeSpan delay) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}")
            };
        }
    }

    private sealed class CliFixture : IDisposable
    {
        private CliFixture(string root, string workspace, string executable, string launchArgs)
        {
            Root = root;
            Workspace = workspace;
            Executable = executable;
            LaunchArgs = launchArgs;
        }

        public string Root { get; }
        public string Workspace { get; }
        public string Executable { get; }
        public string LaunchArgs { get; }

        public static CliFixture Create(string unixScript)
        {
            var root = Path.Combine(Path.GetTempPath(), $"tinadec-cli-policy-{Guid.NewGuid():N}");
            var workspace = Path.Combine(root, "workspace");
            Directory.CreateDirectory(workspace);

            var shellScriptPath = Path.Combine(root, "fake-cli.sh");
            File.WriteAllText(shellScriptPath, "#!/bin/sh\ncat >/dev/null\n" + unixScript + "\n");
            return new CliFixture(root, workspace, "/bin/sh", shellScriptPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
