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
            "agent_meeting_planner",
            "Meeting Planner",
            "planning",
            "meeting",
            "plan-first",
            "Turns user intent into a task graph, success criteria, dependencies, and approval points.",
            "planner",
            ["skill", "model.chat"],
            ["task_graph.create", "success_criteria.define", "approval_points.mark"]),
        new(
            "agent_tool_manager",
            "Tool Manager",
            "planning",
            "tool-manager",
            "balanced",
            "Selects model routes, tools, MCP servers, ACP adapters, and permission envelopes for each task node.",
            "tooling",
            ["skill", "mcp.list", "model.route"],
            ["toolkit.resolve", "model_route.resolve", "policy_scope.assign"]),
        new(
            "agent_context_compressor",
            "Context Compressor",
            "planning",
            "context-compressor",
            "balanced",
            "Maintains reversible context packs, evidence maps, and token budget summaries.",
            "context",
            ["message.read", "event.read"],
            ["context.compact", "evidence.map", "summary.expand"]),
        new(
            "agent_supervisor",
            "Supervisor",
            "planning",
            "supervisor",
            "safe-research",
            "Owns safety, cost, quality, cancellation, and approval gates across the run.",
            "supervisor",
            ["approval", "event.read", "policy"],
            ["authorize", "abort_run", "raise_alert", "budget.guard"]),
        new(
            "agent_purifier",
            "Evolution Agent",
            "planning",
            "evolution",
            "safe-research",
            "Observes repeated workflow patterns and proposes candidate skills, MCP manifests, prompts, or agent specs without hot-path publishing.",
            "evolution",
            ["event.read", "skill.read"],
            ["evolution.observe", "candidate.generate", "evaluation.plan", "skill.propose", "mcp.propose"]),
        new(
            "executor_plan",
            "Plan Executor",
            "execution",
            "plan-executor",
            "balanced",
            "Runs deterministic plan steps and reports structured step results.",
            "executor",
            ["task.step", "event.write"],
            ["step.run", "step.result"]),
        new(
            "executor_code_locator",
            "Code Locator",
            "execution",
            "code-locator",
            "safe-research",
            "Read-only worker for finding files, symbols, references, and relevant evidence.",
            "search",
            ["file.read", "grep", "glob"],
            ["code.search", "evidence.collect"]),
        new(
            "executor_search",
            "Search Executor",
            "execution",
            "search-executor",
            "safe-research",
            "Read-only worker for broad repository, docs, event, and extension search before code-specific localization.",
            "search",
            ["file.read", "grep", "glob", "event.read"],
            ["search.query", "evidence.collect"]),
        new(
            "executor_test",
            "Test Runner",
            "execution",
            "test-runner",
            "balanced",
            "Runs tests under approval and reports command, output, failure cause, and retry guidance.",
            "test",
            ["shell.approved", "event.write"],
            ["test.run", "failure.classify"]),
        new(
            "executor_browser_file_git",
            "Browser File Git Worker",
            "execution",
            "browser-file-git",
            "parallel",
            "Handles browser preview, file edits, and Git operations through explicit approval envelopes.",
            "executor",
            ["browser", "file.write.approved", "git.approved"],
            ["browser.inspect", "file.patch", "git.worktree"]),
        new(
            "executor_synthesis",
            "Synthesis Model Executor",
            "execution",
            "synthesis-executor",
            "balanced",
            "Combines model reasoning, collected evidence, and supervision notes into a structured next step.",
            "executor",
            ["model.chat", "event.read"],
            ["model.reason", "step.result"])
    ];

    public static IReadOnlyList<AgentCandidateSeed> Candidates { get; } =
    [
        new(
            "cand_purified_review_agent",
            "agent_purifier",
            "Evolution Review Agent",
            "execution",
            "review-executor",
            "Candidate generated from repeated review workflows. It would run read-only code review, cite files, and request a verifier before completion.",
            ["file.read", "grep", "git.diff"],
            ["Schema valid", "Read-only by default", "Needs golden-repo evaluation before enablement"]),
        new(
            "cand_mcp_packager_agent",
            "agent_purifier",
            "MCP Packager Agent",
            "planning",
            "tool-packager",
            "Candidate for turning repeated external tool setup steps into declarative MCP extension manifests.",
            ["event.read", "market.preview"],
            ["Requires extension signing workflow", "Must stay out of hot-path execution"])
    ];
}
