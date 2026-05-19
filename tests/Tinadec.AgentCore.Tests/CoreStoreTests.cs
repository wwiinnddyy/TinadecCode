using System.Text.Json.Nodes;
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
        Assert.Contains(snapshot.Assignments, assignment => assignment.AgentType == "search-executor");
        Assert.Contains(snapshot.Assignments, assignment => assignment.AgentType == "synthesis-executor");
    }

    [Fact]
    public void DisabledExecutionAgentIsNotAssigned()
    {
        var db = Path.Combine(Path.GetTempPath(), $"tinadec-test-{Guid.NewGuid():N}.db");
        var store = new CoreStore(db);
        store.Initialize();

        var testAgent = Assert.Single(store.ListAgentProfiles(), agent => agent.AgentType == "test-runner");
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

        Assert.DoesNotContain(snapshot.Assignments, assignment => assignment.AgentType == "test-runner");
        Assert.Contains(snapshot.Nodes, node => node.Title == "Prepare validation");
        Assert.Equal(snapshot.Nodes.Count - 1, snapshot.Assignments.Count);
    }

    [Fact]
    public void EvolutionAgentCandidatesRemainProposed()
    {
        var db = Path.Combine(Path.GetTempPath(), $"tinadec-test-{Guid.NewGuid():N}.db");
        var store = new CoreStore(db);
        store.Initialize();

        var evolutionAgent = Assert.Single(store.ListAgentProfiles(), agent => agent.Id == "agent_purifier");
        Assert.Equal("Evolution Agent", evolutionAgent.Name);
        Assert.Equal("evolution", evolutionAgent.AgentType);
        Assert.All(store.ListAgentCandidates(), candidate => Assert.Equal("proposed", candidate.Status));
    }
}
