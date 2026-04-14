<template>
  <div class="page-container">
    <div class="toolbar">
      <div class="toolbar-left">
        <el-input v-model="keyword" placeholder="搜索监控" style="width: 260px" clearable>
          <template #prefix>
            <el-icon><Search /></el-icon>
          </template>
        </el-input>
      </div>
      <div class="toolbar-right">
        <el-button :icon="'Refresh'" @click="refresh">刷新</el-button>
      </div>
    </div>

    <el-row :gutter="20">
      <el-col :span="8">
        <el-card shadow="never" class="list-card">
          <template #header>
            <div class="card-header">
              <span>监控列表（观测）</span>
              <el-tag size="small" type="info">{{ filtered.length }} 个</el-tag>
            </div>
          </template>
          <el-table
            :data="filtered"
            size="small"
            v-loading="loading"
            highlight-current-row
            @row-click="selectRow"
          >
            <el-table-column prop="name" label="监控" min-width="140" show-overflow-tooltip />
            <el-table-column prop="targetType" label="类型" width="80">
              <template #default="{ row }">
                <el-tag size="small" effect="plain">{{ row.targetType }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="状态" width="90" align="center">
              <template #default="{ row }">
                <el-tag size="small" :type="statusInfo(row.status).type">
                  {{ statusInfo(row.status).text }}
                </el-tag>
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </el-col>

      <el-col :span="16">
        <el-card shadow="never" class="detail-card">
          <template #header>
            <div class="card-header">
              <span class="detail-title">{{ selected ? selected.name : '请选择一个监控' }}</span>
              <div class="actions">
                <el-button :disabled="!selected" :loading="loadingDetail" :icon="'RefreshRight'" @click="loadDetail">
                  刷新数据
                </el-button>
              </div>
            </div>
          </template>

          <div v-if="!selected" class="empty">
            <el-empty description="请选择左侧监控，查看执行记录与指标" />
          </div>

          <div v-else class="content">
            <el-row :gutter="16" class="kpi-row">
              <el-col :span="4">
                <el-card shadow="hover" class="kpi-card">
                  <div class="kpi-title">总执行次数</div>
                  <el-statistic :value="overall.total" />
                </el-card>
              </el-col>
              <el-col :span="4">
                <el-card shadow="hover" class="kpi-card">
                  <div class="kpi-title">成功率</div>
                  <el-statistic :value="overall.successRate" :precision="1" suffix="%" />
                </el-card>
              </el-col>
              <el-col :span="4">
                <el-card shadow="hover" class="kpi-card">
                  <div class="kpi-title">失败率</div>
                  <el-statistic :value="overall.failRate" :precision="1" suffix="%" />
                </el-card>
              </el-col>
              <el-col :span="4">
                <el-card shadow="hover" class="kpi-card">
                  <div class="kpi-title">最后执行</div>
                  <div class="kpi-value">{{ overall.lastStartedAt ? formatTime(overall.lastStartedAt) : '-' }}</div>
                </el-card>
              </el-col>
              <el-col :span="4">
                <el-card shadow="hover" class="kpi-card">
                  <div class="kpi-title">平均响应</div>
                  <el-statistic :value="kpiWindow.avgMs" suffix=" ms" />
                </el-card>
              </el-col>
              <el-col :span="4">
                <el-card shadow="hover" class="kpi-card">
                  <div class="kpi-title">P95 响应</div>
                  <el-statistic :value="kpiWindow.p95Ms" suffix=" ms" />
                </el-card>
              </el-col>
            </el-row>

            <el-row :gutter="16" class="grid-row">
              <el-col :span="12">
                <el-card shadow="never" class="panel">
                  <template #header>
                    <div class="panel-header">错误分析（499 / 4xx / 5xx）</div>
                  </template>
                  <div class="error-grid">
                    <div class="error-item">
                      <div class="label">499</div>
                      <div class="value danger">{{ errorAgg.c499 }}</div>
                    </div>
                    <div class="error-item">
                      <div class="label">4xx</div>
                      <div class="value warning">{{ errorAgg.c4xx }}</div>
                    </div>
                    <div class="error-item">
                      <div class="label">5xx</div>
                      <div class="value danger">{{ errorAgg.c5xx }}</div>
                    </div>
                    <div class="error-item">
                      <div class="label">其他失败</div>
                      <div class="value">{{ errorAgg.otherFail }}</div>
                    </div>
                  </div>
                </el-card>
              </el-col>

              <el-col :span="12">
                <el-card shadow="never" class="panel">
                  <template #header>
                    <div class="panel-header">失败原因 TOP</div>
                  </template>
                  <el-empty v-if="topErrors.length === 0" description="暂无失败记录" />
                  <el-table v-else :data="topErrors" size="small" stripe border style="width: 100%">
                    <el-table-column prop="errorMessage" label="错误原因" show-overflow-tooltip />
                    <el-table-column prop="count" label="次数" width="90" align="center" />
                    <el-table-column prop="lastOccurredAt" label="最近发生" width="180">
                      <template #default="{ row }">{{ formatTime(row.lastOccurredAt) }}</template>
                    </el-table-column>
                  </el-table>
                </el-card>
              </el-col>
            </el-row>

            <el-card shadow="never" class="panel" style="margin-top: 16px">
              <template #header>
                <div class="panel-header">执行记录</div>
              </template>
              <el-table :data="records" stripe border size="small" v-loading="loadingDetail" style="width: 100%">
                <el-table-column prop="startedAt" label="开始时间" width="180">
                  <template #default="{ row }">{{ formatTime(row.startedAt) }}</template>
                </el-table-column>
                <el-table-column prop="finishedAt" label="结束时间" width="180">
                  <template #default="{ row }">{{ row.finishedAt ? formatTime(row.finishedAt) : '-' }}</template>
                </el-table-column>
                <el-table-column label="耗时" width="110" align="center">
                  <template #default="{ row }">{{ durationText(row) }}</template>
                </el-table-column>
                <el-table-column label="结果" width="90" align="center">
                  <template #default="{ row }">
                    <el-tag :type="row.isExecutionSuccess ? 'success' : 'danger'" size="small">
                      {{ row.isExecutionSuccess ? '成功' : '失败' }}
                    </el-tag>
                  </template>
                </el-table-column>
                <el-table-column label="状态码" width="90" align="center">
                  <template #default="{ row }">{{ row.__statusCode ?? '-' }}</template>
                </el-table-column>
                <el-table-column label="响应时间" width="110" align="center">
                  <template #default="{ row }">{{ row.__elapsedMs != null ? `${row.__elapsedMs}ms` : '-' }}</template>
                </el-table-column>
                <el-table-column prop="errorMessage" label="错误信息" show-overflow-tooltip />
              </el-table>
            </el-card>
          </div>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { MonitorsApi } from '../api/monitors'
import { ensureMonitorHubStarted, getMonitorHubConnection } from '../realtime/monitorHub'

const loading = ref(false)
const loadingDetail = ref(false)
const keyword = ref('')

const monitors = ref([])
const selected = ref(null)
const records = ref([])
const runtimeStats = ref(null)
const topErrors = ref([])

const filtered = computed(() => {
  const k = String(keyword.value || '').trim().toLowerCase()
  if (!k) return monitors.value
  return (monitors.value || []).filter(x => String(x.name || '').toLowerCase().includes(k))
})

function statusInfo(status) {
  const s = Number(status)
  if (s === 0) return { text: '等待中', type: 'info' }
  if (s === 1) return { text: '运行中', type: 'warning' }
  if (s === 2) return { text: '成功', type: 'success' }
  if (s === 3) return { text: '失败', type: 'danger' }
  if (s === 4) return { text: '超时', type: 'warning' }
  if (s === 5) return { text: '已取消', type: 'info' }
  return { text: '未知', type: 'info' }
}

function setRowStatus(monitorId, status) {
  const idx = (monitors.value || []).findIndex(x => String(x.id) === String(monitorId))
  if (idx >= 0) monitors.value[idx].status = status
}

function formatTime(s) {
  try {
    return new Date(s).toLocaleString()
  } catch {
    return String(s || '')
  }
}

function safeJsonParse(s) {
  try {
    return JSON.parse(String(s || ''))
  } catch {
    return null
  }
}

function parseExecutionMeta(record) {
  const payload = safeJsonParse(record?.resultJson)
  const statusCode = payload?.StatusCode ?? payload?.statusCode
  const elapsedMs = payload?.ElapsedMilliseconds ?? payload?.elapsedMilliseconds
  return { statusCode, elapsedMs }
}

async function refresh() {
  loading.value = true
  try {
    const rows = await MonitorsApi.list()
    monitors.value = (rows || []).map(x => ({
      id: x.id || x.Id,
      name: x.name || x.Name,
      targetType: x.targetType || x.TargetType,
      status: x.status ?? x.Status
    }))
  } catch (e) {
    ElMessage.error(e.message || String(e))
  } finally {
    loading.value = false
  }
}

async function selectRow(row) {
  if (!row?.id) return
  selected.value = row
  await loadDetail()
}

async function loadDetail() {
  if (!selected.value?.id) return
  loadingDetail.value = true
  try {
    const [rows, statsResp] = await Promise.all([
      MonitorsApi.executions(selected.value.id, 50),
      MonitorsApi.runtimeStats(selected.value.id, 10)
    ])
    records.value = (rows || []).map(r => {
      const meta = parseExecutionMeta(r)
      return { ...r, __statusCode: meta.statusCode, __elapsedMs: meta.elapsedMs }
    })
    runtimeStats.value = statsResp?.stats || null
    topErrors.value = statsResp?.topErrors || []
  } catch (e) {
    ElMessage.error(e.message || String(e))
  } finally {
    loadingDetail.value = false
  }
}

function durationText(row) {
  const s = row?.startedAt
  const f = row?.finishedAt
  if (!s || !f) return '-'
  const ms = new Date(f).getTime() - new Date(s).getTime()
  if (!Number.isFinite(ms) || ms < 0) return '-'
  if (ms < 1000) return `${ms}ms`
  const sec = Math.round(ms / 1000)
  return `${sec}s`
}

const overall = computed(() => {
  const s = runtimeStats.value
  if (!s) return { total: 0, successRate: 0, failRate: 0, lastStartedAt: null }
  const total = Number(s.total || 0)
  const success = Number(s.success || 0)
  const fail = Number(s.fail || 0)
  const successRate = total > 0 ? (success / total) * 100 : 0
  const failRate = total > 0 ? (fail / total) * 100 : 0
  return { total, successRate, failRate, lastStartedAt: s.lastStartedAt || null }
})

const kpiWindow = computed(() => {
  const arr = records.value || []
  if (arr.length === 0) return { avgMs: 0, p95Ms: 0 }

  const ms = arr.map(x => x.__elapsedMs).filter(x => Number.isFinite(x) && x >= 0)
  const avgMs = ms.length ? Math.round(ms.reduce((a, b) => a + b, 0) / ms.length) : 0
  const p95Ms = (() => {
    if (ms.length === 0) return 0
    const sorted = [...ms].sort((a, b) => a - b)
    const idx = Math.min(sorted.length - 1, Math.floor(sorted.length * 0.95) - 1)
    return sorted[Math.max(0, idx)]
  })()

  return { avgMs, p95Ms }
})

const errorAgg = computed(() => {
  const arr = records.value || []
  let c499 = 0
  let c4xx = 0
  let c5xx = 0
  let otherFail = 0

  for (const r of arr) {
    if (r.isExecutionSuccess) continue
    const sc = r.__statusCode
    if (sc === 499) c499++
    else if (typeof sc === 'number' && sc >= 500) c5xx++
    else if (typeof sc === 'number' && sc >= 400) c4xx++
    else otherFail++
  }

  return { c499, c4xx, c5xx, otherFail }
})

 

let hubConn = null
function onMonitorUpdated(payload) {
  const mid = payload?.monitorId
  if (!mid) return

  if (selected.value?.id && String(selected.value.id) === String(mid)) {
    if (payload.status === 'running') {
      setRowStatus(mid, 1)
      ElMessage.info('开始执行…')
      return
    }
    if (payload.status === 'finished') {
      const recordStatus = payload?.record?.status ?? payload?.record?.Status
      if (recordStatus != null) setRowStatus(mid, recordStatus)
      loadDetail()
      ElMessage.success('执行完成')
      return
    }
  }

  if (payload.status === 'running') {
    setRowStatus(mid, 1)
    return
  }
  if (payload.status === 'finished') {
    const recordStatus = payload?.record?.status ?? payload?.record?.Status
    if (recordStatus != null) setRowStatus(mid, recordStatus)
    return
  }
}

onMounted(async () => {
  await refresh()
  try {
    hubConn = await ensureMonitorHubStarted()
    hubConn.off('monitorUpdated', onMonitorUpdated)
    hubConn.on('monitorUpdated', onMonitorUpdated)
  } catch (e) {
    ElMessage.warning(e.message || String(e))
  }
})

onBeforeUnmount(() => {
  try {
    const c = hubConn || getMonitorHubConnection()
    c.off('monitorUpdated', onMonitorUpdated)
  } catch {
  }
})
</script>

<style scoped>
.page-container {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: #fff;
  padding: 12px 20px;
  border-radius: 8px;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.05);
}

.toolbar-left,
.toolbar-right {
  display: flex;
  gap: 12px;
  align-items: center;
}

.list-card,
.detail-card {
  border-radius: 8px;
  height: calc(100vh - 160px);
  display: flex;
  flex-direction: column;
}

.list-card :deep(.el-card__body),
.detail-card :deep(.el-card__body) {
  flex: 1;
  overflow-y: auto;
  padding: 20px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-weight: 600;
}

.detail-title {
  font-size: 16px;
  color: #303133;
}

.actions {
  display: flex;
  gap: 10px;
}

.empty {
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.content {
  display: flex;
  flex-direction: column;
}

.kpi-row {
  margin-bottom: 16px;
}

.kpi-card {
  border: none;
  border-radius: 8px;
}

.kpi-title {
  font-size: 13px;
  color: #909399;
  margin-bottom: 8px;
}

.kpi-value {
  font-size: 14px;
  font-weight: 600;
  color: #303133;
}

.panel {
  border-radius: 8px;
  border: 1px solid #ebeef5;
  background: #fff;
}

.panel-header {
  font-size: 14px;
  font-weight: 600;
  color: #606266;
}

.grid-row {
  margin-top: 6px;
}

.error-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.error-item {
  background: #f8f9fa;
  border: 1px solid #ebeef5;
  border-radius: 8px;
  padding: 12px;
}

.error-item .label {
  font-size: 12px;
  color: #909399;
}

.error-item .value {
  margin-top: 6px;
  font-size: 22px;
  font-weight: 700;
  color: #303133;
}

.warning {
  color: #e6a23c;
}

.danger {
  color: #f56c6c;
}

.alert-line {
  display: flex;
  align-items: center;
  gap: 10px;
}

.alert-msg {
  font-size: 13px;
  color: #303133;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
