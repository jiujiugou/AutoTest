<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { Search, Refresh, VideoPlay, InfoFilled } from '@element-plus/icons-vue'
import { MonitorsApi } from '../api/monitors'
import { ensureMonitorHubStarted, getMonitorHubConnection } from '../realtime/monitorHub'

// ── State ──
const loading = ref(false)
const loadingDetail = ref(false)
const keyword = ref('')
const monitors = ref([])
const selected = ref(null)
const records = ref([])
const runtimeStats = ref(null)
const topErrors = ref([])
let hubConn = null

// ── Analysis drawer ──
const analysisDrawer = ref(false)
const analysisLoading = ref(false)
const analysisError = ref(false)
const analysisData = ref(null)

// ── Computed ──
const filtered = computed(() => {
  const k = String(keyword.value || '').trim().toLowerCase()
  if (!k) return monitors.value
  return (monitors.value || []).filter(x => String(x.name || '').toLowerCase().includes(k))
})

const statusInfo = (s) => {
  const map = { 0: ['等待中','info'], 1: ['运行中','warning'], 2: ['成功','success'], 3: ['失败','danger'], 4: ['超时','warning'], 5: ['已取消','info'] }
  const [text, type] = map[Number(s)] || ['未知','info']
  return { text, type }
}

const overall = computed(() => {
  const s = runtimeStats.value
  if (!s) return { total: 0, successRate: 0, failRate: 0 }
  const total = Number(s.total || 0)
  const success = Number(s.success || 0)
  const fail = Number(s.fail || 0)
  return {
    total,
    successRate: total > 0 ? Math.round(success / total * 100) : 0,
    failRate: total > 0 ? Math.round(fail / total * 100) : 0
  }
})

// ── Helpers ──
function formatTime(s) { try { return new Date(s).toLocaleString() } catch { return String(s || '') } }
function duration(r) {
  if (!r?.startedAt || !r?.finishedAt) return '-'
  const ms = new Date(r.finishedAt) - new Date(r.startedAt)
  return ms < 1000 ? `${ms}ms` : `${Math.round(ms / 1000)}s`
}
function safeJsonParse(s) { try { return JSON.parse(String(s || '')) } catch { return null } }
function parseMeta(r) {
  const p = safeJsonParse(r?.resultJson)
  return { statusCode: p?.StatusCode ?? p?.statusCode, elapsedMs: p?.ElapsedMilliseconds ?? p?.elapsedMilliseconds }
}

// ── Data ──
async function refresh() {
  loading.value = true
  try {
    const rows = await MonitorsApi.list()
    monitors.value = (rows || []).map(x => ({
      id: x.id || x.Id, name: x.name || x.Name,
      targetType: x.targetType || x.TargetType, status: x.status ?? x.Status,
      isEnabled: x.isEnabled
    }))
  } catch { /* ignore */ }
  finally { loading.value = false }
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
      const meta = parseMeta(r)
      return { ...r, __statusCode: meta.statusCode, __elapsedMs: meta.elapsedMs }
    })
    runtimeStats.value = statsResp?.stats || null
    topErrors.value = statsResp?.topErrors || []
  } catch { /* ignore */ }
  finally { loadingDetail.value = false }
}

// ── AI ──
async function showAnalysis(record) {
  analysisDrawer.value = true
  analysisLoading.value = true
  analysisError.value = false
  analysisData.value = null
  try {
    const res = await MonitorsApi.executionAnalysis(record.id)
    if (res?.summary) analysisData.value = res
    else analysisError.value = true
  } catch { analysisError.value = true }
  finally { analysisLoading.value = false }
}

// ── SignalR ──
function setRowStatus(mid, status) {
  const idx = (monitors.value || []).findIndex(x => String(x.id) === String(mid))
  if (idx >= 0) monitors.value[idx].status = status
}

function onMonitorUpdated(p) {
  if (!p?.monitorId) return
  if (selected.value?.id && String(selected.value.id) === String(p.monitorId)) {
    if (p.status === 'running') { setRowStatus(p.monitorId, 1); ElMessage.info('开始执行...'); return }
    if (p.status === 'finished') { setRowStatus(p.monitorId, p?.record?.status ?? p?.record?.Status); loadDetail(); ElMessage.success('执行完成'); return }
  }
  if (p.status === 'running') { setRowStatus(p.monitorId, 1); return }
  if (p.status === 'finished') { setRowStatus(p.monitorId, p?.record?.status ?? p?.record?.Status) }
}

onMounted(async () => {
  await refresh()
  try {
    hubConn = await ensureMonitorHubStarted()
    hubConn.off('monitorUpdated', onMonitorUpdated)
    hubConn.on('monitorUpdated', onMonitorUpdated)
  } catch { /* ignore */ }
})

onBeforeUnmount(() => {
  try { (hubConn || getMonitorHubConnection()).off('monitorUpdated', onMonitorUpdated) } catch { /* ignore */ }
})
</script>

<template>
  <div class="monitor-page">
    <!-- ====== 左侧列表 ====== -->
    <aside class="sidebar">
      <div class="sidebar-head">
        <el-input v-model="keyword" placeholder="搜索..." :prefix-icon="Search" clearable size="large" />
        <el-button :icon="Refresh" size="large" @click="refresh" :loading="loading">刷新</el-button>
      </div>
      <div class="sidebar-list">
        <div v-if="filtered.length === 0" class="empty"><el-empty description="暂无监控" :image-size="80" /></div>
        <div
          v-for="m in filtered" :key="m.id"
          class="monitor-card" :class="{ active: selected?.id === m.id }"
          @click="selectRow(m)"
        >
          <div class="card-top">
            <span class="card-name">{{ m.name }}</span>
            <el-tag :type="statusInfo(m.status).type" size="small" effect="dark">
              {{ statusInfo(m.status).text }}
            </el-tag>
          </div>
          <div class="card-meta">
            <el-tag size="small" effect="plain">{{ m.targetType }}</el-tag>
            <span class="dot" v-if="m.isEnabled === false">已暂停</span>
          </div>
        </div>
      </div>
    </aside>

    <!-- ====== 右侧详情 ====== -->
    <main class="detail" v-if="selected">
      <header class="detail-head">
        <h2>{{ selected.name }}</h2>
        <el-tag :type="statusInfo(selected.status).type" effect="dark">{{ statusInfo(selected.status).text }}</el-tag>
        <el-button :icon="Refresh" size="small" @click="loadDetail" :loading="loadingDetail">刷新</el-button>
      </header>

      <div class="detail-body">
        <!-- KPI 卡片 -->
        <section class="kpi-row">
          <div class="kpi-card">
            <div class="kpi-num">{{ overall.total }}</div>
            <div class="kpi-label">总执行</div>
          </div>
          <div class="kpi-card success">
            <div class="kpi-num">{{ overall.successRate }}%</div>
            <div class="kpi-label">成功率</div>
          </div>
          <div class="kpi-card danger">
            <div class="kpi-num">{{ overall.failRate }}%</div>
            <div class="kpi-label">失败率</div>
          </div>
          <div class="kpi-card" v-if="topErrors.length">
            <div class="kpi-num">{{ topErrors.length }}</div>
            <div class="kpi-label">Top 错误</div>
          </div>
        </section>

        <!-- 执行记录 -->
        <section class="section">
          <h3 class="section-title">执行记录</h3>
          <el-table :data="records" size="small" v-loading="loadingDetail" empty-text="暂无记录" max-height="320">
            <el-table-column label="时间" width="160">
              <template #default="{ row }">{{ formatTime(row.startedAt) }}</template>
            </el-table-column>
            <el-table-column label="结果" width="80">
              <template #default="{ row }">
                <el-tag size="small" :type="row.isExecutionSuccess ? 'success' : 'danger'">
                  {{ row.isExecutionSuccess ? '通过' : '失败' }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="耗时" width="80">
              <template #default="{ row }">{{ duration(row) }}</template>
            </el-table-column>
            <el-table-column label="错误" min-width="200" show-overflow-tooltip>
              <template #default="{ row }">{{ row.errorMessage || '-' }}</template>
            </el-table-column>
            <el-table-column label="AI" width="70" align="center">
              <template #default="{ row }">
                <el-button v-if="!row.isExecutionSuccess" size="small" type="warning" text :icon="InfoFilled" @click="showAnalysis(row)">分析</el-button>
              </template>
            </el-table-column>
          </el-table>
        </section>

        <!-- Top 错误 -->
        <section class="section" v-if="topErrors.length">
          <h3 class="section-title">Top 错误</h3>
          <div class="error-grid">
            <div v-for="e in topErrors.slice(0, 6)" :key="e.errorMessage" class="error-item">
              <div class="err-count">{{ e.count }}</div>
              <div class="err-msg">{{ e.errorMessage || '未知错误' }}</div>
            </div>
          </div>
        </section>
      </div>
    </main>

    <!-- 空状态 -->
    <main class="detail empty-detail" v-else>
      <el-empty description="选择左侧监控查看详情" :image-size="120" />
    </main>

    <!-- AI 分析抽屉 -->
    <el-drawer v-model="analysisDrawer" title="AI 故障分析" size="480px" direction="rtl">
      <template v-if="analysisLoading"><el-skeleton :rows="6" animated /></template>
      <template v-else-if="analysisError"><el-empty description="暂无分析结果" /></template>
      <template v-else-if="analysisData">
        <div class="analysis">
          <div class="analysis-tags">
            <el-tag :type="{ critical:'danger', high:'warning', medium:'warning', low:'info' }[analysisData.severity] || 'info'">
              {{ { critical:'严重', high:'高', medium:'中', low:'低' }[analysisData.severity] || analysisData.severity }}
            </el-tag>
            <el-tag type="info">置信度 {{ (analysisData.confidence * 100).toFixed(0) }}%</el-tag>
            <el-tag v-if="analysisData.type" type="info">{{ analysisData.type }}</el-tag>
          </div>
          <div class="analysis-field"><span>分类</span> {{ analysisData.category || '未分类' }}</div>
          <div class="analysis-field"><span>摘要</span> {{ analysisData.summary }}</div>
          <div class="analysis-field"><span>根因</span><pre>{{ analysisData.rootCause }}</pre></div>
          <div class="analysis-field"><span>建议</span><pre class="suggestion">{{ analysisData.suggestion }}</pre></div>
          <div class="analysis-field"><span>时间</span> {{ formatTime(analysisData.createdAt) }}</div>
        </div>
      </template>
    </el-drawer>
  </div>
</template>

<style scoped>
.monitor-page { display: flex; height: calc(100vh - 80px); background: #f5f7fa; }

/* ── Sidebar ── */
.sidebar { width: 370px; min-width: 260px; background: #fff; border-right: 1px solid #ebeef5; display: flex; flex-direction: column; }
.sidebar-head { display: flex; gap: 10px; padding: 16px; border-bottom: 1px solid #ebeef5; background: #fafbfc; }
.sidebar-list { flex: 1; overflow-y: auto; padding: 8px 12px; }
.empty { padding-top: 60px; }

.monitor-card { padding: 14px 16px; margin-bottom: 6px; border-radius: 8px; border: 1px solid transparent; cursor: pointer; transition: all .15s; }
.monitor-card:hover { background: #f0f5ff; border-color: #c6d8ff; }
.monitor-card.active { background: #e8f0ff; border-color: #91b5ff; }
.card-top { display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px; }
.card-name { font-size: 14px; font-weight: 500; color: #303133; flex: 1; margin-right: 8px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.card-meta { display: flex; align-items: center; gap: 8px; }
.dot { font-size: 12px; color: #909399; }

/* ── Detail ── */
.detail { flex: 1; display: flex; flex-direction: column; overflow: hidden; }
.detail-head { display: flex; align-items: center; gap: 12px; padding: 16px 24px; background: #fff; border-bottom: 1px solid #ebeef5; }
.detail-head h2 { margin: 0; font-size: 18px; font-weight: 600; }
.detail-body { flex: 1; overflow-y: auto; padding: 24px; }
.empty-detail { display: flex; align-items: center; justify-content: center; }

/* ── KPI cards ── */
.kpi-row { display: flex; gap: 12px; margin-bottom: 20px; }
.kpi-card { flex: 1; background: #fff; border: 1px solid #ebeef5; border-radius: 10px; padding: 16px; text-align: center; }
.kpi-card.success { border-left: 3px solid #67c23a; }
.kpi-card.danger { border-left: 3px solid #f56c6c; }
.kpi-num { font-size: 24px; font-weight: 700; color: #303133; }
.kpi-label { font-size: 12px; color: #909399; margin-top: 4px; }

/* ── Sections ── */
.section { background: #fff; border: 1px solid #ebeef5; border-radius: 10px; padding: 20px 24px; margin-bottom: 16px; }
.section-title { margin: 0 0 16px; font-size: 15px; font-weight: 600; color: #303133; }

/* ── Error grid ── */
.error-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.error-item { background: #fafafa; border: 1px solid #ebeef5; border-radius: 8px; padding: 12px; }
.err-count { font-size: 20px; font-weight: 700; color: #f56c6c; }
.err-msg { font-size: 13px; color: #606266; margin-top: 4px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

/* ── AI drawer ── */
.analysis { padding: 0 8px; }
.analysis-tags { display: flex; gap: 8px; margin-bottom: 20px; }
.analysis-field { margin-bottom: 18px; }
.analysis-field > span { display: block; font-size: 13px; color: #909399; margin-bottom: 6px; font-weight: 500; }
.analysis-field pre { margin: 0; background: #f5f7fa; border: 1px solid #e4e7ed; border-radius: 6px; padding: 12px 16px; font-size: 14px; line-height: 1.7; white-space: pre-wrap; word-break: break-word; }
.analysis-field pre.suggestion { border-left: 3px solid #e6a23c; }
</style>
