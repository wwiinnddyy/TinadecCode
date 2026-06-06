using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Tinadec.Contracts.Models;
using TinadecCore.Abstractions;
using TinadecCore.Services;
using TinadecCore.Storage;

namespace TinadecCore.Tests;

public sealed class AnthropicProviderRuntimeTests
{
    [Fact]
    public async Task BuildsAnthropicMessagesRequestFromNormalizedMessages()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return AnthropicSuccessResponse("Hello from Claude");
        });
        var client = new AnthropicClient(new HttpClient(handler));
        var settings = new StoredModelSettings("https://api.anthropic.com/v1/", "claude-test", null, DateTimeOffset.UtcNow);

        await client.CreateAssistantResponseAsync(settings, "anthropic-secret", CreateMessages(), "provider-anthropic", CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("https://api.anthropic.com/v1/messages", capturedRequest.RequestUri!.ToString());
        Assert.Equal("anthropic-secret", capturedRequest.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", capturedRequest.Headers.GetValues("anthropic-version").Single());
        Assert.Null(capturedRequest.Headers.Authorization);

        using var document = JsonDocument.Parse(capturedBody!);
        var root = document.RootElement;
        Assert.Equal("claude-test", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal("You are concise.", root.GetProperty("system").GetString());
        var messages = root.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Equal("text", messages[0].GetProperty("content")[0].GetProperty("type").GetString());
        Assert.Equal("Hello", messages[0].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal("assistant", messages[1].GetProperty("role").GetString());
    }

    [Fact]
    public async Task MapsAnthropicResponseToNormalizedResponse()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""
                {
                  "id": "msg_123",
                  "type": "message",
                  "role": "assistant",
                  "model": "claude-test",
                  "content": [
                    { "type": "text", "text": "First" },
                    { "type": "text", "text": " second" }
                  ],
                  "stop_reason": "end_turn",
                  "usage": { "input_tokens": 12, "output_tokens": 8 }
                }
                """)
        }));
        var client = new AnthropicClient(new HttpClient(handler));
        var settings = new StoredModelSettings("https://api.anthropic.com/v1", "claude-test", null, DateTimeOffset.UtcNow);

        var response = await client.CreateAssistantResponseAsync(settings, "anthropic-secret", CreateMessages(), "provider-anthropic", CancellationToken.None);

        Assert.Equal("First second", response.TextContent);
        Assert.Equal(new ModelUsageDto(12, 8, 20), response.Usage);
        Assert.Equal(ModelFinishReason.Stop, response.FinishReason);
        Assert.Equal("provider-anthropic", response.Metadata.ProviderId);
        Assert.Equal("claude-test", response.Metadata.Model);
        Assert.Equal("anthropic", response.Metadata.RawProviderName);
        Assert.Equal("msg_123", response.Metadata.Custom["message_id"]);
        Assert.Equal("message", response.Metadata.Custom["message_type"]);
        Assert.False(response.Metadata.Custom.ContainsKey("content"));
        Assert.Null(response.ErrorCategory);
        Assert.Null(response.ErrorMessage);
    }

    [Fact]
    public async Task ToolResultShapeGuardIsAnthropicAdapterLocal()
    {
        var settings = new StoredModelSettings("https://api.anthropic.com/v1", "claude-test", null, DateTimeOffset.UtcNow);
        var messages = new[]
        {
            new MessageDto("msg_1", "sess_1", "tool", "plain tool output", DateTimeOffset.UtcNow)
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var request = AnthropicClient.BuildMessagesRequest(settings, "anthropic-secret", messages);
            await request.Content!.ReadAsStringAsync();
        });

        Assert.Contains("tool_result", exception.Message);
        Assert.Contains("tool_use_id", exception.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ProviderErrorCategory.AuthenticationFailed, false)]
    [InlineData((HttpStatusCode)429, ProviderErrorCategory.RateLimited, true)]
    public async Task RuntimeMapsAnthropicHttpFailuresToNormalizedErrors(
        HttpStatusCode statusCode,
        ProviderErrorCategory expectedCategory,
        bool expectedRetryable)
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = JsonContent("{\"error\":{\"message\":\"anthropic-secret provider detail\"}}")
        }));
        var runtime = new AnthropicProviderRuntime(new AnthropicClient(new HttpClient(handler)));

        var result = await runtime.GenerateAsync(CreateContext(), "anthropic-secret", CreateMessages(), CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Equal(expectedCategory, result.ErrorCategory);
        Assert.Equal(expectedRetryable, result.IsRetryable);
        Assert.Equal((int)statusCode, result.ProviderStatusCode);
        Assert.Equal("provider-anthropic", result.ErrorProviderId);
        Assert.Equal(result.SafeErrorMessage, result.Content);
        Assert.DoesNotContain("anthropic-secret", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeMapsTimeoutWithoutLeakingCredential()
    {
        var handler = new StubHttpMessageHandler(_ => throw new TimeoutException("anthropic-secret timeout detail"));
        var runtime = new AnthropicProviderRuntime(new AnthropicClient(new HttpClient(handler)));

        var result = await runtime.GenerateAsync(CreateContext(), "anthropic-secret", CreateMessages(), CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Equal(ProviderErrorCategory.Timeout, result.ErrorCategory);
        Assert.True(result.IsRetryable);
        Assert.Null(result.ProviderStatusCode);
        Assert.Equal("Provider request timed out.", result.SafeErrorMessage);
        Assert.DoesNotContain("anthropic-secret", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeCanHandleAnthropicRequestThroughRegisteredModuleRuntime()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(AnthropicSuccessResponse("Runtime content")));
        var services = new ServiceCollection();
        services.AddSingleton(new AnthropicClient(new HttpClient(handler)));
        services.AddSingleton<IModelProviderRuntime, AnthropicProviderRuntime>();

        using var provider = services.BuildServiceProvider();
        var runtime = Assert.Single(provider.GetServices<IModelProviderRuntime>(), item => item.Id == "anthropic");

        var result = await runtime.GenerateAsync(CreateContext(), "anthropic-secret", CreateMessages(), CancellationToken.None);

        Assert.True(runtime.CanHandle(CreateContext()));
        Assert.Equal("executed", result.Status);
        Assert.Equal("Runtime content", result.Content);
        Assert.Equal("anthropic", result.RuntimeId);
        Assert.False(result.UsedStubResponse);
        Assert.Null(result.ErrorCategory);
    }

    private static MessageDto[] CreateMessages()
    {
        return
        [
            new MessageDto("msg_1", "sess_1", "system", "You are concise.", DateTimeOffset.UtcNow),
            new MessageDto("msg_2", "sess_1", "user", "Hello", DateTimeOffset.UtcNow),
            new MessageDto("msg_3", "sess_1", "assistant", "Hi", DateTimeOffset.UtcNow)
        ];
    }

    private static ResolvedModelInvocationContextDto CreateContext()
    {
        return new ResolvedModelInvocationContextDto(
            "planner",
            null,
            null,
            "https://api.anthropic.com/v1",
            "claude-test",
            null,
            "anthropic",
            "http",
            "provider-anthropic",
            false);
    }

    private static HttpResponseMessage AnthropicSuccessResponse(string text)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent($$"""
                {
                  "id": "msg_123",
                  "type": "message",
                  "role": "assistant",
                  "model": "claude-test",
                  "content": [{ "type": "text", "text": "{{text}}" }],
                  "stop_reason": "end_turn",
                  "usage": { "input_tokens": 3, "output_tokens": 4 }
                }
                """)
        };
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
}
