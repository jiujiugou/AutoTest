<template>
  <el-card>
    <template #header>
      <div class="header">
        <span>AI 对话</span>
        <div class="actions">
          <el-button size="small" @click="clear">清空</el-button>
        </div>
      </div>
    </template>

    <div class="form">
      <el-input v-model="system" type="textarea" :rows="2" placeholder="System（可选）" />
      <div class="row">
        <el-input v-model="message" placeholder="输入消息…" @keydown.enter="send" />
        <el-button type="primary" :loading="busy" @click="send">发送</el-button>
      </div>
    </div>

    <div class="chat">
      <div v-for="(m, idx) in messages" :key="idx" class="msg">
        <div class="role">{{ m.role }}</div>
        <div class="text">{{ m.text }}</div>
      </div>
      <div v-if="messages.length === 0" class="muted">暂无消息</div>
    </div>
  </el-card>
</template>

<script setup>
import { ref } from "vue";
import { ElMessage } from "element-plus";
import api from "../api/http";

const system = ref("");
const message = ref("");
const messages = ref([]);
const busy = ref(false);

function clear() {
  messages.value = [];
}

async function send() {
  const msg = message.value.trim();
  if (!msg) return;
  message.value = "";
  messages.value.push({ role: "user", text: msg });
  busy.value = true;
  try {
    const r = await api.post("/api/ai/chat", { message: msg, system: system.value || null });
    messages.value.push({ role: "assistant", text: r?.reply || "" });
  } catch (e) {
    ElMessage.error(e.message || String(e));
    messages.value.push({ role: "error", text: e.message || String(e) });
  } finally {
    busy.value = false;
  }
}
</script>

<style scoped>
.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.actions {
  display: flex;
  gap: 8px;
}
.form {
  display: grid;
  gap: 10px;
}
.row {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 10px;
  align-items: center;
}
.chat {
  margin-top: 12px;
  border: 1px solid #e5e7eb;
  border-radius: 10px;
  padding: 12px;
  min-height: 260px;
  max-height: 520px;
  overflow: auto;
  background: #fff;
}
.msg {
  margin-bottom: 12px;
}
.role {
  font-size: 12px;
  color: #64748b;
}
.text {
  white-space: pre-wrap;
  line-height: 1.45;
}
.muted {
  font-size: 12px;
  color: #94a3b8;
}
</style>

