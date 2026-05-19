using Tinadec.Contracts.Models;

namespace Tinadec.AgentCore.Services;

public sealed record AgentProfileSeed(
    string Id,
    string Name,
    string Layer,
    string AgentType,
    string Mode,
    string Description,
    string ModelRoutePurpose,
    IReadOnlyList<string> AllowedTools,
    IReadOnlyList<string> Capabilities);

public sealed record AgentCandidateSeed(
    string Id,
    string GeneratedByAgentId,
    string Name,
    string Layer,
    string AgentType,
    string Description,
    IReadOnlyList<string> SuggestedTools,
    IReadOnlyList<string> EvaluationNotes);

public static class AgentCatalog
{
    public static IReadOnlyList<AgentModeDto> Modes { get; } =
    [
        new("balanced", "Balanced", "Default two-layer orchestration with one planner lane and two executor lanes.", 2, true, true, "balanced"),
        new("plan-first", "Plan First", "Meeting and supervisor agents draft a task graph before execution starts.", 1, true, true, "strict"),
        new("parallel", "Parallel", "Allows more execution-layer workers when dependencies and budget allow it.", 4, true, true, "performance"),
        new("safe-research", "Safe Research", "Read-only exploration with strict approval gates for writes, shell, network, and ACP.", 2, false, true, "strict")
    ];

    public static IReadOnlyList<AgentProfileSeed> Profiles { get; } =
    [
        new(
            "agent_meeting",
            "Meeting Agent",
            "planning",
            "meeting",
            "plan-first",
            "Turns user intent into a task graph, success criteria, dependencies, and approval points.",
            "planner",
            ["skill", "model.chat"],
            ["task_graph.create", "success_criteria.define", "approval_points.mark"]),
        new(
            "agent_tool_manager",
            "Tool Management Agent",
            "planning",
            "tool-manager",
            "balanced",
            "Selects model routes, tools, MCP servers, ACP adapters, and permission envelopes for each task node.",
            "tooling",
            ["skill", "mcp.list", "model.route"],
            ["toolkit.resolve", "model_route.resolve", "policy_scope.assign"]),
        new(
            "agent_realtime_context_compressor",
            "Real-time Context Compression Agent",
            "planning",
            "realtime-context-compressor",
            "balanced",
            "Continuously detects repeated context patterns and maintains reversible context packs, evidence maps, and token budget summaries.",
            "context",
            ["message.read", "event.read"],
            ["context.compact", "context.pattern.detect", "evidence.map", "summary.expand"]),
        new(
            "agent_supervisor",
            "Supervisor Agent",
            "planning",
            "supervisor",
            "safe-research",
            "Owns safety, cost, quality, cancellation, and approval gates across the run.",
            "supervisor",
            ["approval", "event.read", "policy"],
            ["authorize", "abort_run", "raise_alert", "budget.guard"]),
        new(
            "agent_evolution_algorithm",
            "Evolution Algorithm Agent",
            "planning",
            "evolution-algorithm",
            "safe-research",
            "Observes repeated workflow patterns and proposes candidate skills, MCP manifests, prompts, or agent specs without hot-path publishing.",
            "evolution",
            ["event.read", "skill.read"],
            ["evolution.observe", "pattern.learn", "candidate.generate", "evaluation.plan", "skill.propose", "mcp.propose", "executor.propose"]),
        new(
            "executor_planning_agent",
            "Planning Agent",
            "execution",
            "planning-agent",
            "balanced",
            "Runs deterministic plan steps and reports structured step results.",
            "executor",
            ["task.step", "event.write"],
            ["step.run", "step.result"]),
        new(
            "executor_search_agent",
            "Search Agent",
            "execution",
            "search-agent",
            "safe-research",
            "Read-only worker for broad repository, docs, event, and extension search before code-specific localization.",
            "search",
            ["file.read", "grep", "glob", "event.read"],
            ["search.query", "evidence.collect"]),
        new(
            "executor_code_locator_agent",
            "Code Locator Agent",
            "execution",
            "code-locator-agent",
            "safe-research",
            "Read-only worker for finding files, symbols, references, and relevant code evidence.",
            "search",
            ["file.read", "grep", "glob"],
            ["code.search", "symbol.locate", "evidence.collect"]),
        new(
            "executor_testing_agent",
            "Testing Agent",
            "execution",
            "testing-agent",
            "balanced",
            "Runs tests under approval and reports command, output, failure cause, and retry guidance.",
            "test",
            ["shell.approved", "event.write"],
            ["test.run", "failure.classify"]),
        new(
            "executor_synthesis_model_agent",
            "Synthesis Model Agent",
            "execution",
            "synthesis-model-agent",
            "balanced",
            "Combines language-model reasoning, collected evidence, and supervision notes into a structured next step.",
            "executor",
            ["model.chat", "event.read"],
            ["model.reason", "step.result"]),
        new(
            "executor_multimodal_model_agent",
            "Multimodal Model Agent",
            "execution",
            "multimodal-model-agent",
            "balanced",
            "Execution-layer placeholder for user-configured multimodal models that can reason over text and visual evidence.",
            "executor",
            ["model.multimodal", "event.read"],
            ["model.vision", "evidence.inspect"]),
        new(
            "executor_generation_model_agent",
            "Generation Model Agent",
            "execution",
            "generation-model-agent",
            "balanced",
            "Execution-layer placeholder for user-configured generation models such as image, audio, video, or artifact generation providers.",
            "executor",
            ["model.generate", "event.write"],
            ["artifact.generate", "generation.plan"])
    ];

    public static IReadOnlyList<AgentCandidateSeed> Candidates { get; } =
    [
        new(
            "cand_evolution_review_agent",
            "agent_evolution_algorithm",
            "Evolved Review Agent",
            "execution",
            "review-executor",
            "Candidate generated from repeated review workflows. It would run read-only code review, cite files, and request a verifier before completion.",
            ["file.read", "grep", "git.diff"],
            ["Schema valid", "Read-only by default", "Needs golden-repo evaluation before enablement"]),
        new(
            "cand_mcp_packager_agent",
            "agent_evolution_algorithm",
            "MCP Packager Agent",
            "planning",
            "tool-packager",
            "Candidate for turning repeated external tool setup steps into declarative MCP extension manifests.",
            ["event.read", "market.preview"],
            ["Requires extension signing workflow", "Must stay out of hot-path execution"])
    ];
}
