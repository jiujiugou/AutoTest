<template>
  <div class="page" data-testid="ai-page">
    <AiChat />
  </div>
</template>

<script setup>
import AiChat from '../components/AiChat.vue';
</script>

<style scoped>
.page {
  max-width: 980px;
  margin: 0 auto;
}
</style>
