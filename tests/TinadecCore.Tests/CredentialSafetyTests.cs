using System.Text.Json;
using System.Text.Json.Nodes;
using Tinadec.Contracts.Events;
using Tinadec.Contracts.Models;
using TinadecCore.Abstractions;
using TinadecCore.Services;

namespace TinadecCore.Tests;

public sealed class CredentialSafetyTests
{
    [Fact]
    public async Task InvalidApiKeyMapsToAuthenticationFailedWithoutLeakingSecret()
    {
        const string fakeSecret = "sk-test-do-not-leak";
        var context = CreateContext();
        var routeResolver = new FixedRouteResolver(context);
        var credentialResolver = new FixedCredentialResolver($" {fakeSecret} ");
        var runtime = new ModelInvocationRuntime(routeResolver, credentialResolver, Array.Empty<IModelProviderRuntime>());

        var result = await runtime.InvokeAsync("sess-1", "planner", Array.Empty<MessageDto>());

        Assert.Equal("failed", result.Status);
        Assert.Equal(ProviderErrorCategory.AuthenticationFailed, result.ErrorCategory);
        Assert.False(result.IsRetryable);
        Assert.False(ProviderCredentialValidator.ContainsRawSecret(result.Content, fakeSecret));
        Assert.False(ProviderCredentialValidator.ContainsRawSecret(result.SafeErrorMessage ?? string.Empty, fakeSecret));
    }

    [Fact]
    public void SafeErrorAndEventSerializationNeverContainRawSecret()
    {
        const string fakeSecret = "sk-test-do-not-leak";
        var failure = ProviderErrorMapper.FromHttpStatus("provider-openai", 401);
        var envelope = EventEnvelope.Create(
            type: "model.request.failed",
            seq: 1,
            sessionId: "sess-1",
            payload: new JsonObject
            {
                ["provider_id"] = failure.ProviderId,
                ["category"] = failure.Category.ToString(),
                ["retryable"] = failure.Retryable,
                ["safe_message"] = failure.SafeMessage
            },
            error: new TinadecError("MODEL_PROVIDER_ERROR", failure.SafeMessage));

        var output = JsonSerializer.Serialize(new
        {
            error = failure.SafeMessage,
            event_envelope = envelope
        });

        Assert.DoesNotContain(fakeSecret, output, StringComparison.Ordinal);
        Assert.Equal(ProviderErrorCategory.AuthenticationFailed, failure.Category);
    }

    private static ResolvedModelInvocationContextDto CreateContext()
    {
        return new ResolvedModelInvocationContextDto(
            "planner",
            null,
            null,
            "https://api.example.test/v1",
            "gpt-test",
            null,
            "openai-compatible",
            "http",
            "provider-openai",
            false);
    }

    private sealed class FixedRouteResolver(ResolvedModelInvocationContextDto context) : IModelRouteResolver
    {
        public ResolvedModelInvocationContextDto Resolve(string purpose) => context;
    }

    private sealed class FixedCredentialResolver(string? value) : IModelCredentialResolver
    {
        public string? ResolveApiKey(ResolvedModelInvocationContextDto context) => value;
    }
}
