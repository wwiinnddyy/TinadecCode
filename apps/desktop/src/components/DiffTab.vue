<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
</script>

<template>
  <section class="panel">
    <div class="diff-box">
      <span>{{ t('context.diffPlaceholder') }}</span>
    </div>
  </section>
</template>
