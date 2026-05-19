using System.Text.Json;
using System.Text.Json.Nodes;
using Tinadec.Contracts.Events;

namespace Tinadec.Contracts.Tests;

public sealed class EventEnvelopeTests
{
    [Fact]
    public void SerializesWithSnakeCaseEnvelopeFields()
    {
        var envelope = EventEnvelope.Create(
            "agent.event.delta",
            42,
            "sess_123",
            new JsonObject { ["message_id"] = "msg_123" },
            ["tool.shell"]);

        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        Assert.Contains("\"request_id\"", json);
        Assert.Contains("\"session_id\":\"sess_123\"", json);
        Assert.Contains("\"trace_id\"", json);
        Assert.Contains("\"message_id\":\"msg_123\"", json);
    }
}
