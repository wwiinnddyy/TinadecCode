import { useStorage } from '@vueuse/core'
import { watch } from 'vue'

export type Theme = 'dark' | 'light' | 'system'

const stored = useStorage<Theme>('tinadec-theme', 'dark')

function applyTheme(theme: Theme) {
  let resolved: 'dark' | 'light'
  if (theme === 'system') {
    resolved = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
  } else {
    resolved = theme
  }
  document.documentElement.setAttribute('data-theme', resolved)
}

export function useTheme() {
  applyTheme(stored.value)

  watch(stored, (val) => {
    applyTheme(val)
  })

  window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
    if (stored.value === 'system') {
      applyTheme('system')
    }
  })

  return {
    theme: stored,
    setTheme: (t: Theme) => {
      stored.value = t
    },
  }
}
