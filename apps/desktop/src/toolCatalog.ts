import type { CodeToolExecuteResultDto, ToolDescriptorDto } from './api';

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
