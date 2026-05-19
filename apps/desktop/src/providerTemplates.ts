export type ConnectionKind = 'api-key' | 'cli' | 'local-server'

export type ProviderCategory = 'cloud-api' | 'local-server' | 'agent-cli' | 'custom'

export interface ProviderTemplate {
  driver: string
  display_name_key: string
  summary_key: string
  connection_kind: ConnectionKind
  category: ProviderCategory
  default_base_url: string | null
  default_model: string | null
  capabilities: string[]
  brand_color: string
  brand_bg: string
  fields: {
    base_url: boolean
    model: boolean
    api_key: boolean
    binary_path: boolean
    home_path: boolean
    server_url: boolean
    launch_args: boolean
  }
  placeholders: {
    base_url?: string
    model?: string
    api_key?: string
    binary_path?: string
    home_path?: string
    server_url?: string
    launch_args?: string
  }
}

function hexToRgba(hex: string, alpha: number): string {
  const r = parseInt(hex.slice(1, 3), 16)
  const g = parseInt(hex.slice(3, 5), 16)
  const b = parseInt(hex.slice(5, 7), 16)
  return `rgba(${r},${g},${b},${alpha})`
}

export const PROVIDER_TEMPLATES: ProviderTemplate[] = [
  {
    driver: 'openai-compatible',
    display_name_key: 'providers.openaiCompatible',
    summary_key: 'providers.openaiCompatibleSummary',
    connection_kind: 'api-key',
    category: 'cloud-api',
    default_base_url: 'https://api.openai.com/v1',
    default_model: 'gpt-5.4-mini',
    capabilities: ['chat', 'streaming', 'tool-calls'],
    brand_color: '#10a37f',
    brand_bg: hexToRgba('#10a37f', 0.12),
    fields: { base_url: true, model: true, api_key: true, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'https://api.openai.com/v1', model: 'gpt-5.4-mini', api_key: 'sk-...' }
  },
  {
    driver: 'anthropic',
    display_name_key: 'providers.anthropic',
    summary_key: 'providers.anthropicSummary',
    connection_kind: 'api-key',
    category: 'cloud-api',
    default_base_url: 'https://api.anthropic.com/v1',
    default_model: 'claude-sonnet-4-6',
    capabilities: ['chat', 'streaming', 'reasoning', 'tool-calls'],
    brand_color: '#d97706',
    brand_bg: hexToRgba('#d97706', 0.12),
    fields: { base_url: true, model: true, api_key: true, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'https://api.anthropic.com/v1', model: 'claude-sonnet-4-6 / claude-opus-4', api_key: 'sk-ant-...' }
  },
  {
    driver: 'google',
    display_name_key: 'providers.googleGemini',
    summary_key: 'providers.googleGeminiSummary',
    connection_kind: 'api-key',
    category: 'cloud-api',
    default_base_url: 'https://generativelanguage.googleapis.com/v1beta/openai',
    default_model: 'gemini-2.5-pro',
    capabilities: ['chat', 'streaming', 'reasoning', 'tool-calls'],
    brand_color: '#4285f4',
    brand_bg: hexToRgba('#4285f4', 0.12),
    fields: { base_url: true, model: true, api_key: true, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'https://generativelanguage.googleapis.com/v1beta/openai', model: 'gemini-2.5-pro / gemini-2.5-flash', api_key: 'AIza...' }
  },
  {
    driver: 'deepseek',
    display_name_key: 'providers.deepseek',
    summary_key: 'providers.deepseekSummary',
    connection_kind: 'api-key',
    category: 'cloud-api',
    default_base_url: 'https://api.deepseek.com/v1',
    default_model: 'deepseek-chat',
    capabilities: ['chat', 'streaming', 'reasoning', 'tool-calls'],
    brand_color: '#4d6bfe',
    brand_bg: hexToRgba('#4d6bfe', 0.12),
    fields: { base_url: true, model: true, api_key: true, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'https://api.deepseek.com/v1', model: 'deepseek-chat / deepseek-reasoner', api_key: 'sk-...' }
  },
  {
    driver: 'openrouter',
    display_name_key: 'providers.openrouter',
    summary_key: 'providers.openrouterSummary',
    connection_kind: 'api-key',
    category: 'cloud-api',
    default_base_url: 'https://openrouter.ai/api/v1',
    default_model: 'openai/gpt-5',
    capabilities: ['chat', 'streaming', 'routing', 'tool-calls'],
    brand_color: '#6366f1',
    brand_bg: hexToRgba('#6366f1', 0.12),
    fields: { base_url: true, model: true, api_key: true, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'https://openrouter.ai/api/v1', model: 'openai/gpt-5 / anthropic/claude-sonnet-4', api_key: 'sk-or-...' }
  },
  {
    driver: 'groq',
    display_name_key: 'providers.groq',
    summary_key: 'providers.groqSummary',
    connection_kind: 'api-key',
    category: 'cloud-api',
    default_base_url: 'https://api.groq.com/openai/v1',
    default_model: 'llama-3.3-70b-versatile',
    capabilities: ['chat', 'streaming', 'fast'],
    brand_color: '#f55036',
    brand_bg: hexToRgba('#f55036', 0.12),
    fields: { base_url: true, model: true, api_key: true, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'https://api.groq.com/openai/v1', model: 'llama-3.3-70b-versatile', api_key: 'gsk_...' }
  },
  {
    driver: 'togetherai',
    display_name_key: 'providers.togetherAi',
    summary_key: 'providers.togetherAiSummary',
    connection_kind: 'api-key',
    category: 'cloud-api',
    default_base_url: 'https://api.together.xyz/v1',
    default_model: 'meta-llama/Llama-3.3-70B-Instruct-Turbo',
    capabilities: ['chat', 'streaming', 'tool-calls'],
    brand_color: '#0081f1',
    brand_bg: hexToRgba('#0081f1', 0.12),
    fields: { base_url: true, model: true, api_key: true, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'https://api.together.xyz/v1', model: 'meta-llama/Llama-3.3-70B-Instruct-Turbo', api_key: 'together_...' }
  },
  {
    driver: 'fireworks',
    display_name_key: 'providers.fireworksAi',
    summary_key: 'providers.fireworksAiSummary',
    connection_kind: 'api-key',
    category: 'cloud-api',
    default_base_url: 'https://api.fireworks.ai/inference/v1',
    default_model: 'accounts/fireworks/models/deepseek-v3',
    capabilities: ['chat', 'streaming'],
    brand_color: '#ff6b35',
    brand_bg: hexToRgba('#ff6b35', 0.12),
    fields: { base_url: true, model: true, api_key: true, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'https://api.fireworks.ai/inference/v1', model: 'accounts/fireworks/models/...', api_key: 'fw_...' }
  },
  {
    driver: 'xai',
    display_name_key: 'providers.xai',
    summary_key: 'providers.xaiSummary',
    connection_kind: 'api-key',
    category: 'cloud-api',
    default_base_url: 'https://api.x.ai/v1',
    default_model: 'grok-3',
    capabilities: ['chat', 'streaming', 'tool-calls'],
    brand_color: '#1d9bf0',
    brand_bg: hexToRgba('#1d9bf0', 0.12),
    fields: { base_url: true, model: true, api_key: true, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'https://api.x.ai/v1', model: 'grok-3 / grok-3-mini', api_key: 'xai-...' }
  },
  {
    driver: 'mistral',
    display_name_key: 'providers.mistral',
    summary_key: 'providers.mistralSummary',
    connection_kind: 'api-key',
    category: 'cloud-api',
    default_base_url: 'https://api.mistral.ai/v1',
    default_model: 'mistral-large-latest',
    capabilities: ['chat', 'streaming', 'tool-calls'],
    brand_color: '#ff7000',
    brand_bg: hexToRgba('#ff7000', 0.12),
    fields: { base_url: true, model: true, api_key: true, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'https://api.mistral.ai/v1', model: 'mistral-large-latest / codestral-latest', api_key: 'mistral_...' }
  },
  {
    driver: 'cohere',
    display_name_key: 'providers.cohere',
    summary_key: 'providers.cohereSummary',
    connection_kind: 'api-key',
    category: 'cloud-api',
    default_base_url: 'https://api.cohere.ai/v1',
    default_model: 'command-r-plus',
    capabilities: ['chat', 'streaming', 'tool-calls', 'rag'],
    brand_color: '#39d353',
    brand_bg: hexToRgba('#39d353', 0.12),
    fields: { base_url: true, model: true, api_key: true, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'https://api.cohere.ai/v1', model: 'command-r-plus', api_key: 'co_...' }
  },
  {
    driver: 'qwen',
    display_name_key: 'providers.qwen',
    summary_key: 'providers.qwenSummary',
    connection_kind: 'api-key',
    category: 'cloud-api',
    default_base_url: 'https://dashscope.aliyuncs.com/compatible-mode/v1',
    default_model: 'qwen-max',
    capabilities: ['chat', 'streaming', 'reasoning', 'tool-calls'],
    brand_color: '#6236ff',
    brand_bg: hexToRgba('#6236ff', 0.12),
    fields: { base_url: true, model: true, api_key: true, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'https://dashscope.aliyuncs.com/compatible-mode/v1', model: 'qwen-max / qwen-plus / qwen-turbo', api_key: 'sk-...' }
  },
  {
    driver: 'azure-openai',
    display_name_key: 'providers.azureOpenai',
    summary_key: 'providers.azureOpenaiSummary',
    connection_kind: 'api-key',
    category: 'cloud-api',
    default_base_url: 'https://YOUR_RESOURCE.openai.azure.com/openai/deployments/YOUR_DEPLOYMENT',
    default_model: 'gpt-5.4-mini',
    capabilities: ['chat', 'streaming', 'tool-calls', 'enterprise'],
    brand_color: '#0078d4',
    brand_bg: hexToRgba('#0078d4', 0.12),
    fields: { base_url: true, model: true, api_key: true, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'https://YOUR_RESOURCE.openai.azure.com/...', model: 'gpt-5.4-mini', api_key: 'azure-api-key' }
  },
  {
    driver: 'aws-bedrock',
    display_name_key: 'providers.awsBedrock',
    summary_key: 'providers.awsBedrockSummary',
    connection_kind: 'api-key',
    category: 'cloud-api',
    default_base_url: 'https://bedrock-runtime.us-east-1.amazonaws.com',
    default_model: 'anthropic.claude-sonnet-4-20250514',
    capabilities: ['chat', 'streaming', 'enterprise'],
    brand_color: '#ff9900',
    brand_bg: hexToRgba('#ff9900', 0.12),
    fields: { base_url: true, model: true, api_key: true, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'https://bedrock-runtime.us-east-1.amazonaws.com', model: 'anthropic.claude-sonnet-4-20250514', api_key: 'AWS_ACCESS_KEY_ID' }
  },
  {
    driver: 'github-copilot',
    display_name_key: 'providers.githubCopilot',
    summary_key: 'providers.githubCopilotSummary',
    connection_kind: 'api-key',
    category: 'cloud-api',
    default_base_url: 'https://api.githubcopilot.com',
    default_model: 'gpt-4.1',
    capabilities: ['chat', 'streaming', 'tool-calls'],
    brand_color: '#6e40c9',
    brand_bg: hexToRgba('#6e40c9', 0.12),
    fields: { base_url: true, model: true, api_key: true, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'https://api.githubcopilot.com', model: 'gpt-4.1 / claude-sonnet-4', api_key: 'ghu_...' }
  },
  {
    driver: 'ollama',
    display_name_key: 'providers.ollama',
    summary_key: 'providers.ollamaSummary',
    connection_kind: 'local-server',
    category: 'local-server',
    default_base_url: 'http://localhost:11434/v1',
    default_model: 'llama3.2',
    capabilities: ['chat', 'local', 'no-api-key'],
    brand_color: '#c8a87c',
    brand_bg: hexToRgba('#c8a87c', 0.12),
    fields: { base_url: true, model: true, api_key: false, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'http://localhost:11434/v1', model: 'llama3.2 / qwen2.5 / mistral' }
  },
  {
    driver: 'vllm',
    display_name_key: 'providers.vllm',
    summary_key: 'providers.vllmSummary',
    connection_kind: 'local-server',
    category: 'local-server',
    default_base_url: 'http://localhost:8000/v1',
    default_model: 'default',
    capabilities: ['chat', 'local', 'no-api-key'],
    brand_color: '#7c3aed',
    brand_bg: hexToRgba('#7c3aed', 0.12),
    fields: { base_url: true, model: true, api_key: false, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'http://localhost:8000/v1', model: 'default' }
  },
  {
    driver: 'sglang',
    display_name_key: 'providers.sglang',
    summary_key: 'providers.sglangSummary',
    connection_kind: 'local-server',
    category: 'local-server',
    default_base_url: 'http://localhost:30000/v1',
    default_model: 'default',
    capabilities: ['chat', 'local', 'no-api-key'],
    brand_color: '#f59e0b',
    brand_bg: hexToRgba('#f59e0b', 0.12),
    fields: { base_url: true, model: true, api_key: false, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'http://localhost:30000/v1', model: 'default' }
  },
  {
    driver: 'lmstudio',
    display_name_key: 'providers.lmStudio',
    summary_key: 'providers.lmStudioSummary',
    connection_kind: 'local-server',
    category: 'local-server',
    default_base_url: 'http://localhost:1234/v1',
    default_model: 'default',
    capabilities: ['chat', 'local', 'no-api-key'],
    brand_color: '#00d4aa',
    brand_bg: hexToRgba('#00d4aa', 0.12),
    fields: { base_url: true, model: true, api_key: false, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'http://localhost:1234/v1', model: 'default' }
  },
  {
    driver: 'codex-cli',
    display_name_key: 'providers.codexCli',
    summary_key: 'providers.codexCliSummary',
    connection_kind: 'cli',
    category: 'agent-cli',
    default_base_url: null,
    default_model: 'gpt-5.4',
    capabilities: ['agent', 'cli', 'workspace'],
    brand_color: '#10a37f',
    brand_bg: hexToRgba('#10a37f', 0.12),
    fields: { base_url: false, model: false, api_key: false, binary_path: true, home_path: true, server_url: false, launch_args: false },
    placeholders: { binary_path: 'codex', home_path: '~/.codex-work' }
  },
  {
    driver: 'claude-cli',
    display_name_key: 'providers.claudeCli',
    summary_key: 'providers.claudeCliSummary',
    connection_kind: 'cli',
    category: 'agent-cli',
    default_base_url: null,
    default_model: 'claude-sonnet-4-6',
    capabilities: ['agent', 'cli', 'workspace'],
    brand_color: '#d97706',
    brand_bg: hexToRgba('#d97706', 0.12),
    fields: { base_url: false, model: false, api_key: false, binary_path: true, home_path: true, server_url: false, launch_args: false },
    placeholders: { binary_path: 'claude', home_path: '~/.claude-work' }
  },
  {
    driver: 'cursor-acp',
    display_name_key: 'providers.cursorAcp',
    summary_key: 'providers.cursorAcpSummary',
    connection_kind: 'cli',
    category: 'agent-cli',
    default_base_url: null,
    default_model: 'auto',
    capabilities: ['agent', 'cli', 'acp'],
    brand_color: '#6366f1',
    brand_bg: hexToRgba('#6366f1', 0.12),
    fields: { base_url: false, model: false, api_key: false, binary_path: true, home_path: false, server_url: false, launch_args: true },
    placeholders: { binary_path: 'cursor-agent', launch_args: '--acp-port 0' }
  },
  {
    driver: 'opencode',
    display_name_key: 'providers.opencode',
    summary_key: 'providers.opencodeSummary',
    connection_kind: 'cli',
    category: 'agent-cli',
    default_base_url: 'http://127.0.0.1:4096',
    default_model: 'openai/gpt-5',
    capabilities: ['agent', 'cli', 'server'],
    brand_color: '#22d3ee',
    brand_bg: hexToRgba('#22d3ee', 0.12),
    fields: { base_url: false, model: false, api_key: false, binary_path: true, home_path: false, server_url: true, launch_args: true },
    placeholders: { binary_path: 'opencode', server_url: 'http://127.0.0.1:4096', launch_args: 'serve --port 4096' }
  },
  {
    driver: 'custom',
    display_name_key: 'providers.custom',
    summary_key: 'providers.customSummary',
    connection_kind: 'api-key',
    category: 'custom',
    default_base_url: '',
    default_model: '',
    capabilities: ['chat', 'streaming'],
    brand_color: '#8b949e',
    brand_bg: hexToRgba('#8b949e', 0.10),
    fields: { base_url: true, model: true, api_key: true, binary_path: false, home_path: false, server_url: false, launch_args: false },
    placeholders: { base_url: 'https://your-api-endpoint.com/v1', model: 'model-name', api_key: 'your-api-key' }
  }
]

export function findTemplate(driver: string): ProviderTemplate | undefined {
  return PROVIDER_TEMPLATES.find((t) => t.driver === driver)
}

export const PROVIDER_CATEGORIES: { key: ProviderCategory; labelKey: string }[] = [
  { key: 'cloud-api', labelKey: 'settings.categoryCloudApi' },
  { key: 'local-server', labelKey: 'settings.categoryLocalServer' },
  { key: 'agent-cli', labelKey: 'settings.categoryAgentCli' },
  { key: 'custom', labelKey: 'settings.categoryCustom' }
]
