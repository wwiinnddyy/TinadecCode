using System.Text.Json.Nodes;
using Tinadec.Contracts.Events;
using Tinadec.Contracts.Models;
using TinadecCore.Abstractions;
using TinadecCore.Storage;

namespace TinadecCore.Services;

public sealed class ToolExecutionTimelineService(
    CoreStore store,
    IToolRegistry tools)
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;

    public IReadOnlyList<ToolExecutionTimelineItemDto> ListForSession(
        string sessionId,
        string? runId = null,
        int? limit = null)
    {
        var take = Math.Clamp(limit.GetValueOrDefault(DefaultLimit), 1, MaxLimit);
        var stepResultsByRun = new Dictionary<string, IReadOnlyDictionary<string, StepResultDto>>(StringComparer.OrdinalIgnoreCase);
        var items = new List<ToolExecutionTimelineBuilder>();

        foreach (var envelope in store.ListEvents(sessionId).Where(IsToolExecutionEvent))
        {
            var payload = envelope.Payload;
            var eventRunId = ReadString(payload, "run_id");
            if (string.IsNullOrWhiteSpace(eventRunId))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(runId) && !eventRunId.Equals(runId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var toolId = ReadString(payload, "tool_id");
            if (string.IsNullOrWhiteSpace(toolId))
            {
                continue;
            }

            var builder = envelope.Type.Equals("tool.execution.requested", StringComparison.OrdinalIgnoreCase)
                ? null
                : FindOpenItem(items, eventRunId, toolId);

            if (builder is null)
            {
                builder = CreateBuilder(envelope, eventRunId, sessionId, toolId);
                items.Add(builder);
            }

            ApplyEvent(builder, envelope, stepResultsByRun);
        }

        return items
            .OrderByDescending(item => item.UpdatedSeq)
            .Take(take)
            .Select(item => item.ToDto())
            .ToArray();
    }

    private static bool IsToolExecutionEvent(EventEnvelope envelope)
    {
        return envelope.Type.Equals("tool.execution.requested", StringComparison.OrdinalIgnoreCase)
            || envelope.Type.Equals("tool.execution.approval_required", StringComparison.OrdinalIgnoreCase)
            || envelope.Type.Equals("tool.execution.completed", StringComparison.OrdinalIgnoreCase)
            || envelope.Type.Equals("tool.execution.failed", StringComparison.OrdinalIgnoreCase);
    }

    private ToolExecutionTimelineBuilder CreateBuilder(
        EventEnvelope envelope,
        string runId,
        string sessionId,
        string toolId)
    {
        var tool = tools.Resolve(toolId);
        return new ToolExecutionTimelineBuilder
        {
            RunId = runId,
            SessionId = sessionId,
            ToolId = toolId,
            ToolDisplayName = tool?.DisplayName ?? toolId,
            Source = tool?.Source ?? "unknown",
            Risk = tool?.Risk ?? "unknown",
            RequiresApproval = tool?.RequiresApproval ?? ReadBool(envelope.Payload, "requires_approval"),
            Status = StatusFromEvent(envelope),
            Summary = $"Tool {toolId} was {StatusFromEvent(envelope).Replace('_', ' ')}.",
            RequestedAt = envelope.Ts,
            UpdatedAt = envelope.Ts,
            RequestedSeq = envelope.Seq,
            UpdatedSeq = envelope.Seq
        };
    }

    private void ApplyEvent(
        ToolExecutionTimelineBuilder builder,
        EventEnvelope envelope,
        IDictionary<string, IReadOnlyDictionary<string, StepResultDto>> stepResultsByRun)
    {
        builder.UpdatedAt = envelope.Ts;
        builder.UpdatedSeq = envelope.Seq;
        builder.EventTypes.Add(envelope.Type);

        var status = ReadString(envelope.Payload, "status") ?? StatusFromEvent(envelope);
        builder.Status = status;
        builder.RequiresApproval = builder.RequiresApproval || ReadBool(envelope.Payload, "requires_approval");

        var approvalId = ReadString(envelope.Payload, "approval_id");
        if (!string.IsNullOrWhiteSpace(approvalId))
        {
            builder.ApprovalId = approvalId;
        }

        var stepResultId = ReadString(envelope.Payload, "step_result_id");
        if (!string.IsNullOrWhiteSpace(stepResultId))
        {
            builder.StepResultId = stepResultId;
            var stepResult = ResolveStepResult(builder.RunId, stepResultId, stepResultsByRun);
            if (stepResult is not null)
            {
                builder.Summary = stepResult.Summary;
                builder.Evidence = stepResult.Evidence.ToArray();
            }
        }
        else
        {
            builder.Summary = builder.Status switch
            {
                "requested" => $"Requested {builder.ToolDisplayName}.",
                "approval_required" => $"Waiting for Core approval for {builder.ToolDisplayName}.",
                _ => builder.Summary
            };
        }
    }

    private StepResultDto? ResolveStepResult(
        string runId,
        string stepResultId,
        IDictionary<string, IReadOnlyDictionary<string, StepResultDto>> stepResultsByRun)
    {
        if (!stepResultsByRun.TryGetValue(runId, out var stepResults))
        {
            stepResults = store.GetOrchestrationSnapshotByRun(runId)?.StepResults
                .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, StepResultDto>(StringComparer.OrdinalIgnoreCase);
            stepResultsByRun[runId] = stepResults;
        }

        return stepResults.TryGetValue(stepResultId, out var stepResult) ? stepResult : null;
    }

    private static ToolExecutionTimelineBuilder? FindOpenItem(
        IReadOnlyList<ToolExecutionTimelineBuilder> items,
        string runId,
        string toolId)
    {
        return items
            .LastOrDefault(item =>
                item.RunId.Equals(runId, StringComparison.OrdinalIgnoreCase)
                && item.ToolId.Equals(toolId, StringComparison.OrdinalIgnoreCase)
                && !IsTerminalStatus(item.Status));
    }

    private static bool IsTerminalStatus(string status)
    {
        return status.Equals("completed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("failed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("blocked", StringComparison.OrdinalIgnoreCase);
    }

    private static string StatusFromEvent(EventEnvelope envelope)
    {
        return envelope.Type.ToLowerInvariant() switch
        {
            "tool.execution.requested" => "requested",
            "tool.execution.approval_required" => "approval_required",
            "tool.execution.completed" => "completed",
            "tool.execution.failed" => "failed",
            _ => "unknown"
        };
    }

    private static string? ReadString(JsonObject? payload, string key)
    {
        if (payload is null || !payload.TryGetPropertyValue(key, out var node) || node is null)
        {
            return null;
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        return node.ToString();
    }

    private static bool ReadBool(JsonObject? payload, string key)
    {
        if (payload is null || !payload.TryGetPropertyValue(key, out var node) || node is null)
        {
            return false;
        }

        return node is JsonValue value && value.TryGetValue<bool>(out var result) && result;
    }

    private sealed class ToolExecutionTimelineBuilder
    {
        public required string RunId { get; init; }
        public required string SessionId { get; init; }
        public required string ToolId { get; init; }
        public required string ToolDisplayName { get; init; }
        public required string Source { get; init; }
        public required string Risk { get; init; }
        public bool RequiresApproval { get; set; }
        public required string Status { get; set; }
        public string? ApprovalId { get; set; }
        public string? StepResultId { get; set; }
        public required string Summary { get; set; }
        public IReadOnlyList<string> Evidence { get; set; } = [];
        public DateTimeOffset RequestedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; set; }
        public long RequestedSeq { get; init; }
        public long UpdatedSeq { get; set; }
        public List<string> EventTypes { get; } = [];

        public ToolExecutionTimelineItemDto ToDto()
        {
            return new ToolExecutionTimelineItemDto(
                StepResultId ?? ApprovalId ?? $"tool_exec_{RequestedSeq}",
                RunId,
                SessionId,
                ToolId,
                ToolDisplayName,
                Source,
                Risk,
                RequiresApproval,
                Status,
                ApprovalId,
                StepResultId,
                Summary,
                Evidence,
                RequestedAt,
                UpdatedAt,
                RequestedSeq,
                UpdatedSeq,
                EventTypes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        }
    }
}
