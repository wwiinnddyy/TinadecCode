<script setup lang="ts">
import {
  ArrowLeft,
  Bot,
  CheckCircle2,
  Circle,
  Cloud,
  Cpu,
  FileText,
  Globe,
  HardDrive,
  Info,
  KeyRound,
  Monitor,
  Moon,
  Palette,
  Plus,
  Save,
  Server,
  Settings2,
  ShieldCheck,
  Sun,
  Terminal,
  Workflow,
  X
} from '@lucide/vue'
import { computed, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useTheme } from '../composables/useTheme'
import {
  api,
  type AgentCandidateDto,
  type AgentModeDto,
  type AgentProfileDto,
  type ModelProviderInstanceDto,
  type ModelRouteDto,
  type SaveModelProviderInstanceInput
} from '../api'
import {
  PROVIDER_TEMPLATES,
  PROVIDER_CATEGORIES,
  findTemplate,
  type ProviderTemplate,
  type ProviderCategory
} from '../providerTemplates'
import { UiButton, UiInput, UiCard, UiBadge, UiLabel, UiSwitch } from '@/components/ui'

type SettingsSection = 'model' | 'agents' | 'appearance' | 'language' | 'apiDocs' | 'about'

interface ProviderForm {
  id: string
  driver: string
  display_name: string
  connection_kind: string
  base_url: string
  model: string
  api_key: string
  clear_api_key: boolean
  binary_path: string
  home_path: string
  server_url: string
  launch_args: string
  enabled: boolean
}

const CATEGORY_ICONS: Record<ProviderCategory, typeof Cloud> = {
  'cloud-api': Cloud,
  'local-server': HardDrive,
  'agent-cli': Terminal,
  'custom': Settings2,
}

const { t, locale } = useI18n()
const router = useRouter()
const { theme, setTheme } = useTheme()

const activeSection = ref<SettingsSection>('model')
const providers = ref<ModelProviderInstanceDto[]>([])
const routes = ref<ModelRouteDto[]>([])
const agentModes = ref<AgentModeDto[]>([])
const agents = ref<AgentProfileDto[]>([])
const agentCandidates = ref<AgentCandidateDto[]>([])
const selectedProviderId = ref('')
const selectedAgentId = ref('')
const busy = ref(false)
const loading = ref(false)
const showModal = ref(false)

const providerForm = reactive<ProviderForm>({
  id: '',
  driver: 'openai-compatible',
  display_name: 'OpenAI Compatible',
  connection_kind: 'api-key',
  base_url: 'https://api.openai.com/v1',
  model: 'gpt-5.4-mini',
  api_key: '',
  clear_api_key: false,
  binary_path: '',
  home_path: '',
  server_url: '',
  launch_args: '',
  enabled: true
})

const navItems = computed(() => [
  { key: 'model' as const, icon: KeyRound, label: t('settings.model') },
  { key: 'agents' as const, icon: Workflow, label: t('settings.agents') },
  { key: 'appearance' as const, icon: Palette, label: t('settings.appearance') },
  { key: 'language' as const, icon: Globe, label: t('settings.language') },
  { key: 'apiDocs' as const, icon: FileText, label: t('settings.apiDocs') },
  { key: 'about' as const, icon: Info, label: t('settings.about') },
])

const currentTemplate = computed(() => findTemplate(providerForm.driver))

const chatRoute = computed(() =>
  routes.value.find((route) => route.purpose === 'chat') ?? null
)
const chatProvider = computed(() =>
  providers.value.find((provider) => provider.id === chatRoute.value?.provider_instance_id) ?? null
)

const formFields = computed(() => currentTemplate.value?.fields ?? {
  base_url: true, model: true, api_key: true,
  binary_path: false, home_path: false, server_url: false, launch_args: false
})
const formPlaceholders = computed(() => currentTemplate.value?.placeholders ?? {})

const addedDriverSet = computed(() => new Set(providers.value.map((p) => p.driver)))

const templatesByCategory = computed(() => {
  const map = new Map<ProviderCategory, ProviderTemplate[]>()
  for (const cat of PROVIDER_CATEGORIES) {
    map.set(cat.key, PROVIDER_TEMPLATES.filter((t) => t.category === cat.key))
  }
  return map
})

const selectedProvider = computed(() =>
  providers.value.find((provider) => provider.id === selectedProviderId.value) ?? null
)
const selectedAgent = computed(() =>
  agents.value.find((agent) => agent.id === selectedAgentId.value) ?? agents.value[0] ?? null
)
const planningAgents = computed(() => agents.value.filter((agent) => agent.layer === 'planning'))
const executionAgents = computed(() => agents.value.filter((agent) => agent.layer === 'execution'))
const selectedAgentMode = computed(() => agentModes.value.find((mode) => mode.id === selectedAgent.value?.mode) ?? null)

function brandColor(driver: string) {
  return findTemplate(driver)?.brand_color ?? '#58a6ff'
}

function brandBg(driver: string) {
  return findTemplate(driver)?.brand_bg ?? 'rgba(88,166,255,0.12)'
}

function setLocale(lang: string) {
  locale.value = lang
  localStorage.setItem('tinadec-locale', lang)
}

function fillForm(provider: ModelProviderInstanceDto) {
  providerForm.id = provider.id
  providerForm.driver = provider.driver
  providerForm.display_name = provider.display_name
  providerForm.connection_kind = provider.connection_kind
  providerForm.base_url = provider.base_url ?? ''
  providerForm.model = provider.model ?? ''
  providerForm.api_key = ''
  providerForm.clear_api_key = false
  providerForm.binary_path = provider.binary_path ?? ''
  providerForm.home_path = provider.home_path ?? ''
  providerForm.server_url = provider.server_url ?? ''
  providerForm.launch_args = provider.launch_args ?? ''
  providerForm.enabled = provider.enabled
}

function applyTemplateDefaults(template: ProviderTemplate) {
  providerForm.driver = template.driver
  providerForm.display_name = template.display_name
  providerForm.connection_kind = template.connection_kind
  providerForm.base_url = template.default_base_url ?? ''
  providerForm.model = template.default_model ?? ''
  providerForm.binary_path = ''
  providerForm.home_path = ''
  providerForm.server_url = template.driver === 'opencode' ? template.default_base_url ?? '' : ''
  providerForm.launch_args = ''
}

function openAddModal(template?: ProviderTemplate) {
  selectedProviderId.value = ''
  providerForm.id = ''
  if (template) {
    applyTemplateDefaults(template)
  } else {
    applyTemplateDefaults(PROVIDER_TEMPLATES[0])
  }
  providerForm.api_key = ''
  providerForm.clear_api_key = false
  providerForm.enabled = true
  showModal.value = true
}

function openEditModal(provider: ModelProviderInstanceDto) {
  selectedProviderId.value = provider.id
  fillForm(provider)
  showModal.value = true
}

function closeModal() {
  showModal.value = false
}

async function loadModelCenter() {
  loading.value = true
  try {
    const [instances, modelRoutes] = await Promise.all([
      api.listModelProviders(),
      api.listModelRoutes()
    ])
    providers.value = instances
    routes.value = modelRoutes

    const selected = instances.find((provider) => provider.id === selectedProviderId.value) ?? instances[0]
    if (selected) {
      selectedProviderId.value = selected.id
    }
  } finally {
    loading.value = false
  }
}

async function loadAgentCenter() {
  loading.value = true
  try {
    const [modes, agentList, candidates] = await Promise.all([
      api.listAgentModes(),
      api.listAgents(),
      api.listAgentCandidates()
    ])
    agentModes.value = modes
    agents.value = agentList
    agentCandidates.value = candidates
    if (!agentList.some((agent) => agent.id === selectedAgentId.value)) {
      selectedAgentId.value = agentList[0]?.id ?? ''
    }
  } finally {
    loading.value = false
  }
}

async function updateAgentMode(agent: AgentProfileDto, mode: string) {
  busy.value = true
  try {
    await api.updateAgentMode(agent.id, mode)
    await loadAgentCenter()
  } finally {
    busy.value = false
  }
}

async function toggleAgent(agent: AgentProfileDto) {
  busy.value = true
  try {
    await api.saveAgent(agent.id, {
      name: agent.name,
      layer: agent.layer,
      agent_type: agent.agent_type,
      mode: agent.mode,
      description: agent.description,
      model_route_purpose: agent.model_route_purpose,
      allowed_tools: agent.allowed_tools,
      capabilities: agent.capabilities,
      enabled: !agent.enabled
    })
    await loadAgentCenter()
  } finally {
    busy.value = false
  }
}

async function saveProvider(makeChatDefault = false) {
  busy.value = true
  try {
    const tmpl = currentTemplate.value
    const payload: SaveModelProviderInstanceInput = {
      id: providerForm.id || undefined,
      driver: providerForm.driver,
      display_name: providerForm.display_name,
      connection_kind: providerForm.connection_kind,
      base_url: formFields.value.base_url ? (providerForm.base_url || null) : null,
      model: formFields.value.model ? (providerForm.model || null) : null,
      api_key: formFields.value.api_key ? (providerForm.api_key || null) : null,
      clear_api_key: providerForm.clear_api_key,
      binary_path: formFields.value.binary_path ? (providerForm.binary_path || null) : null,
      home_path: formFields.value.home_path ? (providerForm.home_path || null) : null,
      server_url: formFields.value.server_url ? (providerForm.server_url || null) : null,
      launch_args: formFields.value.launch_args ? (providerForm.launch_args || null) : null,
      capabilities: tmpl?.capabilities ?? [],
      enabled: providerForm.enabled
    }

    const saved = providerForm.id
      ? await api.saveModelProvider(providerForm.id, payload)
      : await api.createModelProvider(payload)

    if (makeChatDefault) {
      await api.saveModelRoute('chat', saved.id, saved.model ?? providerForm.model)
    }

    selectedProviderId.value = saved.id
    showModal.value = false
    await loadModelCenter()
  } finally {
    busy.value = false
  }
}

function connectionKindLabel(kind: string) {
  if (kind === 'cli') return t('settings.connectionKindCli')
  if (kind === 'local-server') return t('settings.connectionKindLocal')
  return t('settings.connectionKindApiKey')
}

function statusLabel(status: string) {
  if (status === 'ready') return t('settings.statusReady')
  if (status === 'needs_key') return t('settings.statusNeedsKey')
  if (status === 'disabled') return t('settings.statusDisabled')
  return t('settings.statusNotConfigured')
}

function statusVariant(status: string): 'default' | 'secondary' | 'destructive' | 'outline' {
  if (status === 'ready') return 'default'
  if (status === 'needs_key' || status === 'not_configured') return 'destructive'
  if (status === 'disabled') return 'secondary'
  return 'outline'
}

loadModelCenter()
loadAgentCenter()
</script>

<template>
  <div>
    <div class="settings-back">
      <UiButton variant="ghost" size="icon" :title="t('settings.back')" @click="router.push('/')">
        <ArrowLeft :size="16" />
      </UiButton>
      <span>{{ t('settings.title') }}</span>
    </div>
    <div class="settings-shell">
      <nav class="settings-nav">
        <UiButton
          v-for="item in navItems"
          :key="item.key"
          variant="ghost"
          size="sm"
          class="settings-nav-item w-full justify-start"
          :class="{ active: activeSection === item.key }"
          @click="activeSection = item.key"
        >
          <component :is="item.icon" :size="16" />
          {{ item.label }}
        </UiButton>
      </nav>

      <div class="settings-content">
        <template v-if="activeSection === 'model'">
          <div class="model-center-heading">
            <div>
              <h2>{{ t('settings.modelCenter') }}</h2>
              <p>{{ t('settings.modelCenterSubtitle') }}</p>
            </div>
            <UiButton variant="outline" size="sm" :disabled="loading" @click="loadModelCenter">
              <Server :size="14" />
              <span>{{ t('settings.refresh') }}</span>
            </UiButton>
          </div>

          <UiCard class="model-route-panel">
            <div class="model-route-icon">
              <Bot :size="18" />
            </div>
            <div class="model-route-main">
              <span class="model-route-label">{{ t('settings.chatRoute') }}</span>
              <strong>{{ chatProvider?.display_name ?? t('settings.noProvider') }}</strong>
              <small>{{ chatRoute?.model ?? chatProvider?.model ?? t('settings.noModel') }}</small>
            </div>
            <UiButton size="sm" :disabled="busy || !selectedProviderId" @click="saveProvider(true)">
              <CheckCircle2 :size="14" />
              <span>{{ t('settings.setChatDefault') }}</span>
            </UiButton>
          </UiCard>

          <div class="model-section-header">
            <h3>{{ t('settings.addedProviders') }}</h3>
            <UiButton variant="ghost" size="icon" :title="t('settings.newProvider')" @click="openAddModal()">
              <Plus :size="15" />
            </UiButton>
          </div>

          <div v-if="providers.length > 0" class="model-provider-grid">
            <button
              v-for="provider in providers"
              :key="provider.id"
              class="model-provider-card"
              :style="{ background: brandBg(provider.driver), borderColor: brandColor(provider.driver) + '40' }"
              @click="openEditModal(provider)"
            >
              <div class="model-provider-card-main">
                <span class="model-provider-name">{{ provider.display_name }}</span>
                <span class="model-provider-meta">{{ connectionKindLabel(provider.connection_kind) }} · {{ provider.model || provider.driver }}</span>
              </div>
              <UiBadge :variant="statusVariant(provider.status)">
                <Circle :size="8" />
                {{ statusLabel(provider.status) }}
              </UiBadge>
            </button>
          </div>
          <p v-else class="quiet">{{ t('settings.noProvider') }}</p>

          <template v-for="cat in PROVIDER_CATEGORIES" :key="cat.key">
            <div class="model-section-header">
              <h3>
                <component :is="CATEGORY_ICONS[cat.key]" :size="14" style="vertical-align:-2px" />
                {{ t(cat.labelKey) }}
              </h3>
            </div>
            <div class="model-provider-grid">
              <button
                v-for="template in templatesByCategory.get(cat.key)"
                :key="template.driver"
                class="model-provider-card add"
                :class="{ 'already-added': addedDriverSet.has(template.driver) }"
                :style="{ background: template.brand_bg, borderColor: template.brand_color + '40' }"
                @click="openAddModal(template)"
              >
                <div class="add-label">
                  <Plus v-if="!addedDriverSet.has(template.driver)" :size="16" />
                  <CheckCircle2 v-else :size="16" style="color:var(--accent-success)" />
                  <span>{{ template.display_name }}</span>
                  <small>{{ template.summary }}</small>
                </div>
              </button>
            </div>
          </template>
        </template>

        <template v-if="activeSection === 'agents'">
          <div class="model-center-heading">
            <div>
              <h2>{{ t('settings.agentCenter') }}</h2>
              <p>{{ t('settings.agentCenterSubtitle') }}</p>
            </div>
            <UiButton variant="outline" size="sm" :disabled="loading" @click="loadAgentCenter">
              <Server :size="14" />
              <span>{{ t('settings.refresh') }}</span>
            </UiButton>
          </div>

          <div class="agent-center-layout">
            <section class="agent-column">
              <div class="model-section-header">
                <h3>{{ t('settings.planningLayer') }}</h3>
                <UiBadge variant="secondary">{{ planningAgents.length }}</UiBadge>
              </div>
              <button
                v-for="agent in planningAgents"
                :key="agent.id"
                class="agent-card"
                :class="{ active: selectedAgentId === agent.id, disabled: !agent.enabled }"
                @click="selectedAgentId = agent.id"
              >
                <div class="agent-card-icon">
                  <Workflow :size="17" />
                </div>
                <div class="agent-card-main">
                  <strong>{{ agent.name }}</strong>
                  <span>{{ agent.agent_type }} · {{ agent.mode }}</span>
                </div>
                <UiBadge :variant="agent.enabled ? 'default' : 'secondary'">
                  {{ agent.enabled ? t('settings.enabled') : t('settings.statusDisabled') }}
                </UiBadge>
              </button>
            </section>

            <section class="agent-column">
              <div class="model-section-header">
                <h3>{{ t('settings.executionLayer') }}</h3>
                <UiBadge variant="secondary">{{ executionAgents.length }}</UiBadge>
              </div>
              <button
                v-for="agent in executionAgents"
                :key="agent.id"
                class="agent-card"
                :class="{ active: selectedAgentId === agent.id, disabled: !agent.enabled }"
                @click="selectedAgentId = agent.id"
              >
                <div class="agent-card-icon execution">
                  <Cpu :size="17" />
                </div>
                <div class="agent-card-main">
                  <strong>{{ agent.name }}</strong>
                  <span>{{ agent.agent_type }} · {{ agent.mode }}</span>
                </div>
                <UiBadge :variant="agent.enabled ? 'default' : 'secondary'">
                  {{ agent.enabled ? t('settings.enabled') : t('settings.statusDisabled') }}
                </UiBadge>
              </button>
            </section>
          </div>

          <UiCard v-if="selectedAgent" class="agent-detail-panel">
            <template #content>
              <div class="agent-detail-head">
                <div class="agent-card-icon" :class="{ execution: selectedAgent.layer === 'execution' }">
                  <component :is="selectedAgent.layer === 'planning' ? Workflow : Cpu" :size="20" />
                </div>
                <div>
                  <h3>{{ selectedAgent.name }}</h3>
                  <p>{{ selectedAgent.description }}</p>
                </div>
                <UiButton variant="secondary" size="sm" :disabled="busy" @click="toggleAgent(selectedAgent)">
                  {{ selectedAgent.enabled ? t('settings.disableAgent') : t('settings.enableAgent') }}
                </UiButton>
              </div>

              <div class="agent-mode-grid">
                <button
                  v-for="mode in agentModes"
                  :key="mode.id"
                  class="agent-mode-card"
                  :class="{ active: selectedAgent.mode === mode.id }"
                  @click="updateAgentMode(selectedAgent, mode.id)"
                >
                  <strong>{{ mode.display_name }}</strong>
                  <span>{{ mode.summary }}</span>
                  <small>
                    {{ t('settings.parallelExecutors') }} {{ mode.max_parallel_executors }}
                    · {{ mode.worktree_isolation ? t('settings.worktreeOn') : t('settings.worktreeOff') }}
                  </small>
                </button>
              </div>

              <div v-if="selectedAgentMode" class="agent-policy-strip">
                <ShieldCheck :size="16" />
                <span>
                  {{ selectedAgentMode.approval_required ? t('settings.approvalGateOn') : t('settings.approvalGateOff') }}
                  · {{ selectedAgentMode.budget_policy }}
                </span>
              </div>

              <div class="agent-detail-grid">
                <div>
                  <span>{{ t('settings.modelRoute') }}</span>
                  <strong>{{ selectedAgent.model_route_purpose }}</strong>
                </div>
                <div>
                  <span>{{ t('settings.agentLayer') }}</span>
                  <strong>{{ selectedAgent.layer }}</strong>
                </div>
              </div>

              <div class="model-capability-row">
                <span v-for="tool in selectedAgent.allowed_tools" :key="tool">{{ tool }}</span>
              </div>
              <div class="model-capability-row">
                <span v-for="capability in selectedAgent.capabilities" :key="capability">{{ capability }}</span>
              </div>
            </template>
          </UiCard>

          <div class="model-section-header">
            <h3>{{ t('settings.purifierCandidates') }}</h3>
            <UiBadge variant="outline">{{ agentCandidates.length }}</UiBadge>
          </div>

          <div class="agent-candidate-grid">
            <UiCard v-for="candidate in agentCandidates" :key="candidate.id" class="agent-candidate-card">
              <template #content>
                <div class="agent-candidate-head">
                  <div>
                    <strong>{{ candidate.name }}</strong>
                    <span>{{ candidate.layer }} · {{ candidate.agent_type }} · {{ candidate.status }}</span>
                  </div>
                  <UiBadge variant="secondary">{{ t('settings.generatedByPurifier') }}</UiBadge>
                </div>
                <p>{{ candidate.description }}</p>
                <div class="model-capability-row">
                  <span v-for="tool in candidate.suggested_tools" :key="tool">{{ tool }}</span>
                </div>
                <ul class="agent-eval-list">
                  <li v-for="note in candidate.evaluation_notes" :key="note">{{ note }}</li>
                </ul>
              </template>
            </UiCard>
          </div>
        </template>

        <template v-if="activeSection === 'appearance'">
          <h2>{{ t('settings.appearance') }}</h2>
          <h3>{{ t('settings.theme') }}</h3>
          <div class="theme-options">
            <UiButton
              variant="outline"
              :class="['theme-option', { active: theme === 'dark' }]"
              @click="setTheme('dark')"
            >
              <Moon :size="20" />
              {{ t('settings.dark') }}
            </UiButton>
            <UiButton
              variant="outline"
              :class="['theme-option', { active: theme === 'light' }]"
              @click="setTheme('light')"
            >
              <Sun :size="20" />
              {{ t('settings.light') }}
            </UiButton>
            <UiButton
              variant="outline"
              :class="['theme-option', { active: theme === 'system' }]"
              @click="setTheme('system')"
            >
              <Monitor :size="20" />
              {{ t('settings.system') }}
            </UiButton>
          </div>
        </template>

        <template v-if="activeSection === 'language'">
          <h2>{{ t('settings.language') }}</h2>
          <div class="lang-options">
            <UiButton
              variant="outline"
              :class="['lang-option', { active: locale === 'zh-CN' }]"
              @click="setLocale('zh-CN')"
            >
              中文
            </UiButton>
            <UiButton
              variant="outline"
              :class="['lang-option', { active: locale === 'en' }]"
              @click="setLocale('en')"
            >
              English
            </UiButton>
          </div>
        </template>

        <template v-if="activeSection === 'apiDocs'">
          <h2>{{ t('settings.apiDocs') }}</h2>
          <iframe class="api-docs-frame" :src="api.gatewayUrl + '/docs'" />
        </template>

        <template v-if="activeSection === 'about'">
          <h2>{{ t('settings.about') }}</h2>
          <UiCard class="about-section">
            <div class="about-row">
              <span>{{ t('app.name') }}</span>
              <span>TinadecCode</span>
            </div>
            <div class="about-row">
              <span>{{ t('settings.version') }}</span>
              <span>0.1.0</span>
            </div>
          </UiCard>
        </template>
      </div>
    </div>

    <div v-if="showModal" class="model-provider-modal" @click.self="closeModal">
      <UiCard class="model-provider-modal-content">
        <template #header>
          <div class="model-section-header">
            <h3>
              <span class="modal-brand-dot" :style="{ background: brandColor(providerForm.driver) }"></span>
              {{ providerForm.id ? t('settings.editProviderTitle') : t('settings.newProvider') }}
              — {{ currentTemplate?.display_name ?? providerForm.driver }}
            </h3>
            <UiButton variant="ghost" size="icon" @click="closeModal">
              <X :size="16" />
            </UiButton>
          </div>
        </template>

        <template #content>
          <p v-if="currentTemplate" class="template-summary">{{ currentTemplate.summary }}</p>

          <div class="settings-field" style="margin-top:12px">
            <UiLabel>{{ t('settings.displayName') }}</UiLabel>
            <UiInput v-model="providerForm.display_name" />
          </div>

          <div class="model-form-grid" style="margin-top:12px">
            <div v-if="formFields.base_url" class="settings-field">
              <UiLabel>{{ t('settings.baseUrl') }}</UiLabel>
              <UiInput v-model="providerForm.base_url" :placeholder="formPlaceholders.base_url" />
            </div>
            <div v-if="formFields.model" class="settings-field">
              <UiLabel>{{ t('settings.modelLabel') }}</UiLabel>
              <UiInput v-model="providerForm.model" :placeholder="formPlaceholders.model" />
            </div>
          </div>

          <div v-if="formFields.api_key" class="settings-field" style="margin-top:12px">
            <UiLabel>{{ t('settings.apiKey') }}</UiLabel>
            <UiInput
              v-model="providerForm.api_key"
              type="password"
              :placeholder="selectedProvider?.has_api_key ? t('settings.apiKeyStored') : formPlaceholders.api_key ?? t('settings.apiKeyNotSet')"
            />
          </div>

          <div v-if="formFields.binary_path || formFields.home_path" class="model-form-grid" style="margin-top:12px">
            <div v-if="formFields.binary_path" class="settings-field">
              <UiLabel>{{ t('settings.binaryPath') }}</UiLabel>
              <UiInput v-model="providerForm.binary_path" :placeholder="formPlaceholders.binary_path" />
            </div>
            <div v-if="formFields.home_path" class="settings-field">
              <UiLabel>{{ t('settings.homePath') }}</UiLabel>
              <UiInput v-model="providerForm.home_path" :placeholder="formPlaceholders.home_path" />
            </div>
          </div>

          <div v-if="formFields.server_url || formFields.launch_args" class="model-form-grid" style="margin-top:12px">
            <div v-if="formFields.server_url" class="settings-field">
              <UiLabel>{{ t('settings.serverUrl') }}</UiLabel>
              <UiInput v-model="providerForm.server_url" :placeholder="formPlaceholders.server_url" />
            </div>
            <div v-if="formFields.launch_args" class="settings-field">
              <UiLabel>{{ t('settings.launchArgs') }}</UiLabel>
              <UiInput v-model="providerForm.launch_args" :placeholder="formPlaceholders.launch_args" />
            </div>
          </div>

          <div class="flex items-center gap-2" style="margin-top:12px">
            <UiSwitch v-model="providerForm.enabled" />
            <UiLabel>{{ t('settings.enabled') }}</UiLabel>
          </div>

          <div v-if="currentTemplate" class="model-capability-row">
            <span v-for="capability in currentTemplate.capabilities" :key="capability">{{ capability }}</span>
          </div>

          <div v-if="selectedProvider" class="model-provider-note">
            <Terminal :size="14" />
            <span>{{ selectedProvider.status_message }}</span>
          </div>
        </template>

        <template #footer>
          <div class="modal-actions">
            <UiButton variant="outline" @click="closeModal">
              {{ t('settings.cancel') }}
            </UiButton>
            <UiButton :disabled="busy" @click="saveProvider(false)">
              <Save :size="14" />
              <span>{{ t('settings.save') }}</span>
            </UiButton>
          </div>
        </template>
      </UiCard>
    </div>
  </div>
</template>
