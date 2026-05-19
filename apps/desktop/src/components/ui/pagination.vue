<script setup lang="ts">
import { cn } from '@/lib/utils'

interface Props {
  class?: string
}

const props = defineProps<Props>()
</script>

<template>
  <nav :class="cn('mx-auto flex w-full justify-center', props.class)">
    <ul class="flex flex-row items-center gap-1">
      <slot />
    </ul>
  </nav>
</template>
