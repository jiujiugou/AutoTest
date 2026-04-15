<template>
  <div class="log-page">
    <el-card shadow="never" class="log-card">
      <template #header>
        <div class="card-header">
          <span class="title">系统操作日志</span>
          <div class="actions">
            <el-radio-group v-model="mode" size="small" @change="handleModeChange">
              <el-radio-button label="history">历史模式 (ES)</el-radio-button>
              <el-radio-button label="realtime">实时模式 (SignalR)</el-radio-button>
            </el-radio-group>
            
            <template v-if="mode === 'history'">
              <el-select v-model="level" size="small" style="width: 100px" placeholder="级别">
                <el-option label="全部" value="" />
                <el-option label="INFO" value="INFO" />
                <el-option label="WARN" value="WARN" />
                <el-option label="ERROR" value="ERROR" />
                <el-option label="DEBUG" value="DEBUG" />
                <el-option label="FATAL" value="FATAL" />
              </el-select>
              <el-input placeholder="模块..." v-model="moduleQuery" style="width: 120px" size="small" />
              <el-date-picker
                v-model="timeRange"
                type="datetimerange"
                size="small"
                start-placeholder="开始时间"
                end-placeholder="结束时间"
                style="width: 320px"
              />
              <el-input placeholder="关键词..." v-model="searchQuery" style="width: 160px" size="small">
                <template #prefix>
                  <el-icon><Search /></el-icon>
                </template>
              </el-input>
               <el-button size="small" :icon="'Search'" type="primary" :loading="loading" @click="refresh">查询</el-button>
            </template>
            <template v-else>
              <el-button size="small" type="danger" :icon="'Delete'" plain @click="logs = []">清屏</el-button>
            </template>
          </div>
        </div>
      </template>

      <div class="body">
        <div class="footer" v-if="mode === 'history'">
          <el-button size="small" :loading="loadingMore" :disabled="!hasMore" @click="loadMore">
            {{ hasMore ? '加载更早' : '没有更多了' }}
          </el-button>
        </div>
        <el-table :data="logs" stripe style="width: 100%" height="100%" empty-text="暂无日志数据">
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
import { ref, onMounted, onBeforeUnmount } from 'vue'
import { ElMessage } from 'element-plus'
import api from '../api/http'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

const mode = ref('history') // 'history' | 'realtime'

const searchQuery = ref('')
const moduleQuery = ref('')
const level = ref('')
const timeRange = ref(null)

const logs = ref([])
const nextCursor = ref(null)
const hasMore = ref(false)
const loadingMore = ref(false)
const loading = ref(false)

let connection = null

function getLevelType(level) {
  const map = { 'INFO': 'info', 'WARN': 'warning', 'ERROR': 'danger', 'DEBUG': '', 'FATAL': 'danger' }
  return map[level] || 'info'
}

function handleModeChange(val) {
  if (val === 'history') {
    logs.value = [] // 只有主动切回历史模式时，才清空重新查
    refresh()
  } else {
    // 💡 切换到实时模式时，保留现有的表格数据作为“垫底”
    hasMore.value = false
    nextCursor.value = null
    ElMessage.success('已开启实时日志流') // 给个提示，让用户知道切成功了
  }
}

function refresh() {
  if (mode.value === 'history') {
    fetchLogs({ reset: true })
  }
}

async function fetchLogs({ reset }) {
  if (mode.value !== 'history') return
  
  loading.value = true
  try {
    const params = {
      take: 100,
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
    
    if (reset) logs.value = mapped
    else logs.value = [...logs.value, ...mapped]
    
    nextCursor.value = res?.next || null
    hasMore.value = !!res?.hasMore
  } catch (err) {
    ElMessage.error(err?.message || '获取日志失败')
  } finally {
    loading.value = false
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

async function startRealtime() {
  if (connection) return

  connection = new HubConnectionBuilder()
    .withUrl('/hubs/logs', {
      accessTokenFactory: () => localStorage.getItem('accessToken') || ''
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning) // 调回 Warning，保持前端控制台干净
    .build()

  connection.on('logAppended', (item) => {
    // 💡 核心修复：如果是历史模式，收到后端的数据直接丢掉，啥也不干！
console.log('📥 浏览器收到日志推送:', item); 
    if (mode.value !== 'realtime') return
    
    const mapped = {
      cursor: item?.cursor || '',
      timestamp: item?.timestamp ?? '',
      level: item?.level ?? 'INFO',
      module: item?.module ?? 'Webapi',
      message: item?.message ?? ''
    }
    
    // 只有在实时模式下，才把数据塞进数组触发视图更新
    const next = [mapped, ...logs.value]
    logs.value = next.length > 500 ? next.slice(0, 500) : next
  })

  try {
    await connection.start()
    console.log('✅ SignalR 实时日志通道已连接')
  } catch (err) {
    console.error('❌ SignalR 连接失败:', err)
  }
}

onMounted(() => {
  if (mode.value === 'history') {
    fetchLogs({ reset: true })
  }
  // 组件一挂载，就默默建立后台连接
  startRealtime()
})

onBeforeUnmount(async () => {
  if (connection) {
    await connection.stop()
    connection = null
  }
})
</script>

<style scoped>
.log-page {
  height: 100%;
  display: flex;
  flex-direction: column;
}
.log-card {
  height: 100%;
  display: flex;
  flex-direction: column;
}
.log-card :deep(.el-card__body) {
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  padding: 0;
}
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.title {
  font-weight: bold;
}
.actions {
  display: flex;
  gap: 10px;
  align-items: center;
}
.body {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.footer {
  padding: 10px;
  text-align: center;
  border-bottom: 1px solid #ebeef5;
  background-color: #fafafa;
}
</style>