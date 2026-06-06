namespace TinadecCore.Tracing;

/// <summary>
/// Span name constants for Agent Debug Studio tracing.
/// </summary>
public static class SpanNames
{
    public const string AgentTurn = "agent.turn";
    public const string AgentInference = "agent.inference";
    public const string AgentToolDispatch = "agent.tool_dispatch";
    public const string AgentToolExecution = "agent.tool_execution";
    public const string AgentApproval = "agent.approval";
    public const string AgentSupervision = "agent.supervision";
    public const string AgentContextPack = "agent.context_pack";
    public const string AgentSubAgentSpawn = "agent.sub_agent_spawn";
    public const string AgentWorkflowCompile = "agent.workflow_compile";
    public const string SqliteQuery = "sqlite.query";
    public const string ModelRequest = "model.request";
    public const string ModelRouteSelection = "model.route_selection";
    public const string ModelProviderInvocation = "model.provider_invocation";
}

/// <summary>
/// Attribute key constants for span tags.
/// </summary>
public static class SpanAttrs
{
    public const string ProviderId = "provider_id";
    public const string SessionId = "session_id";
    public const string RunId = "run_id";
    public const string AgentId = "agent_id";
    public const string AgentType = "agent_type";
    public const string UserMessageId = "user_message_id";
    public const string Model = "model";
    public const string ProviderInstanceId = "provider_instance_id";
    public const string Driver = "driver";
    public const string ConnectionKind = "connection_kind";
    public const string TokenIn = "token_in";
    public const string TokenOut = "token_out";
    public const string LatencyMs = "latency_ms";
    public const string ToolId = "tool_id";
    public const string TaskNodeId = "task_node_id";
    public const string AutoDispatch = "auto_dispatch";
    public const string AdapterId = "adapter_id";
    public const string PermissionMode = "permission_mode";
    public const string RequiresApproval = "requires_approval";
    public const string ApprovalId = "approval_id";
    public const string ApprovalKind = "kind";
    public const string ApprovalSummary = "summary";
    public const string ApprovalDecision = "decision";
    public const string ApprovalWaitMs = "wait_ms";
    public const string Severity = "severity";
    public const string Category = "category";
    public const string FindingStatus = "finding_status";
    public const string ContextPackId = "context_pack_id";
    public const string TokenBudget = "token_budget";
    public const string CompressionRatio = "compression_ratio";
    public const string ChildAgentId = "child_agent_id";
    public const string ChildAgentType = "child_agent_type";
    public const string ParentAgentId = "parent_agent_id";
    public const string Runtime = "runtime";
    public const string StepCount = "step_count";
    public const string Table = "table";
    public const string Operation = "operation";
    public const string DurationMs = "duration_ms";
    public const string BaseUrl = "base_url";
    public const string StatusCode = "status_code";
    public const string HasApiKey = "has_api_key";
    public const string Status = "status";
    public const string Simulated = "simulated";
    public const string RoutePurpose = "route_purpose";
    public const string ErrorCategory = "error_category";
    public const string RetryCount = "retry_count";
    public const string FallbackProviderId = "fallback_provider_id";
    public const string HealthStatus = "health_status";
    public const string MessageCount = "message_count";
}
