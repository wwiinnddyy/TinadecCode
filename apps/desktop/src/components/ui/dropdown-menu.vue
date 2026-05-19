<script setup lang="ts">
import { cn } from '@/lib/utils'
import { ref, watch } from 'vue'

interface Props {
  open?: boolean
  class?: string
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:open': [value: boolean]
}>()

const isOpen = ref(props.open)

watch(() => props.open, (val) => {
  isOpen.value = val
})

function close() {
  isOpen.value = false
  emit('update:open', false)
}

function open() {
  isOpen.value = true
  emit('update:open', true)
}
</script>

<template>
  <div class="relative">
    <div @click="isOpen ? close() : open()">
      <slot name="trigger" />
    </div>
    <div
      v-if="isOpen"
      :class="cn(
        'absolute z-50 min-w-[8rem] overflow-hidden rounded-md border bg-popover p-1 text-popover-foreground shadow-md',
        props.class,
      )"
    >
      <slot />
    </div>
  </div>
</template>
