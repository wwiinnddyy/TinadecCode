using Tinadec.Contracts.Events;
using Tinadec.Contracts.Models;

namespace TinadecCore.Abstractions;

public interface ISessionService
{
    IReadOnlyList<SessionDto> ListSessions(string? projectId);
    SessionDto CreateSession(string projectId, string? title);
    IReadOnlyList<MessageDto> ListMessages(string sessionId);
    MessageDto AddMessage(string sessionId, string role, string content);
}

public interface IApprovalService
{
    IReadOnlyList<ApprovalDto> ListApprovals(string? status, string? sessionId);
    ApprovalDto CreateApproval(CreateApprovalRequest request);
    ApprovalDto? DecideApproval(string approvalId, string decision);
}

public interface IEventLog
{
    IReadOnlyList<EventEnvelope> ListEvents(string? sessionId);
    EventEnvelope AppendNewEvent(string type, string? sessionId, IReadOnlyDictionary<string, object?>? payload, IReadOnlyList<string> capabilities);
}

public interface IModelProviderRegistry
{
    IReadOnlyList<ModelProviderTemplateDto> ListTemplates();
    IReadOnlyList<ModelProviderInstanceDto> ListProviders();
}

public interface IModelRouter
{
    IReadOnlyList<ModelRouteDto> ListRoutes();
    ModelRouteDto? GetRoute(string purpose);
}

public interface IExtensionCatalogService
{
    IReadOnlyList<ExtensionSourceDto> ListSources();
    IReadOnlyList<MarketCatalogItemDto> ListCatalog(string? kind, string? query, string? sourceId);
    MarketCatalogItemDto? GetCatalogItem(string catalogId);
}

public interface IExtensionInstallService
{
    ExtensionInstallPreviewDto PreviewInstall(InstallExtensionPreviewRequest request);
    ExtensionInstallResultDto InstallExtension(InstallExtensionRequest request);
    IReadOnlyList<InstalledExtensionDto> ListInstalledExtensions();
}

public interface IExtensionRuntimeRegistry
{
    IReadOnlyList<InstalledExtensionDto> ListEnabledExtensions();
}

public interface ISkillRegistry
{
    IReadOnlyList<InstalledExtensionDto> ListEnabledSkills();
}

public interface IMcpRegistry
{
    IReadOnlyList<McpServerDto> ListServers();
}

public interface IAcpRegistry
{
    IReadOnlyList<AcpAdapterDto> ListAdapters();
}

public interface IAgentProfileRegistry
{
    IReadOnlyList<AgentProfileDto> ListAgentProfiles();
    IReadOnlyList<AgentModeDto> ListAgentModes();
    IReadOnlyList<AgentCandidateDto> ListAgentCandidates();
}

public interface IOrchestrationRuntime
{
    OrchestrationSnapshotDto GetOrchestrationSnapshot(string sessionId);
    IReadOnlyList<OrchestrationRunDto> ListRuns(string sessionId);
    IReadOnlyList<TaskNodeDto> ListTaskNodes(string sessionId);
    IReadOnlyList<ContextPackDto> ListContextPacks(string sessionId);
    IReadOnlyList<SupervisionFindingDto> ListSupervisionFindings(string sessionId);
}

public interface IToolPermissionPolicy
{
    bool RequiresApproval(string capability);
}

public interface ICoreStore : ISessionService, IApprovalService, IModelRouter
{
    IReadOnlyList<ProjectDto> ListProjects();
    ProjectDto CreateProject(string name, string path);
}
