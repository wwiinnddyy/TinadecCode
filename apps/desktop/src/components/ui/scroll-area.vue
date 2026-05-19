<script setup lang="ts">
import { cn } from '@/lib/utils'

interface Props {
  class?: string
}

const props = defineProps<Props>()
</script>

<template>
  <div :class="cn('relative overflow-hidden', props.class)">
    <div class="h-full w-full overflow-auto rounded-[inherit]">
      <slot />
    </div>
  </div>
</template>
