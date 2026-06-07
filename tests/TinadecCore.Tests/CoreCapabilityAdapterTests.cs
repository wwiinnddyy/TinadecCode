using Tinadec.Contracts.Models;
using TinadecCore.Abstractions;
using TinadecCore.Services;
using TinadecCore.Storage;

namespace TinadecCore.Tests;

public sealed class CoreCapabilityAdapterTests
{
    [Fact]
    public void CodexCapabilityProviderRegistersKernelBackedCapabilities()
    {
        var provider = new CodexCapabilityProvider();

        var capabilities = provider.ListCapabilities();

        Assert.Contains(capabilities, tool => tool.Id == "search_files" && tool.Source == "codex-rust");
        Assert.Contains(capabilities, tool => tool.Id == "read_file" && tool.Capabilities.Contains("codex-rust.active"));
        Assert.Contains(capabilities, tool => tool.Id == "grep_content" && tool.Risk == "read-only");
        Assert.Contains(capabilities, tool => tool.Id == "apply_patch" && tool.RequiresApproval);
        Assert.Contains(capabilities, tool => tool.Id == "sandbox_exec" && tool.RequiresApproval);
    }

    [Fact]
    public void CapabilityPolicyKeepsReadOnlyAutomaticAndMutatingApprovalGated()
    {
        var policy = new CapabilityPolicyService();
        var provider = new CodexCapabilityProvider();
        var readFile = provider.ListCapabilities().Single(tool => tool.Id == "read_file");
        var applyPatch = provider.ListCapabilities().Single(tool => tool.Id == "apply_patch");

        Assert.False(policy.Evaluate("approval", readFile).Required);
        Assert.True(policy.IsReadOnly(readFile));
        Assert.True(policy.Evaluate("approval", applyPatch).Required);
        Assert.False(policy.IsReadOnly(applyPatch));
    }

    [Fact]
    public void PromptContextCapabilityProviderRegistersReadOnlyCoreTool()
    {
        var registry = new ToolRegistryService();

        var tool = Assert.Single(registry.ListTools("agent-context"), item => item.Id == "prompt_context_resolve");

        Assert.Equal("core", tool.Source);
        Assert.Equal("read-only", tool.Risk);
        Assert.False(tool.RequiresApproval);
        Assert.Contains("prompt.context.resolve", tool.Capabilities);
    }

    [Fact]
    public void HarnessManifestKeepsDualLayerAgentsAndToolProvidersCoreOwned()
    {
        var store = new CoreStore(Path.Combine(Path.GetTempPath(), $"tinadec-harness-manifest-{Guid.NewGuid():N}.db"));
        store.Initialize();
        var service = new HarnessManifestService(store, new ToolRegistryService());

        var manifest = service.Build();

        Assert.Equal(AgentWorkflowRuntime.RuntimeName, manifest.Runtime);
        Assert.Contains("Core owns orchestration", manifest.OwnershipModel);
        Assert.Contains(manifest.AgentLayers, layer => layer.Layer == "planning" && layer.Role.Contains("planning", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(manifest.AgentLayers, layer => layer.Layer == "execution" && layer.Role.Contains("execution", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(manifest.ToolProviders, provider => provider.Source == "core" && provider.Layer == "core");
        Assert.Contains(manifest.ToolProviders, provider => provider.Source == "code" && provider.Layer == "tool-layer" && provider.ApprovalRequiredCount > 0);
        Assert.Contains(manifest.ToolProviders, provider => provider.Source == "codex-rust" && provider.Layer == "native-glue");
        Assert.Contains(manifest.ToolRisks, risk => risk.Risk == "read-only" && !risk.RequiresHumanCheckpoint);
        Assert.Contains(manifest.ToolRisks, risk => risk.Risk == "workspace-write" && risk.RequiresHumanCheckpoint);
        Assert.Contains(manifest.DesignNotes, note => note.Contains("not a second orchestration runtime", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ToolSearchRanksMetadataAndPreservesCoreRiskPolicy()
    {
        var service = new ToolSearchService(new ToolRegistryService());

        var results = service.Search("git worktree", source: "code", limit: 3);

        var top = Assert.Single(results.Take(1));
        Assert.Equal("git_worktree_manager", top.Tool.Id);
        Assert.True(top.Score > 0);
        Assert.Equal("tool-layer", top.ProviderLayer);
        Assert.True(top.RequiresHumanCheckpoint);
        Assert.Contains("Core approval", top.ApprovalSummary);
        Assert.Contains("capabilities", top.MatchedFields);
        Assert.Contains("id", top.MatchedFields);
    }

    [Fact]
    public void ToolSearchFiltersReadOnlyPrimitiveTools()
    {
        var service = new ToolSearchService(new ToolRegistryService());

        var results = service.Search("file", source: "codex-rust", risk: "read-only", limit: 10);

        Assert.NotEmpty(results);
        Assert.All(results, result =>
        {
            Assert.Equal("codex-rust", result.Tool.Source);
            Assert.Equal("read-only", result.Tool.Risk);
            Assert.False(result.RequiresHumanCheckpoint);
        });
        Assert.Contains(results, result => result.Tool.Id == "read_file");
    }

    [Fact]
    public void ToolExecutionTimelineAggregatesEventsWithStepEvidence()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), $"tinadec-tool-timeline-project-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectPath);
        var store = new CoreStore(Path.Combine(Path.GetTempPath(), $"tinadec-tool-timeline-{Guid.NewGuid():N}.db"));
        store.Initialize();
        var project = store.CreateProject("Timeline", projectPath);
        var session = store.CreateSession(project.Id, "Timeline session");
        var snapshot = store.CreateOrchestrationRun(session.Id, "msg_1", "Read the project file.");
        var runId = snapshot.Run?.Id ?? throw new InvalidOperationException("Run was not created.");
        var step = store.AddStepResult(
            runId,
            "node_read_file",
            "agent_tool_manager",
            "completed",
            "Read File completed with one evidence item.",
            ["file:README.md"]);
        store.AppendNewEvent("tool.execution.requested", session.Id, new System.Text.Json.Nodes.JsonObject
        {
            ["run_id"] = runId,
            ["tool_id"] = "read_file",
            ["requires_approval"] = false
        }, ["tool.execution"]);
        store.AppendNewEvent("tool.execution.completed", session.Id, new System.Text.Json.Nodes.JsonObject
        {
            ["run_id"] = runId,
            ["tool_id"] = "read_file",
            ["status"] = "completed",
            ["step_result_id"] = step.Id
        }, ["tool.execution", "step.result"]);
        var service = new ToolExecutionTimelineService(store, new ToolRegistryService());

        var timeline = service.ListForSession(session.Id);

        var item = Assert.Single(timeline);
        Assert.Equal("read_file", item.ToolId);
        Assert.Equal("Read File", item.ToolDisplayName);
        Assert.Equal("completed", item.Status);
        Assert.False(item.RequiresApproval);
        Assert.Equal(step.Id, item.StepResultId);
        Assert.Equal("Read File completed with one evidence item.", item.Summary);
        Assert.Contains("file:README.md", item.Evidence);
        Assert.Contains("tool.execution.requested", item.EventTypes);
        Assert.Contains("tool.execution.completed", item.EventTypes);
    }

    [Fact]
    public async Task CodexInvocationAdapterTranslatesCoreInvocationToCodeClient()
    {
        var client = new RecordingCodeToolClient();
        var adapter = new CodexToolInvocationAdapter(client);
        var tool = new CodexCapabilityProvider().ListCapabilities().Single(item => item.Id == "read_file");
        var request = new CodeToolExecuteRequest("sess_1", "run_1", "node_1", null, "D:\\repo", new Dictionary<string, object?>());

        var result = await adapter.InvokeAsync(tool, request);

        Assert.True(adapter.CanInvoke(tool));
        Assert.Equal("native", result.Status);
        Assert.Equal(tool.Id, client.ToolId);
        Assert.Equal(request.RunId, client.Request?.RunId);
    }

    [Fact]
    public async Task CodeInvocationAdapterAcceptsCodeSuiteTools()
    {
        var client = new RecordingCodeToolClient();
        var adapter = new CodexToolInvocationAdapter(client);
        var tool = new CodeCapabilityProvider().ListCapabilities().Single(item => item.Id == "project_template_scaffold");
        var request = new CodeToolExecuteRequest("sess_1", "run_1", "node_1", null, "D:\\repo", new Dictionary<string, object?>());

        var result = await adapter.InvokeAsync(tool, request);

        Assert.True(adapter.CanInvoke(tool));
        Assert.Equal("native", result.Status);
        Assert.Equal("project_template_scaffold", client.ToolId);
    }

    [Fact]
    public async Task CoreInvocationAdapterResolvesPromptContextWithoutReturningFullPrompt()
    {
        var store = new CoreStore(Path.Combine(Path.GetTempPath(), $"tinadec-core-adapter-{Guid.NewGuid():N}.db"));
        store.Initialize();
        var service = new PromptContextService(
            store,
            new ToolRegistryService(),
            new NullPromptContextPlannerRuntime());
        var adapter = new CoreToolInvocationAdapter(service);
        var tool = new PromptContextCapabilityProvider().ListCapabilities().Single(item => item.Id == "prompt_context_resolve");

        var result = await adapter.InvokeAsync(tool, new CodeToolExecuteRequest(
            "sess_1",
            "run_1",
            "node_1",
            null,
            Directory.GetCurrentDirectory(),
            new Dictionary<string, object?>
            {
                ["agent_id"] = "agent_meeting",
                ["mode"] = "plan-first",
                ["user_content"] = "simple preview"
            }));

        Assert.True(adapter.CanInvoke(tool));
        Assert.Equal("completed", result.Status);
        Assert.True(result.Data.ContainsKey("fragment_ids"));
        Assert.True(result.Data.ContainsKey("estimated_tokens"));
        Assert.False(result.Data.ContainsKey("system_prompt"));
        Assert.DoesNotContain("TinadecCode prompt context", result.Data.Values.Select(value => value?.ToString()));
    }

    private sealed class RecordingCodeToolClient : ICodeToolClient
    {
        public string? ToolId { get; private set; }
        public CodeToolExecuteRequest? Request { get; private set; }

        public Task<CodeToolExecuteResultDto> ExecuteAsync(
            ToolDescriptorDto tool,
            CodeToolExecuteRequest request,
            CancellationToken cancellationToken = default)
        {
            ToolId = tool.Id;
            Request = request;
            return Task.FromResult(new CodeToolExecuteResultDto(
                tool.Id,
                "native",
                "Recorded Codex invocation.",
                ["adapter:codex-rust"],
                new Dictionary<string, object?>(),
                false,
                null));
        }
    }

    private sealed class NullPromptContextPlannerRuntime : IPromptContextPlannerRuntime
    {
        public Task<PromptContextPlanDto?> TryCreatePlanAsync(
            PromptContextPlanningInput input,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PromptContextPlanDto?>(null);
        }
    }
}
