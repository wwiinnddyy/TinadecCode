<script setup lang="ts">
import { cn } from '@/lib/utils'
import { ChevronRight } from '@lucide/vue'

interface Props {
  class?: string
}

const props = defineProps<Props>()
</script>

<template>
  <nav :class="cn('flex flex-wrap items-center gap-1.5 break-words text-sm text-muted-foreground sm:gap-2.5', props.class)">
    <slot />
  </nav>
</template>
