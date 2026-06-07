import type {
  AgentLayerManifestDto,
  CodeToolExecuteResultDto,
  HarnessManifestDto,
  ToolDescriptorDto,
  ToolProviderManifestDto,
  ToolRiskManifestDto,
  ToolSearchResultDto
} from './api';

export const CODE_SUITE_SOURCE = 'code';
const CODE_RUNTIME_IDS = new Set(['nodejs', 'bun', 'golang', 'flutter', 'python', 'rust', 'zig', 'nim', 'csharp', 'java']);

export interface ProjectTemplateSummary {
  id: string;
  name: string;
  language: string;
  package_manager: string;
  description?: string;
}

export function codeSuiteTools(tools: ToolDescriptorDto[]): ToolDescriptorDto[] {
  return tools
    .filter((tool) => tool.source === CODE_SUITE_SOURCE)
    .sort((left, right) => left.id.localeCompare(right.id));
}

export function languageSupportFromTools(tools: ToolDescriptorDto[]): string[] {
  const languages = new Set<string>();
  for (const tool of tools) {
    for (const capability of tool.capabilities) {
      if (capability.startsWith('runtime.')) {
        const runtimeId = capability.slice('runtime.'.length);
        if (CODE_RUNTIME_IDS.has(runtimeId)) {
          languages.add(runtimeId);
        }
      }
    }
  }

  return [...languages].sort();
}

export function groupedToolCapabilities(tools: ToolDescriptorDto[]): Record<string, ToolDescriptorDto[]> {
  return tools.reduce<Record<string, ToolDescriptorDto[]>>((groups, tool) => {
    const source = tool.source || 'unknown';
    groups[source] = [...(groups[source] ?? []), tool];
    return groups;
  }, {});
}

export function manifestTools(manifest: HarnessManifestDto | null | undefined, fallback: ToolDescriptorDto[] = []): ToolDescriptorDto[] {
  return manifest?.tools?.length ? manifest.tools : fallback;
}

export function sortedToolProviders(manifest: HarnessManifestDto | null | undefined): ToolProviderManifestDto[] {
  return [...(manifest?.tool_providers ?? [])].sort((left, right) => {
    const rank = sourceRank(left.source) - sourceRank(right.source);
    return rank !== 0 ? rank : left.display_name.localeCompare(right.display_name);
  });
}

export function sortedAgentLayers(manifest: HarnessManifestDto | null | undefined): AgentLayerManifestDto[] {
  return [...(manifest?.agent_layers ?? [])].sort((left, right) => {
    const rank = layerRank(left.layer) - layerRank(right.layer);
    return rank !== 0 ? rank : left.layer.localeCompare(right.layer);
  });
}

export function sortedRiskPolicies(manifest: HarnessManifestDto | null | undefined): ToolRiskManifestDto[] {
  return [...(manifest?.tool_risks ?? [])].sort((left, right) => {
    const rank = riskRank(left.risk) - riskRank(right.risk);
    return rank !== 0 ? rank : left.risk.localeCompare(right.risk);
  });
}

export function sortedToolSearchResults(results: ToolSearchResultDto[]): ToolSearchResultDto[] {
  return [...results].sort((left, right) => {
    const score = right.score - left.score;
    if (score !== 0) return score;
    const source = sourceRank(left.tool.source) - sourceRank(right.tool.source);
    if (source !== 0) return source;
    return left.tool.id.localeCompare(right.tool.id);
  });
}

export function projectTemplatesFromResult(result: CodeToolExecuteResultDto | null | undefined): ProjectTemplateSummary[] {
  const templates = result?.data.templates;
  if (!Array.isArray(templates)) {
    return [];
  }

  return templates
    .filter(isProjectTemplateSummary)
    .sort((left, right) => left.id.localeCompare(right.id));
}

function isProjectTemplateSummary(value: unknown): value is ProjectTemplateSummary {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const template = value as Record<string, unknown>;
  return typeof template.id === 'string'
    && typeof template.name === 'string'
    && typeof template.language === 'string'
    && typeof template.package_manager === 'string'
    && (template.description === undefined || typeof template.description === 'string');
}

function sourceRank(source: string): number {
  const ranks: Record<string, number> = {
    core: 0,
    code: 1,
    'codex-rust': 2
  };
  return ranks[source] ?? 10;
}

function layerRank(layer: string): number {
  const ranks: Record<string, number> = {
    planning: 0,
    execution: 1
  };
  return ranks[layer] ?? 10;
}

function riskRank(risk: string): number {
  const ranks: Record<string, number> = {
    'read-only': 0,
    'workspace-write': 1,
    shell: 2,
    'git-write': 3,
    'external-url': 4
  };
  return ranks[risk] ?? 10;
}
