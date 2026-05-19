using System.Text.Json.Nodes;
using Tinadec.AgentCore.Services;
using Tinadec.AgentCore.Storage;
using Tinadec.Contracts.Models;

namespace Tinadec.AgentCore.Tests;

public sealed class CoreStoreTests
{
    [Fact]
    public void PersistsProjectsSessionsMessagesApprovalsAndEvents()
    {
        var db = Path.Combine(Path.GetTempPath(), $"tinadec-test-{Guid.NewGuid():N}.db");
        var store = new CoreStore(db);
        store.Initialize();

        var project = store.CreateProject("TinadecCode", Environment.CurrentDirectory);
        var session = store.CreateSession(project.Id, "MVP");
        var message = store.AddMessage(session.Id, "user", "hello");
        var approval = store.CreateApproval(new CreateApprovalRequest(session.Id, "shell", "npm test", "npm test", project.Path));
        var envelope = store.AppendNewEvent("message.created", session.Id, new JsonObject { ["message_id"] = message.Id }, ["agent.message"]);

        Assert.Single(store.ListProjects());
        Assert.Single(store.ListSessions(project.Id));
        Assert.Single(store.ListMessages(session.Id));
        Assert.Single(store.ListApprovals("pending", session.Id));
        Assert.Equal(1, envelope.Seq);
        Assert.Equal(approval.Id, store.DecideApproval(approval.Id, "approved")?.Id);
    }

    [Fact]
    public void ExtensionInstallRequiresApprovalAndInstallsDisabled()
    {
        var db = Path.Combine(Path.GetTempPath(), $"tinadec-test-{Guid.NewGuid():N}.db");
        var store = new CoreStore(db);
        store.Initialize();

        var catalogItem = Assert.Single(store.ListMarketCatalog("skill", null, null));
        var preview = store.PreviewExtensionInstall(new InstallExtensionPreviewRequest(catalogItem.CatalogId, null, null, null));
        Assert.True(preview.RequiresApproval);

        var pending = store.InstallExtension(new InstallExtensionRequest(catalogItem.CatalogId, null, null, null, null));
        Assert.True(pending.ApprovalRequired);
        Assert.NotNull(pending.Approval);
        Assert.Empty(store.ListInstalledExtensions());

        store.DecideApproval(pending.Approval!.Id, "approved");
        var installed = store.InstallExtension(new InstallExtensionRequest(catalogItem.CatalogId, null, null, null, pending.Approval.Id));

        Assert.False(installed.ApprovalRequired);
        Assert.NotNull(installed.Extension);
        Assert.False(installed.Extension!.Enabled);
        Assert.Equal("installed_disabled", installed.Extension.Status);
    }

    [Fact]
    public void EnablingMcpAndAcpExtensionsPopulatesRuntimeRegistries()
    {
        var db = Path.Combine(Path.GetTempPath(), $"tinadec-test-{Guid.NewGuid():N}.db");
        var store = new CoreStore(db);
        store.Initialize();

        foreach (var kind in new[] { "mcp-server", "acp-adapter" })
        {
            var catalogItem = Assert.Single(store.ListMarketCatalog(kind, null, null));
            var pending = store.InstallExtension(new InstallExtensionRequest(catalogItem.CatalogId, null, null, null, null));
            store.DecideApproval(pending.Approval!.Id, "approved");
            var installed = store.InstallExtension(new InstallExtensionRequest(catalogItem.CatalogId, null, null, null, pending.Approval.Id)).Extension!;
            store.SetExtensionEnabled(installed.Id, true);
        }

        Assert.Single(store.ListMcpServers());
        Assert.Single(store.ListAcpAdapters());
    }

    [Fact]
    public void CreatesAuditableTwoLayerOrchestrationSnapshot()
    {
        var db = Path.Combine(Path.GetTempPath(), $"tinadec-test-{Guid.NewGuid():N}.db");
        var store = new CoreStore(db);
        store.Initialize();

        var project = store.CreateProject("TinadecCode", Environment.CurrentDirectory);
        var session = store.CreateSession(project.Id, "Agent runtime");
        var message = store.AddMessage(session.Id, "user", "implement the two-layer agent runtime");

        var snapshot = store.CreateOrchestrationRun(session.Id, message.Id, message.Content);

        Assert.NotNull(snapshot.Run);
        Assert.NotNull(snapshot.Graph);
        Assert.Equal(5, snapshot.Nodes.Count);
        Assert.Equal(snapshot.Nodes.Count, snapshot.Assignments.Count);
        Assert.Equal(snapshot.Nodes.Count, snapshot.StepResults.Count);
        Assert.Single(snapshot.ContextPacks);
        Assert.Single(snapshot.SupervisionFindings);
        Assert.Contains(snapshot.Assignments, assignment => assignment.AgentType == "search-agent");
        Assert.Contains(snapshot.Assignments, assignment => assignment.AgentType == "synthesis-model-agent");
    }

    [Fact]
    public void DisabledExecutionAgentIsNotAssigned()
    {
        var db = Path.Combine(Path.GetTempPath(), $"tinadec-test-{Guid.NewGuid():N}.db");
        var store = new CoreStore(db);
        store.Initialize();

        var testAgent = Assert.Single(store.ListAgentProfiles(), agent => agent.AgentType == "testing-agent");
        store.SaveAgentProfile(testAgent.Id, new SaveAgentProfileRequest(
            testAgent.Name,
            testAgent.Layer,
            testAgent.AgentType,
            testAgent.Mode,
            testAgent.Description,
            testAgent.ModelRoutePurpose,
            testAgent.AllowedTools,
            testAgent.Capabilities,
            false));

        var project = store.CreateProject("TinadecCode", Environment.CurrentDirectory);
        var session = store.CreateSession(project.Id, "Agent runtime");
        var message = store.AddMessage(session.Id, "user", "plan and validate the change");

        var snapshot = store.CreateOrchestrationRun(session.Id, message.Id, message.Content);

        Assert.DoesNotContain(snapshot.Assignments, assignment => assignment.AgentType == "testing-agent");
        Assert.Contains(snapshot.Nodes, node => node.Title == "Prepare validation");
        Assert.Equal(snapshot.Nodes.Count - 1, snapshot.Assignments.Count);
    }

    [Fact]
    public void EvolutionAgentCandidatesRemainProposed()
    {
        var db = Path.Combine(Path.GetTempPath(), $"tinadec-test-{Guid.NewGuid():N}.db");
        var store = new CoreStore(db);
        store.Initialize();

        var evolutionAgent = Assert.Single(store.ListAgentProfiles(), agent => agent.Id == "agent_evolution_algorithm");
        Assert.Equal("Evolution Algorithm Agent", evolutionAgent.Name);
        Assert.Equal("evolution-algorithm", evolutionAgent.AgentType);
        Assert.All(store.ListAgentCandidates(), candidate => Assert.Equal("proposed", candidate.Status));
    }

    [Fact]
    public void SeedsRequestedPlanningAndExecutionAgentsFromCore()
    {
        var db = Path.Combine(Path.GetTempPath(), $"tinadec-test-{Guid.NewGuid():N}.db");
        var store = new CoreStore(db);
        store.Initialize();

        var agents = store.ListAgentProfiles();

        Assert.Contains(agents, agent => agent.Id == "agent_meeting" && agent.Layer == "planning");
        Assert.Contains(agents, agent => agent.Id == "agent_tool_manager" && agent.Layer == "planning");
        Assert.Contains(agents, agent => agent.Id == "agent_evolution_algorithm" && agent.Layer == "planning");
        Assert.Contains(agents, agent => agent.Id == "agent_realtime_context_compressor" && agent.Layer == "planning");
        Assert.Contains(agents, agent => agent.Id == "agent_supervisor" && agent.Layer == "planning");
        Assert.Contains(agents, agent => agent.Id == "executor_planning_agent" && agent.Layer == "execution");
        Assert.Contains(agents, agent => agent.Id == "executor_testing_agent" && agent.Layer == "execution");
        Assert.Contains(agents, agent => agent.Id == "executor_search_agent" && agent.Layer == "execution");
        Assert.Contains(agents, agent => agent.Id == "executor_code_locator_agent" && agent.Layer == "execution");
        Assert.Contains(agents, agent => agent.Id == "executor_synthesis_model_agent" && agent.Layer == "execution");
        Assert.Contains(agents, agent => agent.Id == "executor_multimodal_model_agent" && agent.Layer == "execution");
        Assert.Contains(agents, agent => agent.Id == "executor_generation_model_agent" && agent.Layer == "execution");
        Assert.DoesNotContain(agents, agent => agent.Id.Contains("purifier", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(agents, agent => agent.Name.Contains("Purifier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RegistersProgrammingToolsAsCodeLayerCapabilities()
    {
        var registry = new ToolRegistryService();

        var tools = registry.ListTools("programming");

        Assert.Contains(tools, tool => tool.Id == "search_files" && tool.Source == "code" && !tool.RequiresApproval);
        Assert.Contains(tools, tool => tool.Id == "sandbox_exec" && tool.Source == "code" && tool.RequiresApproval);
        Assert.Contains(tools, tool => tool.Id == "apply_patch" && tool.Source == "code" && tool.RequiresApproval);
        Assert.Contains(tools, tool => tool.Id == "review_format" && tool.Source == "code" && !tool.RequiresApproval);
        Assert.All(tools, tool => Assert.Equal("programming", tool.Domain));
    }

    [Fact]
    public void CompilesTaskGraphIntoMicrosoftAgentWorkflowPlan()
    {
        var db = Path.Combine(Path.GetTempPath(), $"tinadec-test-{Guid.NewGuid():N}.db");
        var store = new CoreStore(db);
        store.Initialize();

        var project = store.CreateProject("TinadecCode", Environment.CurrentDirectory);
        var session = store.CreateSession(project.Id, "Workflow runtime");
        var message = store.AddMessage(session.Id, "user", "search code and prepare validation");
        var snapshot = store.CreateOrchestrationRun(session.Id, message.Id, message.Content);

        var runtime = new AgentWorkflowRuntime(new ToolRegistryService());
        var plan = runtime.Compile(snapshot);

        Assert.Equal(snapshot.Run!.Id, plan.RunId);
        Assert.Equal(AgentWorkflowRuntime.RuntimeName, plan.Runtime);
        Assert.Equal(snapshot.Assignments.Count, plan.Steps.Count);
        Assert.Contains(plan.Steps, step => step.AgentType == "search-agent" && step.ToolIds.Contains("search_files"));
        Assert.Contains(plan.Steps, step => step.AgentType == "testing-agent" && step.ToolIds.Contains("sandbox_exec"));
    }
}
