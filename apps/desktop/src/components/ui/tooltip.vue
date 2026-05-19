<script setup lang="ts">
import { cn } from '@/lib/utils'
import { ref } from 'vue'

interface Props {
  content?: string
  class?: string
}

const props = defineProps<Props>()

const show = ref(false)
</script>

<template>
  <div
    class="relative inline-block"
    @mouseenter="show = true"
    @mouseleave="show = false"
  >
    <slot />
    <div
      v-if="show && content"
      :class="cn(
        'absolute z-50 rounded-md bg-primary px-3 py-1.5 text-xs text-primary-foreground animate-in fade-in-0 zoom-in-95',
        props.class,
      )"
    >
      {{ content }}
    </div>
  </div>
</template>
