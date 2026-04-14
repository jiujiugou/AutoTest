<template>
  <div class="log-page">
    <el-card shadow="never" class="log-card">
      <template #header>
        <div class="card-header">
          <span class="title">系统操作日志</span>
          <div class="actions">
            <el-select v-model="level" size="small" style="width: 120px">
              <el-option label="全部" value="" />
              <el-option label="INFO" value="INFO" />
              <el-option label="WARN" value="WARN" />
              <el-option label="ERROR" value="ERROR" />
              <el-option label="DEBUG" value="DEBUG" />
              <el-option label="FATAL" value="FATAL" />
            </el-select>
            <el-input placeholder="模块..." v-model="moduleQuery" style="width: 160px" size="small" />
            <el-date-picker
              v-model="timeRange"
              type="datetimerange"
              size="small"
              start-placeholder="开始时间"
              end-placeholder="结束时间"
              style="width: 320px"
            />
            <el-input placeholder="关键词..." v-model="searchQuery" style="width: 200px" size="small">
              <template #prefix>
                <el-icon><Search /></el-icon>
              </template>
            </el-input>
            <el-button size="small" :icon="'Refresh'" @click="refresh">刷新</el-button>
            <el-button size="small" type="danger" :icon="'Delete'" plain @click="clearLogs">清空</el-button>
          </div>
        </div>
      </template>

      <div class="body">
        <div class="footer">
          <el-button size="small" :loading="loadingMore" :disabled="!hasMore" @click="loadMore">
            {{ hasMore ? '加载更早' : '没有更多了' }}
          </el-button>
        </div>
        <el-table :data="logs" stripe style="width: 100%" height="100%">
          <el-table-column prop="timestamp" label="时间" width="180" />
        <el-table-column prop="level" label="级别" width="100">
          <template #default="{ row }">
            <el-tag :type="getLevelType(row.level)" size="small" effect="dark">
              {{ row.level }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="module" label="模块" width="150" />
        <el-table-column prop="message" label="日志内容" show-overflow-tooltip />
        </el-table>
      </div>
    </el-card>
  </div>
</template>

<script setup>
import { ref, watch, onMounted, onBeforeUnmount } from 'vue'
import { ElMessage } from 'element-plus'
import api from '../api/http'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

const searchQuery = ref('')
const moduleQuery = ref('')
const level = ref('')
const timeRange = ref(null)

const logs = ref([])
const nextCursor = ref(null)
const hasMore = ref(false)
const loadingMore = ref(false)
let connection = null

function getLevelType(level) {
  const map = {
    'INFO': 'info',
    'WARN': 'warning',
    'ERROR': 'danger',
    'DEBUG': ''
  }
  return map[level] || 'info'
}

function refresh() {
  fetchLogs({ reset: true })
}

async function fetchLogs({ reset }) {
  try {
    const params = {
      take: 200,
      level: level.value || null,
      module: moduleQuery.value || null,
      keyword: searchQuery.value || null,
      fromUtc: Array.isArray(timeRange.value) && timeRange.value[0] ? new Date(timeRange.value[0]).toISOString() : null,
      toUtc: Array.isArray(timeRange.value) && timeRange.value[1] ? new Date(timeRange.value[1]).toISOString() : null,
      before: reset ? null : nextCursor.value
    }
    const res = await api.get('/api/logs', { params })
    const items = Array.isArray(res?.items) ? res.items : []
    const mapped = items.map(x => ({
      cursor: x?.cursor || '',
      timestamp: x?.timestamp ?? '',
      level: x?.level ?? 'INFO',
      module: x?.module ?? 'Webapi',
      message: x?.message ?? ''
    }))
    const ordered = [...mapped].reverse()
    if (reset) logs.value = ordered
    else logs.value = [...ordered, ...logs.value]
    nextCursor.value = res?.next || null
    hasMore.value = !!res?.hasMore
  } catch (err) {
    ElMessage.error(err?.message || '获取日志失败')
  }
}

async function loadMore() {
  if (!hasMore.value || loadingMore.value) return
  loadingMore.value = true
  try {
    await fetchLogs({ reset: false })
  } finally {
    loadingMore.value = false
  }
}

async function clearLogs() {
  try {
    await api.delete('/api/logs')
    ElMessage.success('已清空')
    await fetchLogs({ reset: true })
  } catch (err) {
    ElMessage.error(err?.message || '清空失败')
  }
}

async function startRealtime() {
  if (connection) return
  connection = new HubConnectionBuilder()
    .withUrl('/hubs/logs', {
      accessTokenFactory: () => localStorage.getItem('accessToken') || ''
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()

  connection.on('LogAppended', (item) => {
    if (level.value || moduleQuery.value || searchQuery.value || timeRange.value) return
    const mapped = {
      cursor: item?.cursor || '',
      timestamp: item?.timestamp ?? '',
      level: item?.level ?? 'INFO',
      module: item?.module ?? 'Webapi',
      message: item?.message ?? ''
    }
    const next = [...logs.value, mapped]
    logs.value = next.length > 2000 ? next.slice(next.length - 2000) : next
  })

  try {
    await connection.start()
  } catch {
    connection = null
  }
}

onMounted(() => {
  fetchLogs({ reset: true })
  startRealtime()
})

onBeforeUnmount(async () => {
  if (!connection) return
  try {
    await connection.stop()
  } finally {
    connection = null
  }
})

watch([searchQuery, moduleQuery, level], () => {
  fetchLogs({ reset: true })
})

watch(timeRange, () => {
  fetchLogs({ reset: true })
})
</script>

<style scoped>
.log-page {
  height: calc(100vh - 120px);
  display: flex;
  flex-direction: column;
}

.log-card {
  flex: 1;
  display: flex;
  flex-direction: column;
  border: none;
  border-radius: 8px;
}

.log-card :deep(.el-card__body) {
  flex: 1;
  overflow: hidden;
  padding: 10px;
}

.body {
  height: 100%;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.body :deep(.el-table) {
  flex: 1;
}

.footer {
  display: flex;
  justify-content: center;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.title {
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}

.actions {
  display: flex;
  gap: 12px;
  align-items: center;
}
</style>
