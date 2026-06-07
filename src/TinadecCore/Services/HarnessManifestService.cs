using Tinadec.Contracts.Models;
using TinadecCore.Abstractions;
using TinadecCore.Storage;

namespace TinadecCore.Services;

public sealed class HarnessManifestService(
    CoreStore store,
    IToolRegistry tools)
{
    public HarnessManifestDto Build()
    {
        var toolList = tools.ListTools()
            .OrderBy(tool => tool.Source, StringComparer.OrdinalIgnoreCase)
            .ThenBy(tool => tool.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var agents = store.ListAgentProfiles();
        var modes = store.ListAgentModes();

        return new HarnessManifestDto(
            AgentWorkflowRuntime.RuntimeName,
            "Core owns orchestration, approvals, model routing, tool policy, and audit events; Gateway and Desktop only present or proxy this manifest.",
            BuildAgentLayers(agents, modes),
            BuildToolProviders(toolList),
            BuildToolRisks(toolList),
            toolList,
            [
                "Planning agents create and supervise task graphs; execution agents complete assigned nodes with scoped tools.",
                "Tool layer providers declare capabilities; Core decides approval, dispatch, tracing, and state recording.",
                "Code remains a built-in Tool-layer suite, not a second orchestration runtime."
            ]);
    }

    private static IReadOnlyList<AgentLayerManifestDto> BuildAgentLayers(
        IReadOnlyList<AgentProfileDto> agents,
        IReadOnlyList<AgentModeDto> modes)
    {
        return agents
            .GroupBy(agent => agent.Layer, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key.Equals("planning", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .Select(group =>
            {
                var layerAgents = group.ToArray();
                var layerModes = layerAgents
                    .Select(agent => modes.FirstOrDefault(mode => mode.Id.Equals(agent.Mode, StringComparison.OrdinalIgnoreCase)))
                    .Where(mode => mode is not null)
                    .Cast<AgentModeDto>()
                    .ToArray();

                return new AgentLayerManifestDto(
                    group.Key,
                    LayerRole(group.Key),
                    layerAgents.Length,
                    layerAgents.Count(agent => agent.Enabled),
                    layerModes.Length == 0 ? 1 : layerModes.Max(mode => mode.MaxParallelExecutors),
                    layerModes.Any(mode => mode.WorktreeIsolation),
                    layerModes.Any(mode => mode.ApprovalRequired),
                    layerAgents
                        .Select(agent => agent.AgentType)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    layerAgents
                        .SelectMany(agent => agent.AllowedTools)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToArray());
            })
            .ToArray();
    }

    private static IReadOnlyList<ToolProviderManifestDto> BuildToolProviders(IReadOnlyList<ToolDescriptorDto> toolList)
    {
        return toolList
            .GroupBy(tool => tool.Source, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => SourceSortKey(group.Key))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var providerTools = group.ToArray();
                var capabilityPrefixes = providerTools
                    .SelectMany(tool => tool.Capabilities)
                    .Select(CapabilityPrefix)
                    .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(prefix => prefix, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return new ToolProviderManifestDto(
                    group.Key,
                    SourceDisplayName(group.Key),
                    SourceLayer(group.Key),
                    ProviderStatus(providerTools),
                    providerTools.Length,
                    providerTools.Count(IsActiveTool),
                    providerTools.Count(IsFutureTool),
                    providerTools.Count(tool => tool.RequiresApproval),
                    providerTools.Count(IsReadOnlyTool),
                    capabilityPrefixes);
            })
            .ToArray();
    }

    private static IReadOnlyList<ToolRiskManifestDto> BuildToolRisks(IReadOnlyList<ToolDescriptorDto> toolList)
    {
        return toolList
            .GroupBy(tool => tool.Risk, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => RiskSortKey(group.Key))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ToolRiskManifestDto(
                group.Key,
                group.Count(),
                group.Any(tool => tool.RequiresApproval) || !group.Key.Equals("read-only", StringComparison.OrdinalIgnoreCase),
                RiskPolicySummary(group.Key)))
            .ToArray();
    }

    private static string LayerRole(string layer)
    {
        return layer.ToLowerInvariant() switch
        {
            "planning" => "Active planning and supervision layer",
            "execution" => "Passive task execution and evidence layer",
            _ => "Custom agent layer"
        };
    }

    private static string SourceDisplayName(string source)
    {
        return source.ToLowerInvariant() switch
        {
            "core" => "Tinadec Core",
            "code" => "Code Tool Suite",
            "codex-rust" => "Codex Rust Primitives",
            _ => source
        };
    }

    private static string SourceLayer(string source)
    {
        return source.ToLowerInvariant() switch
        {
            "core" => "core",
            "code" => "tool-layer",
            "codex-rust" => "native-glue",
            _ => "extension"
        };
    }

    private static string ProviderStatus(IReadOnlyList<ToolDescriptorDto> providerTools)
    {
        if (providerTools.All(IsFutureTool))
        {
            return "future";
        }

        return providerTools.Any(IsFutureTool) ? "mixed" : "active";
    }

    private static bool IsActiveTool(ToolDescriptorDto tool)
    {
        return tool.Capabilities.Any(capability => capability.EndsWith(".active", StringComparison.OrdinalIgnoreCase))
            || !IsFutureTool(tool);
    }

    private static bool IsFutureTool(ToolDescriptorDto tool)
    {
        return tool.Capabilities.Any(capability => capability.EndsWith(".future", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsReadOnlyTool(ToolDescriptorDto tool)
    {
        return tool.Risk.Equals("read-only", StringComparison.OrdinalIgnoreCase) && !tool.RequiresApproval;
    }

    private static string CapabilityPrefix(string capability)
    {
        var dot = capability.IndexOf('.');
        return dot <= 0 ? capability : capability[..dot];
    }

    private static int SourceSortKey(string source)
    {
        return source.ToLowerInvariant() switch
        {
            "core" => 0,
            "code" => 1,
            "codex-rust" => 2,
            _ => 3
        };
    }

    private static int RiskSortKey(string risk)
    {
        return risk.ToLowerInvariant() switch
        {
            "read-only" => 0,
            "workspace-write" => 1,
            "shell" => 2,
            "git-write" => 3,
            "external-url" => 4,
            _ => 5
        };
    }

    private static string RiskPolicySummary(string risk)
    {
        return risk.ToLowerInvariant() switch
        {
            "read-only" => "Auto-dispatchable when the tool does not request approval.",
            "workspace-write" => "Requires a human checkpoint before writing workspace files.",
            "shell" => "Requires approval because commands can mutate state or leak data.",
            "git-write" => "Requires approval because branch, worktree, commit, or history state may change.",
            "external-url" => "Requires approval because external data egress is involved.",
            _ => "Unknown risks default to a human checkpoint."
        };
    }
}
