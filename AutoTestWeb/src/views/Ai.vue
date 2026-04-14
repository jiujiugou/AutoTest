<template>
  <div class="ai-page">
    <el-card shadow="never" class="chat-card">
      <template #header>
        <div class="card-header">
          <span class="title">
            <el-icon><ChatDotRound /></el-icon> AI 运维助手 (Agent)
          </span>
          <el-tag size="small" type="success" effect="dark">基于 Semantic Kernel + 豆包 API</el-tag>
        </div>
      </template>

      <div class="chat-container" ref="chatContainer">
        <div 
          v-for="(msg, index) in messages" 
          :key="index" 
          class="message-row"
          :class="msg.role === 'user' ? 'is-user' : 'is-ai'"
        >
          <el-avatar v-if="msg.role === 'ai'" :size="36" class="avatar-ai">AI</el-avatar>
          
          <div class="message-bubble">
            <div class="message-content" v-html="formatText(msg.content)"></div>
          </div>
          
          <el-avatar v-if="msg.role === 'user'" :size="36" class="avatar-user">Me</el-avatar>
        </div>
        
        <!-- Loading 状态 -->
        <div v-if="loading" class="message-row is-ai">
          <el-avatar :size="36" class="avatar-ai">AI</el-avatar>
          <div class="message-bubble loading-bubble">
            <el-icon class="is-loading"><Loading /></el-icon> 正在思考并调用工具...
          </div>
        </div>
      </div>

      <div class="input-area">
        <el-input
          v-model="inputText"
          type="textarea"
          :rows="3"
          placeholder="问点什么吧，例如：'查看当前所有的监控任务' 或 '帮我执行任务 xxx-xxx'"
          @keyup.enter.exact.prevent="sendMessage"
        />
        <el-button type="primary" :icon="'Position'" :loading="loading" @click="sendMessage" class="send-btn">发送</el-button>
      </div>
    </el-card>
  </div>
</template>

<script setup>
import { ref, nextTick } from 'vue'
import { ElMessage } from 'element-plus'
import api from '../api/http'

const messages = ref([
  { role: 'ai', content: '你好，我是 AutoTest 监控助手。我可以帮你：\n1. 查询监控列表\n2. 查询监控运行状态\n3. 立即触发执行任务\n有什么我可以帮你的吗？' }
])
const inputText = ref('')
const loading = ref(false)
const chatContainer = ref(null)

// 简单的 Markdown 换行转换
function formatText(text) {
  if (!text) return ''
  return text.replace(/\n/g, '<br />')
}

function scrollToBottom() {
  nextTick(() => {
    if (chatContainer.value) {
      chatContainer.value.scrollTop = chatContainer.value.scrollHeight
    }
  })
}

async function sendMessage() {
  const text = inputText.value.trim()
  if (!text) return

  // 1. 追加用户消息
  messages.value.push({ role: 'user', content: text })
  inputText.value = ''
  scrollToBottom()

  // 2. 请求后端 Agent
  loading.value = true
  try {
    const res = await api.post('/api/AiAgent/chat', { message: text })
    
    // 3. 追加 AI 响应
    messages.value.push({ role: 'ai', content: res.text || '（没有返回内容）' })
  } catch (err) {
    ElMessage.error(err.message || '请求 AI 服务失败')
    messages.value.push({ role: 'ai', content: `[错误] ${err.message}` })
  } finally {
    loading.value = false
    scrollToBottom()
  }
}
</script>

<style scoped>
.ai-page {
  height: calc(100vh - 120px);
  display: flex;
  flex-direction: column;
}

.chat-card {
  flex: 1;
  display: flex;
  flex-direction: column;
  border: none;
  border-radius: 8px;
}

.chat-card :deep(.el-card__body) {
  flex: 1;
  display: flex;
  flex-direction: column;
  padding: 0;
  overflow: hidden;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.title {
  font-size: 16px;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 6px;
}

.chat-container {
  flex: 1;
  padding: 20px;
  overflow-y: auto;
  background-color: #f5f7fa;
}

.message-row {
  display: flex;
  margin-bottom: 20px;
  align-items: flex-start;
  gap: 12px;
}

.is-user {
  justify-content: flex-end;
}

.is-ai {
  justify-content: flex-start;
}

.avatar-ai {
  background-color: #67c23a;
}

.avatar-user {
  background-color: #409eff;
}

.message-bubble {
  max-width: 70%;
  padding: 12px 16px;
  border-radius: 8px;
  font-size: 14px;
  line-height: 1.5;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.05);
}

.is-user .message-bubble {
  background-color: #409eff;
  color: #fff;
  border-top-right-radius: 2px;
}

.is-ai .message-bubble {
  background-color: #fff;
  color: #303133;
  border-top-left-radius: 2px;
}

.loading-bubble {
  display: flex;
  align-items: center;
  gap: 8px;
  color: #909399 !important;
}

.input-area {
  padding: 16px;
  background: #fff;
  border-top: 1px solid #ebeef5;
  display: flex;
  gap: 12px;
  align-items: flex-end;
}

.send-btn {
  height: 75px;
  width: 100px;
}
</style>