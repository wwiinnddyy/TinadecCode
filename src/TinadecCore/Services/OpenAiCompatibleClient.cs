using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Tinadec.AgentCore.Storage;
using Tinadec.Contracts.Models;

namespace Tinadec.AgentCore.Services;

public sealed class OpenAiCompatibleClient(HttpClient httpClient)
{
    public async Task<string> CreateAssistantReplyAsync(
        StoredModelSettings settings,
        string? apiKey,
        IReadOnlyList<MessageDto> messages,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.BaseUrl) ||
            string.IsNullOrWhiteSpace(settings.Model))
        {
            return "TinadecCode Agent Core is running. Add an OpenAI-compatible base URL and model to enable live model responses.";
        }

        using var request = BuildChatCompletionRequest(settings, apiKey, messages);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return $"Model request failed with {(int)response.StatusCode}: {Redact(body)}";
        }

        using var document = JsonDocument.Parse(body);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return string.IsNullOrWhiteSpace(content)
            ? "The model returned an empty response."
            : content;
    }

    public static HttpRequestMessage BuildChatCompletionRequest(
        StoredModelSettings settings,
        string? apiKey,
        IReadOnlyList<MessageDto> messages)
    {
        var endpoint = BuildChatCompletionsEndpoint(settings.BaseUrl);
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        var payload = new
        {
            model = settings.Model,
            stream = false,
            messages = messages.Select(message => new
            {
                role = message.Role,
                content = message.Content
            })
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, TinadecJson.Options),
            Encoding.UTF8,
            "application/json");

        return request;
    }

    public static Uri BuildChatCompletionsEndpoint(string baseUrl)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');
        if (!trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            trimmed += "/chat/completions";
        }

        return new Uri(trimmed, UriKind.Absolute);
    }

    private static string Redact(string body)
    {
        if (body.Length <= 300)
        {
            return body;
        }

        return body[..300] + "...";
    }
}
