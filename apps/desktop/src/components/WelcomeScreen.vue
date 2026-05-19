<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'
import {
  ArrowUp,
  ChevronDown,
  FolderOpen,
  FolderPlus,
  Image,
  List,
  MessageCircle,
  Plus,
  SquareTerminal,
  FileText,
  Map,
  ChevronRight,
} from '@lucide/vue'
import { useI18n } from 'vue-i18n'
import type { ProjectDto } from '../api'
import { UiButton, UiDropdownMenu, UiScrollArea } from '@/components/ui'

const { t } = useI18n()

const props = defineProps<{
  projects: ProjectDto[]
  selectedProjectId: string | null
  modelName: string
  busy: boolean
}>()

const emit = defineEmits<{
  'send': [content: string]
  'create-project': []
  'select-project': [id: string]
  'add-image': []
  'add-file': []
  'plan-mode': []
}>()

const draft = ref('')
const showPlusMenu = ref(false)
const showProjectDropdown = ref(false)
const isChatMode = ref(false)
const textareaRef = ref<HTMLTextAreaElement | null>(null)
const projectTriggerRef = ref<HTMLElement | null>(null)
const dropdownStyle = ref<Record<string, string>>({})

const selectedProject = computed(() =>
  props.projects.find((p) => p.id === props.selectedProjectId) ?? null
)

const titleText = computed(() =>
  isChatMode.value ? t('chat.chatWithTinadec') : t('chat.startProject')
)

function handleSend() {
  const content = draft.value.trim()
  if (!content) return
  draft.value = ''
  resetTextareaHeight()
  emit('send', content)
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault()
    handleSend()
  }
}

function autoResize() {
  const el = textareaRef.value
  if (!el) return
  el.style.height = 'auto'
  el.style.height = Math.min(el.scrollHeight, 200) + 'px'
}

function resetTextareaHeight() {
  const el = textareaRef.value
  if (!el) return
  el.style.height = 'auto'
}

function toggleChatMode() {
  isChatMode.value = !isChatMode.value
}

function updateDropdownPosition() {
  const trigger = projectTriggerRef.value
  if (!trigger) return
  const rect = trigger.getBoundingClientRect()
  dropdownStyle.value = {
    position: 'fixed',
    top: `${rect.bottom + 6}px`,
    left: `${rect.left}px`,
    minWidth: `${Math.max(rect.width, 220)}px`,
  }
}

async function toggleProjectDropdown() {
  showProjectDropdown.value = !showProjectDropdown.value
  if (showProjectDropdown.value) {
    await nextTick()
    updateDropdownPosition()
  }
}

function selectProject(id: string) {
  emit('select-project', id)
  showProjectDropdown.value = false
}

function openNewProject() {
  emit('create-project')
  showProjectDropdown.value = false
}

function handleClickOutside(event: MouseEvent) {
  const target = event.target as HTMLElement
  if (!target.closest('.project-dropdown-trigger') && !target.closest('.project-dropdown-portal')) {
    showProjectDropdown.value = false
  }
  if (!target.closest('.welcome-dialog-plus-wrapper')) {
    showPlusMenu.value = false
  }
}

onMounted(() => document.addEventListener('click', handleClickOutside))
onUnmounted(() => document.removeEventListener('click', handleClickOutside))
</script>

<template>
  <div class="welcome-screen">
    <div class="welcome-content">
      <div class="welcome-title-row">
        <h1 class="welcome-title">{{ titleText }}</h1>
        <UiButton
          variant="ghost"
          size="icon"
          class="welcome-title-action"
          :title="isChatMode ? t('chat.terminal') : t('chat.chatMode')"
          @click="toggleChatMode"
        >
          <MessageCircle v-if="isChatMode" :size="18" />
          <SquareTerminal v-else :size="18" />
        </UiButton>
      </div>

      <div class="welcome-dialog">
        <div class="welcome-dialog-main">
          <div class="welcome-dialog-plus-wrapper">
            <UiDropdownMenu v-model:open="showPlusMenu" class="plus-dropdown-menu">
              <template #trigger>
                <UiButton variant="ghost" size="icon" class="welcome-dialog-plus">
                  <Plus :size="18" />
                </UiButton>
              </template>
              <button class="plus-menu-item" @click="emit('add-image'); showPlusMenu = false">
                <Image :size="14" />
                <span>{{ t('chat.addImage') }}</span>
              </button>
              <button class="plus-menu-item" @click="emit('add-file'); showPlusMenu = false">
                <FileText :size="14" />
                <span>{{ t('chat.addFile') }}</span>
              </button>
              <button class="plus-menu-item" @click="emit('plan-mode'); showPlusMenu = false">
                <Map :size="14" />
                <span>{{ t('chat.planMode') }}</span>
              </button>
            </UiDropdownMenu>
          </div>

          <textarea
            ref="textareaRef"
            v-model="draft"
            class="welcome-dialog-input"
            :placeholder="t('chat.whatToDo')"
            rows="1"
            @keydown="handleKeydown"
            @input="autoResize"
          />

          <UiButton
            variant="ghost"
            size="icon"
            class="welcome-dialog-send"
            :disabled="!draft.trim()"
            @click="handleSend"
          >
            <ArrowUp :size="18" />
          </UiButton>
        </div>

        <div class="welcome-dialog-toolbar">
          <div class="toolbar-left">
            <button
              ref="projectTriggerRef"
              class="project-dropdown-trigger"
              @click="toggleProjectDropdown"
            >
              <FolderOpen :size="14" />
              <span class="project-dropdown-label">
                {{ selectedProject ? selectedProject.name : t('chat.selectProject') }}
              </span>
              <ChevronDown :size="12" class="project-dropdown-chevron" />
            </button>
            <UiButton variant="ghost" size="sm" class="toolbar-plan">
              <List :size="14" />
              <span>{{ t('chat.plan') }}</span>
            </UiButton>
          </div>
          <div class="toolbar-right">
            <UiButton variant="ghost" size="sm" class="toolbar-model">
              <span>{{ t('chat.modelSelect') }}</span>
              <ChevronRight :size="14" />
            </UiButton>
          </div>
        </div>
      </div>
    </div>

    <Teleport to="body">
      <div
        v-if="showProjectDropdown"
        class="project-dropdown-portal"
        :style="dropdownStyle"
      >
        <UiScrollArea v-if="projects.length > 0" class="project-dropdown-scroll">
          <div class="project-dropdown-section">
            <div class="project-dropdown-section-title">{{ t('chat.openedProjects') }}</div>
            <button
              v-for="project in projects"
              :key="project.id"
              class="project-dropdown-item"
              :class="{ active: project.id === selectedProjectId }"
              @click="selectProject(project.id)"
            >
              <FolderOpen :size="14" />
              <span>{{ project.name }}</span>
            </button>
          </div>
        </UiScrollArea>
        <div class="project-dropdown-divider" />
        <button class="project-dropdown-item project-dropdown-new" @click="openNewProject">
          <FolderPlus :size="14" />
          <span>{{ t('chat.openNewProject') }}</span>
        </button>
      </div>
    </Teleport>
  </div>
</template>
