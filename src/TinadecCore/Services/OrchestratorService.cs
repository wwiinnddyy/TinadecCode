using System.Text.Json.Nodes;
using Tinadec.AgentCore.Storage;
using Tinadec.Contracts.Models;

namespace Tinadec.AgentCore.Services;

public sealed class OrchestratorService
{
    private readonly CoreStore _store;
    private readonly EventHub _events;

    public OrchestratorService(CoreStore store, EventHub events)
    {
        _store = store;
        _events = events;
    }

    public OrchestrationSnapshotDto CreateRunForMessage(string sessionId, string userMessageId, string userContent)
    {
        var snapshot = _store.CreateOrchestrationRun(sessionId, userMessageId, userContent);
        if (snapshot.Run is null)
        {
            return snapshot;
        }

        Publish("run.started", sessionId, new JsonObject
        {
            ["run_id"] = snapshot.Run.Id,
            ["summary"] = snapshot.Run.Summary,
            ["status"] = snapshot.Run.Status
        }, ["agent.run"]);

        if (snapshot.Graph is not null)
        {
            Publish("task_graph.created", sessionId, new JsonObject
            {
                ["run_id"] = snapshot.Run.Id,
                ["graph_id"] = snapshot.Graph.Id,
                ["node_count"] = snapshot.Nodes.Count
            }, ["task_graph.create"]);
        }

        foreach (var assignment in snapshot.Assignments)
        {
            Publish("task.assigned", sessionId, new JsonObject
            {
                ["run_id"] = assignment.RunId,
                ["task_node_id"] = assignment.TaskNodeId,
                ["agent_id"] = assignment.AgentId,
                ["agent_type"] = assignment.AgentType,
                ["permission_mode"] = assignment.PermissionMode
            }, ["task.assign", "agent.execution"]);
        }

        foreach (var result in snapshot.StepResults)
        {
            Publish("step.result.created", sessionId, new JsonObject
            {
                ["run_id"] = result.RunId,
                ["task_node_id"] = result.TaskNodeId,
                ["agent_id"] = result.AgentId,
                ["status"] = result.Status
            }, ["step.result"]);
        }

        foreach (var finding in snapshot.SupervisionFindings)
        {
            Publish("supervision.checked", sessionId, new JsonObject
            {
                ["run_id"] = finding.RunId,
                ["severity"] = finding.Severity,
                ["category"] = finding.Category,
                ["status"] = finding.Status
            }, ["supervisor.check"]);
        }

        foreach (var pack in snapshot.ContextPacks)
        {
            Publish("context.pack.created", sessionId, new JsonObject
            {
                ["run_id"] = pack.RunId,
                ["context_pack_id"] = pack.Id,
                ["token_budget"] = pack.TokenBudget,
                ["compression_ratio"] = pack.CompressionRatio
            }, ["context.compact"]);
        }

        return snapshot;
    }

    private void Publish(string type, string sessionId, JsonObject payload, IReadOnlyList<string> capabilities)
    {
        var envelope = _store.AppendNewEvent(type, sessionId, payload, capabilities);
        _events.Publish(envelope);
    }
}
