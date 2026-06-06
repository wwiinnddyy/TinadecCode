using System.Text.Json;
using System.Text.Json.Nodes;
using Tinadec.Contracts.Models;

namespace Tinadec.Contracts.Tests;

public sealed class ModelContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [Fact]
    public void ModelInvocationContractsSerializeWithSnakeCaseFields()
    {
        var request = new ModelInvocationRequestDto(
            Messages:
            [
                new MessageDto("msg_1", "sess_1", "user", "Hello", DateTimeOffset.UnixEpoch)
            ],
            SystemPrompt: "Be concise.",
            Tools:
            [
                new JsonObject
                {
                    ["name"] = "search",
                    ["description"] = "Search workspace"
                }
            ],
            Settings: new ModelSettingsDto("https://example.test", "model-a", true, DateTimeOffset.UnixEpoch),
            StateHandle: new ModelStateHandleDto("state_1", DateTimeOffset.UnixEpoch));

        var response = new ModelInvocationResponseDto(
            TextContent: "Done",
            Usage: new ModelUsageDto(10, 5, 15),
            FinishReason: ModelFinishReason.ToolCalls,
            Metadata: new ProviderMetadataDto(
                ProviderId: "provider_1",
                Model: "model-a",
                RawProviderName: "ExampleProvider",
                Custom: new Dictionary<string, object?> { ["request_id"] = "req_1" }),
            StateHandle: new ModelStateHandleDto("state_2", DateTimeOffset.UnixEpoch),
            ErrorCategory: ProviderErrorCategory.RateLimited,
            ErrorMessage: "Too many requests");

        var requestJson = JsonSerializer.Serialize(request, JsonOptions);
        var responseJson = JsonSerializer.Serialize(response, JsonOptions);

        Assert.Contains("\"system_prompt\":\"Be concise.\"", requestJson);
        Assert.Contains("\"state_handle\"", requestJson);
        Assert.Contains("\"text_content\":\"Done\"", responseJson);
        Assert.Contains("\"prompt_tokens\":10", responseJson);
        Assert.Contains("\"completion_tokens\":5", responseJson);
        Assert.Contains("\"total_tokens\":15", responseJson);
        Assert.Contains("\"finish_reason\":\"tool_calls\"", responseJson);
        Assert.Contains("\"provider_id\":\"provider_1\"", responseJson);
        Assert.Contains("\"raw_provider_name\":\"ExampleProvider\"", responseJson);
        Assert.Contains("\"error_category\":\"rate_limited\"", responseJson);
    }

    [Fact]
    public void ProviderErrorCategoriesIncludeRequiredNormalizedValues()
    {
        var values = Enum.GetNames<ProviderErrorCategory>();

        Assert.Equal(
            [
                "AuthenticationFailed",
                "RateLimited",
                "Timeout",
                "ProviderUnavailable",
                "InvalidRequest",
                "Cancelled",
                "Unknown"
            ],
            values);
    }

    [Fact]
    public void ProviderCapabilityMetadataRepresentsRequiredFields()
    {
        var capability = new ProviderCapabilityDto(
            SupportsStreaming: true,
            SupportsTools: true,
            SupportsJsonMode: true,
            SupportsSystemPrompt: true,
            MaxContextTokens: 128000,
            RequiresWorkspace: false,
            CredentialKind: "api_key",
            HealthStatus: ProviderHealthStatus.Cooldown);

        var json = JsonSerializer.Serialize(capability, JsonOptions);

        Assert.Contains("\"supports_streaming\":true", json);
        Assert.Contains("\"supports_tools\":true", json);
        Assert.Contains("\"supports_json_mode\":true", json);
        Assert.Contains("\"supports_system_prompt\":true", json);
        Assert.Contains("\"max_context_tokens\":128000", json);
        Assert.Contains("\"requires_workspace\":false", json);
        Assert.Contains("\"credential_kind\":\"api_key\"", json);
        Assert.Contains("\"health_status\":\"cooldown\"", json);
    }

    [Fact]
    public void ProviderSpecificWireFieldsDoNotLeakToNormalizedDtoTopLevel()
    {
        var response = new ModelInvocationResponseDto(
            TextContent: "Done",
            Usage: new ModelUsageDto(1, 2, 3),
            FinishReason: ModelFinishReason.Stop,
            Metadata: new ProviderMetadataDto(
                ProviderId: "provider_1",
                Model: "model-a",
                RawProviderName: "ExampleProvider",
                Custom: new Dictionary<string, object?>
                {
                    ["previous_response_id"] = "resp_1",
                    ["choices"] = 1
                }),
            StateHandle: null,
            ErrorCategory: null,
            ErrorMessage: null);

        var json = JsonSerializer.Serialize(response, JsonOptions);
        var root = JsonNode.Parse(json)!.AsObject();

        Assert.DoesNotContain("tool_use", root.Select(property => property.Key));
        Assert.DoesNotContain("previous_response_id", root.Select(property => property.Key));
        Assert.DoesNotContain("choices", root.Select(property => property.Key));
    }
}
