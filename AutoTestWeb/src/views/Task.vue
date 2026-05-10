<script setup>
import { ref, computed, nextTick, onMounted, onBeforeUnmount } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Search, Plus, Delete, VideoPlay, Download } from '@element-plus/icons-vue'
import { MonitorsApi } from '../api/monitors'
import { ensureMonitorHubStarted, getMonitorHubConnection } from '../realtime/monitorHub'
import TargetHttpForm from '../components/targets/TargetHttpForm.vue'
import TargetTcpForm from '../components/targets/TargetTcpForm.vue'
import TargetDbForm from '../components/targets/TargetDbForm.vue'
import TargetPythonForm from '../components/targets/TargetPythonForm.vue'
import TargetTemplateForm from '../components/targets/TargetTemplateForm.vue'
import AssertionEditor from '../components/AssertionEditor.vue'
import { presets } from './presets'
import QuickImportDialog from './QuickImportDialog.vue'

// ── State ──
const monitors = ref([])
const keyword = ref('')
const saving = ref(false)
const debugVisible = ref(false)
const debugEntry = ref(null)

const form = ref({ id: '', name: '', targetType: 'HTTP', isEnabled: true, assertExpected: '200', autoDailyEnabled: false, autoDailyTime: '09:00', maxRuns: 0, executedCount: 0 })

const httpConfig = ref({ url: 'https://example.com', method: 'Get', timeout: 10, headers: [{ key: '', value: '' }], query: [{ key: '', value: '' }], authType: 'None', authToken: '', authUsername: '', authPassword: '', useCookies: false, allowAutoRedirect: false, maxRedirects: 5, ignoreSslErrors: false, proxyUrl: '', proxyUser: '', proxyPass: '', enableRetry: false, retryCount: 1, retryDelayMs: 200, enableRateLimit: false, bodyType: 'Json', bodyContentType: 'application/json', bodyText: '', formFields: [{ key: '', value: '' }] })
const tcpConfig = ref({ host: '127.0.0.1', port: 80, timeout: 5, useTls: false, ignoreSslErrors: false, connectTimeoutMs: 15000, readTimeoutMs: 30000, writeTimeoutMs: 10000, enableRetry: false, retryCount: 2, retryDelayMs: 500, messagesText: '', responseContains: '', latencyLessThan: null })
const dbConfig = ref({ dbType: 'sqlserver', commandType: 'Query', timeoutSeconds: 30, enableRetry: false, retryCount: 2, retryDelayMs: 500, connectionString: '', sql: '' })
const pythonConfig = ref({ scriptPath: '', scriptContent: '', scriptFileName: '', pythonExecutable: 'python', timeoutSeconds: 60, workingDirectory: '', argsText: '', enableRetry: false, retryCount: 0, retryDelayMs: 1000, enableRateLimit: false, maxConcurrency: 1, env: [{ key: '', value: '' }], successExitCodesText: '0', stdOutContains: '', stdErrContains: '' })
const templateConfig = ref({ dslJson: '', variablesJson: '' })
const formAssertions = ref([])
const targetFormRef = ref(null)

const targetConfigs = { HTTP: httpConfig, TCP: tcpConfig, DB: dbConfig, PYTHON: pythonConfig, TEMPLATE: templateConfig }
const targetConfig = computed(() => targetConfigs[form.value.targetType] || httpConfig)
function getTargetForm() { return targetFormRef.value }

// ── Helpers ──
function safeJsonParse(s) { try { return JSON.parse(s) } catch { return null } }

const targetTypeTag = (t) => ({ HTTP: 'success', TCP: 'warning', DB: '', PYTHON: 'danger', TEMPLATE: 'primary' }[t] || 'info')
const targetTypeIcon = (t) => {
  const map = { HTTP: 'Monitor', TCP: 'Connection', DB: 'Coin', PYTHON: 'Document', TEMPLATE: 'SetUp' }
  return map[t] || 'InfoFilled'
}

const filtered = computed(() => {
  const k = String(keyword.value || '').trim().toLowerCase()
  if (!k) return monitors.value
  return (monitors.value || []).filter(x => String(x.name || '').toLowerCase().includes(k))
})

async function loadMonitors() {
  try { monitors.value = await MonitorsApi.list() || [] } catch { /* ignore */ }
}

// ── Form actions ──
function fillTemplate() {
  const f = getTargetForm()
  if (f?.reset) f.reset()
}

function createNew() {
  form.value = { id: '', name: '', targetType: 'HTTP', isEnabled: true, assertExpected: '200', autoDailyEnabled: false, autoDailyTime: '09:00', maxRuns: 0, executedCount: 0 }
  formAssertions.value = []
  nextTick(() => { const f = getTargetForm(); if (f?.reset) f.reset() })
}

// ── Quick import dialog ──
const importDialogVisible = ref(false)
const importPresetKey = ref('')

function quickImport(presetKey) {
  importPresetKey.value = presetKey
  importDialogVisible.value = true
}

function onImportDone() {
  importDialogVisible.value = false
  loadMonitors()
}

async function editRow(row) {
  try {
    const m = await MonitorsApi.get(row.id)
    form.value.id = m.id || m.Id
    form.value.name = m.name || m.Name
    form.value.targetType = m.targetType || m.TargetType || 'HTTP'
    form.value.isEnabled = !!m.isEnabled
    form.value.autoDailyEnabled = m.autoDailyEnabled ?? m.AutoDailyEnabled ?? false
    form.value.autoDailyTime = m.autoDailyTime ?? m.AutoDailyTime ?? '09:00'
    form.value.maxRuns = m.maxRuns ?? m.MaxRuns ?? 0
    form.value.executedCount = m.executedCount ?? m.ExecutedCount ?? 0
    const savedAssertions = m.assertions || m.Assertions || []

    if (form.value.targetType === 'HTTP') {
      const httpAssertion = savedAssertions.find(a => {
        if ((a.type || a.Type) !== 'HTTP') return false
        const ac = safeJsonParse(a.configJson || a.ConfigJson)
        return ac && (ac.Field || ac.field) === 'StatusCode'
      })
      if (httpAssertion) {
        const ac = safeJsonParse(httpAssertion.configJson || httpAssertion.ConfigJson)
        form.value.assertExpected = (ac.Expected ?? ac.expected ?? '200').toString()
      } else {
        form.value.assertExpected = '200'
      }
    } else {
      form.value.assertExpected = '200'
    }

    formAssertions.value = savedAssertions.map(a => {
      const ac = safeJsonParse(a.configJson || a.ConfigJson) || {}
      return {
        id: a.id || a.Id,
        type: a.type || a.Type,
        field: ac.Field || ac.field || '',
        operator: ac.Operator || ac.operator || 'Equal',
        expected: (ac.Expected ?? ac.expected ?? '').toString(),
        headerKey: ac.HeaderKey || ac.headerKey || ''
      }
    })

    const cfgText = m.targetConfig || m.TargetConfig
    const cfg = cfgText ? safeJsonParse(cfgText) : (m.target || {})

    await nextTick()
    const f = getTargetForm()
    if (f?.loadFrom) {
      if (form.value.targetType === 'TEMPLATE') f.loadFrom(cfgText, m)
      else f.loadFrom(cfg, m)
    }
  } catch (e) { ElMessage.error('加载失败: ' + (e.message || e)) }
}

async function save() {
  debugEntry.value = null
  try {
    const f = getTargetForm()
    const err = f?.validate ? f.validate() : null
    if (err) { ElMessage.warning(err); return }

    saving.value = true
    const targetObj = f.buildTargetConfig()
    const targetText = form.value.targetType === 'TEMPLATE'
      ? (f.buildTargetConfigText ? f.buildTargetConfigText() : JSON.stringify(targetObj))
      : JSON.stringify(targetObj)

    const assertions = formAssertions.value
      .filter(a => a.field && a.expected !== undefined && a.expected !== '')
      .map(a => ({
        Id: a.id || crypto.randomUUID(),
        Type: a.type || form.value.targetType,
        ConfigJson: JSON.stringify({ Id: a.id || crypto.randomUUID(), Field: a.field, Operator: a.operator || 'Equal', Expected: String(a.expected), HeaderKey: a.headerKey || '' })
      }))
    // Also collect assertions from target form (e.g. TCP/Python embedded assertions)
    if (f?.buildAssertions) {
      for (const a of f.buildAssertions()) {
        const alreadyIn = assertions.some(e => {
          try { const ac = JSON.parse(e.ConfigJson); return ac.Field === a.field } catch { return false }
        })
        if (!alreadyIn) {
          const id = crypto.randomUUID()
          assertions.push({ Id: id, Type: form.value.targetType, ConfigJson: JSON.stringify({ Id: id, ...a }) })
        }
      }
    }

    const dto = {
      ...(form.value.id ? { Id: form.value.id } : {}),
      Name: form.value.name, TargetType: form.value.targetType,
      TargetConfig: targetText, IsEnabled: form.value.isEnabled,
      AutoDailyEnabled: form.value.autoDailyEnabled, AutoDailyTime: form.value.autoDailyTime || null,
      MaxRuns: form.value.maxRuns > 0 ? form.value.maxRuns : null, ExecutedCount: form.value.executedCount,
      IsTemplate: form.value.targetType === 'TEMPLATE',
      TemplateVariablesJson: form.value.targetType === 'TEMPLATE' ? ((templateConfig.value.variablesJson || '').trim() || null) : null,
      Assertions: assertions
    }

    const method = form.value.id ? 'PUT' : 'POST'
    const url = form.value.id ? `/api/monitor/${form.value.id}` : '/api/monitor'

    let res
    if (form.value.id) res = await MonitorsApi.update(form.value.id, dto)
    else res = await MonitorsApi.create(dto)

    debugEntry.value = { time: new Date().toLocaleTimeString(), method, url, request: dto, response: res, ok: true }
    if (!debugVisible.value) debugVisible.value = true

    ElMessage.success(form.value.id ? '已更新' : '已创建')
    createNew()
    await loadMonitors()
  } catch (e) {
    debugEntry.value = {
      time: new Date().toLocaleTimeString(),
      method: form.value.id ? 'PUT' : 'POST',
      url: form.value.id ? `/api/monitor/${form.value.id}` : '/api/monitor',
      request: null, // 会在下面重新构建
      error: {
        message: e.message || String(e),
        status: e.status,
        data: e.data,
        headers: e.headers,
        config: e.config
      },
      ok: false
    }
    // 如果请求体已经构建完成，也记录下来
    try {
      const f2 = getTargetForm()
      if (f2?.buildTargetConfig) {
        const to = f2.buildTargetConfig()
        const tt = form.value.targetType === 'TEMPLATE'
          ? (f2.buildTargetConfigText ? f2.buildTargetConfigText() : JSON.stringify(to))
          : JSON.stringify(to)
        debugEntry.value.request = {
          Id: form.value.id || null, Name: form.value.name, TargetType: form.value.targetType,
          TargetConfig: tt, IsEnabled: form.value.isEnabled,
          AutoDailyEnabled: form.value.autoDailyEnabled, AutoDailyTime: form.value.autoDailyTime || null,
          MaxRuns: form.value.maxRuns > 0 ? form.value.maxRuns : null,
          TemplateVariablesJson: form.value.targetType === 'TEMPLATE' ? ((templateConfig.value.variablesJson || '').trim() || null) : null,
        }
      }
    } catch { /* ignore */ }
    if (!debugVisible.value) debugVisible.value = true
    ElMessage.error('保存失败: ' + (e.message || e))
  }
  finally { saving.value = false }
}

async function runMonitor(id) {
  try { await MonitorsApi.run(id); ElMessage.success('已触发执行'); await loadMonitors() }
  catch (e) { ElMessage.error('执行失败: ' + (e.message || e)) }
}

async function removeMonitor(id) {
  try {
    await ElMessageBox.confirm('确定要删除此监控任务吗？此操作不可恢复。', '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' })
    await MonitorsApi.remove(id)
    ElMessage.success('已删除')
    if (form.value.id === id) createNew()
    await loadMonitors()
  } catch { /* cancelled */ }
}

async function toggleEnabled(row) {
  try { await MonitorsApi.setEnabled(row.id, row.isEnabled); await loadMonitors() }
  catch (e) { ElMessage.error('操作失败: ' + (e.message || e)) }
}

// ── Status ──
const statusInfo = (s) => {
  const map = { 0: ['等待中','info'], 1: ['运行中','warning'], 2: ['成功','success'], 3: ['失败','danger'], 4: ['超时','warning'], 5: ['已取消','info'] }
  const [text, type] = map[Number(s)] || ['未知','info']
  return { text, type }
}

// ── SignalR ──
let hubConn = null
function onMonitorUpdated(p) {
  if (!p?.monitorId) return
  const idx = (monitors.value || []).findIndex(x => String(x.id) === String(p.monitorId))
  if (idx < 0) return
  if (p.status === 'running') { monitors.value[idx].status = 1; return }
  if (p.status === 'finished') { monitors.value[idx].status = p?.record?.status ?? p?.record?.Status; return }
}

onMounted(async () => {
  await loadMonitors()
  try {
    hubConn = await ensureMonitorHubStarted()
    hubConn.off('monitorUpdated', onMonitorUpdated)
    hubConn.on('monitorUpdated', onMonitorUpdated)
  } catch { /* ignore */ }
})

onBeforeUnmount(() => {
  try { (hubConn || getMonitorHubConnection()).off('monitorUpdated', onMonitorUpdated) } catch { /* ignore */ }
})

// ── Batch ops ──
const selectedIds = ref(new Set())
const batchRunning = ref(false)

function toggleSelect(id) {
  const next = new Set(selectedIds.value)
  next.has(id) ? next.delete(id) : next.add(id)
  selectedIds.value = next
}
function toggleSelectAll() {
  const all = new Set(filtered.value.map(x => x.id))
  selectedIds.value = selectedIds.value.size === all.size ? new Set() : all
}
const selectAll = computed(() => filtered.value.length > 0 && selectedIds.value.size === filtered.value.length)
const selectedCount = computed(() => selectedIds.value.size)

async function batchRun() {
  if (selectedIds.value.size === 0) return
  batchRunning.value = true
  try {
    const ids = [...selectedIds.value]
    for (const id of ids) {
      try { await MonitorsApi.run(id) } catch { /* continue */ }
    }
    ElMessage.success(`已触发 ${ids.length} 个任务`)
    selectedIds.value = new Set()
    await loadMonitors()
  } catch { /* ignore */ }
  finally { batchRunning.value = false }
}

async function batchDelete() {
  if (selectedIds.value.size === 0) return
  try {
    await ElMessageBox.confirm(
      `确定要删除选中的 ${selectedIds.value.size} 个任务吗？此操作不可恢复。`,
      '批量删除',
      { confirmButtonText: '全部删除', cancelButtonText: '取消', type: 'warning' }
    )
    const ids = [...selectedIds.value]
    for (const id of ids) {
      try { await MonitorsApi.remove(id) } catch { /* continue */ }
    }
    ElMessage.success(`已删除 ${ids.length} 个任务`)
    if (form.value.id && ids.includes(form.value.id)) createNew()
    selectedIds.value = new Set()
    await loadMonitors()
  } catch { /* cancelled */ }
}

</script>

<template>
  <div class="task-page">
    <!-- ====== 左侧列表 ====== -->
    <aside class="task-sidebar">
      <div class="sidebar-head">
        <template v-if="selectedCount > 0">
          <span class="selected-hint">已选 {{ selectedCount }} 个</span>
          <el-button type="primary" :icon="VideoPlay" :loading="batchRunning" @click="batchRun">执行</el-button>
          <el-button type="danger" :icon="Delete" @click="batchDelete">删除</el-button>
          <el-button @click="selectedIds = new Set()">取消</el-button>
        </template>
        <template v-else>
          <el-input v-model="keyword" placeholder="搜索任务..." :prefix-icon="Search" clearable size="large" />
          <el-dropdown @command="quickImport" style="margin-right:8px">
            <el-button :icon="Download" size="large">导入</el-button>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item command="http_health">{{ presets.http_health.label }}</el-dropdown-item>
                <el-dropdown-item command="redis_port">{{ presets.redis_port.label }}</el-dropdown-item>
                <el-dropdown-item command="mysql_port">{{ presets.mysql_port.label }}</el-dropdown-item>
                <el-dropdown-item command="external_reachable">{{ presets.external_reachable.label }}</el-dropdown-item>
                <el-dropdown-item divided command="login_check_userinfo">{{ presets.login_check_userinfo.label }}</el-dropdown-item>
                <el-dropdown-item command="cert_expiry">{{ presets.cert_expiry.label }}</el-dropdown-item>
                <el-dropdown-item command="db_rowcount">{{ presets.db_rowcount.label }}</el-dropdown-item>
                <el-dropdown-item command="slow_task">{{ presets.slow_task.label }}</el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
          <el-button type="primary" :icon="Plus" size="large" @click="createNew">新建</el-button>
        </template>
      </div>
      <div class="sidebar-list">
        <div v-if="selectedCount > 0" class="select-bar">
          <el-checkbox :model-value="selectAll" @change="toggleSelectAll" :indeterminate="selectedCount > 0 && !selectAll">全选</el-checkbox>
        </div>
        <div v-if="filtered.length === 0" class="empty-state">
          <el-empty description="暂无任务" :image-size="80" />
        </div>
        <div
          v-for="m in filtered" :key="m.id"
          class="task-card"
          :class="{ active: form.id === m.id }"
        >
          <div class="card-top">
            <el-checkbox :model-value="selectedIds.has(m.id)" @click.stop @change="toggleSelect(m.id)" />
            <span class="card-name" @click="editRow(m)">{{ m.name }}</span>
            <el-tag :type="statusInfo(m.status).type" size="small" effect="dark">
              {{ statusInfo(m.status).text }}
            </el-tag>
            <el-tag :type="targetTypeTag(m.targetType)" size="small" effect="plain">{{ m.targetType }}</el-tag>
          </div>
          <div class="card-actions">
            <el-switch :model-value="m.isEnabled" size="small" @click.stop @change="toggleEnabled(m)" />
            <el-button size="small" text type="primary" @click.stop="runMonitor(m.id)" :icon="VideoPlay">执行</el-button>
          </div>
        </div>
      </div>
    </aside>

    <!-- ====== 右侧表单 ====== -->
    <main class="task-editor" v-if="form.targetType">
      <header class="editor-head">
        <h2>{{ form.id ? '编辑任务' : '新建任务' }}</h2>
        <el-tag v-if="form.id" :type="targetTypeTag(form.targetType)" effect="dark">{{ form.targetType }}</el-tag>
      </header>

      <div class="editor-body">
        <!-- 基本信息 -->
        <section class="form-section">
          <h3 class="section-title">基本信息</h3>
          <div class="form-row">
            <label class="form-label">任务名称</label>
            <el-input v-model="form.name" placeholder="例如：登录接口检测" />
          </div>
          <div class="form-row inline">
            <label class="form-label">目标类型</label>
            <el-radio-group v-model="form.targetType" @change="fillTemplate" size="small">
              <el-radio-button value="HTTP">HTTP</el-radio-button>
              <el-radio-button value="TCP">TCP</el-radio-button>
              <el-radio-button value="DB">DB</el-radio-button>
              <el-radio-button value="PYTHON">Python</el-radio-button>
              <el-radio-button value="TEMPLATE">模板</el-radio-button>
            </el-radio-group>
          </div>
          <div class="form-row inline">
            <label class="form-label">启用</label>
            <el-switch v-model="form.isEnabled" />
          </div>
        </section>

        <!-- 目标配置 -->
        <section class="form-section">
          <h3 class="section-title">目标配置</h3>
          <TargetHttpForm v-if="form.targetType === 'HTTP'" ref="targetFormRef" :config="httpConfig" />
          <TargetTcpForm v-if="form.targetType === 'TCP'" ref="targetFormRef" :config="tcpConfig" />
          <TargetDbForm v-if="form.targetType === 'DB'" ref="targetFormRef" :config="dbConfig" />
          <TargetPythonForm v-if="form.targetType === 'PYTHON'" ref="targetFormRef" :config="pythonConfig" />
          <TargetTemplateForm v-if="form.targetType === 'TEMPLATE'" ref="targetFormRef" :config="templateConfig" />
        </section>

        <!-- 断言 -->
        <section class="form-section" v-if="form.targetType !== 'TEMPLATE'">
          <h3 class="section-title">断言</h3>
          <AssertionEditor
            :target-type="form.targetType"
            v-model="formAssertions"
          />
        </section>

        <!-- 调度 -->
        <section class="form-section">
          <h3 class="section-title">调度</h3>
          <div class="form-row inline">
            <label class="form-label">每日自动执行</label>
            <el-switch v-model="form.autoDailyEnabled" />
            <el-time-select
              v-if="form.autoDailyEnabled"
              v-model="form.autoDailyTime"
              placeholder="09:00"
              start="00:00" step="00:30" end="23:30"
              style="width:140px"
            />
          </div>
          <div class="form-row inline">
            <label class="form-label">最大执行次数</label>
            <el-input-number v-model="form.maxRuns" :min="0" :step="1" placeholder="0=不限" style="width:160px" />
            <span class="hint">已执行 {{ form.executedCount }} 次</span>
          </div>
        </section>
      </div>

      <!-- 调试面板 -->
      <section v-if="debugVisible && debugEntry" class="debug-panel">
        <div class="debug-head">
          <span class="debug-dot" :class="{ ok: debugEntry.ok, err: !debugEntry.ok }"></span>
          <span>{{ debugEntry.method }} {{ debugEntry.url }}</span>
          <span v-if="!debugEntry.ok && debugEntry.error?.status" class="debug-status">HTTP {{ debugEntry.error.status }}</span>
          <span class="debug-time">{{ debugEntry.time }}</span>
          <el-button size="small" text @click="debugVisible = false">收起</el-button>
        </div>
        <div class="debug-body">
          <details open>
            <summary>请求体</summary>
            <pre class="debug-json">{{ JSON.stringify(debugEntry.request, null, 2) }}</pre>
          </details>
          <details v-if="debugEntry.ok" open>
            <summary>响应 (成功)</summary>
            <pre class="debug-json">{{ JSON.stringify(debugEntry.response, null, 2) }}</pre>
          </details>
          <details v-if="!debugEntry.ok" open>
            <summary>错误详情</summary>
            <pre class="debug-json">{{ JSON.stringify(debugEntry.error, null, 2) }}</pre>
          </details>
        </div>
      </section>

      <!-- 操作栏 -->
      <footer class="editor-foot">
        <el-button type="primary" size="large" @click="save" :loading="saving" :icon="Plus">
          {{ form.id ? '保存修改' : '创建任务' }}
        </el-button>
        <el-button v-if="form.id" size="large" type="danger" :icon="Delete" @click="removeMonitor(form.id)" plain>
          删除任务
        </el-button>
        <el-button v-if="!debugVisible" size="small" text type="info" @click="debugVisible = true">调试</el-button>
      </footer>
    </main>

    <QuickImportDialog
      :visible="importDialogVisible"
      :preset-key="importPresetKey"
      @close="importDialogVisible = false"
      @done="onImportDone"
    />
  </div>
</template>

<style scoped>
.task-page { display: flex; height: calc(100vh - 80px); background: #f5f7fa; }

/* ── Sidebar ── */
.task-sidebar {
  width: 380px; min-width: 280px; background: #fff;
  border-right: 1px solid #ebeef5; display: flex; flex-direction: column;
}
.sidebar-head {
  display: flex; gap: 10px; padding: 16px;
  border-bottom: 1px solid #ebeef5; background: #fafbfc;
}
.sidebar-list { flex: 1; overflow-y: auto; padding: 8px 12px; }
.empty-state { padding-top: 60px; }
.selected-hint { font-size: 14px; font-weight: 600; color: #409eff; white-space: nowrap; }
.select-bar { padding: 6px 16px; border-bottom: 1px solid #ebeef5; }

.task-card {
  padding: 14px 16px; margin-bottom: 6px; border-radius: 8px;
  border: 1px solid transparent; cursor: pointer;
  transition: all .15s;
}
.task-card:hover { background: #f0f5ff; border-color: #c6d8ff; }
.task-card.active { background: #e8f0ff; border-color: #91b5ff; }
.card-top { display: flex; align-items: center; gap: 8px; margin-bottom: 8px; }
.card-name { font-size: 14px; font-weight: 500; color: #303133; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; flex: 1; cursor: pointer; }
.card-actions { display: flex; justify-content: space-between; align-items: center; }

/* ── Editor ── */
.task-editor { flex: 1; display: flex; flex-direction: column; overflow: hidden; }
.editor-head {
  display: flex; align-items: center; gap: 12px; padding: 16px 24px;
  background: #fff; border-bottom: 1px solid #ebeef5;
}
.editor-head h2 { margin: 0; font-size: 18px; font-weight: 600; }
.editor-body { flex: 1; overflow-y: auto; padding: 24px; }

.form-section {
  background: #fff; border-radius: 10px; padding: 20px 24px;
  margin-bottom: 16px; border: 1px solid #ebeef5;
}
.section-title { margin: 0 0 16px; font-size: 15px; font-weight: 600; color: #303133; }
.form-row { margin-bottom: 14px; }
.form-row.inline { display: flex; align-items: center; gap: 12px; }
.form-label { display: block; margin-bottom: 6px; font-size: 13px; color: #606266; font-weight: 500; }
.inline .form-label { margin-bottom: 0; min-width: 100px; }
.hint { font-size: 12px; color: #909399; }

.editor-foot {
  display: flex; gap: 12px; padding: 16px 24px;
  background: #fff; border-top: 1px solid #ebeef5;
}

/* ── Override target form internals ── */
:deep(.kv-grid) { display: grid; grid-template-columns: 1fr 1fr; gap: 10px 16px; width: 100%; }
:deep(.kv-span2) { grid-column: span 2; }
:deep(.kv-item) { display: flex; flex-direction: column; gap: 4px; }
:deep(.kv-label) { font-size: 12px; color: #909399; margin-bottom: 2px; }
:deep(.kv-list) { display: flex; flex-direction: column; gap: 6px; }
:deep(.kv-row) { display: flex; gap: 8px; align-items: center; }
:deep(.file-row) { display: flex; gap: 8px; align-items: center; }
:deep(.hidden-file) { display: none; }
:deep(.muted) { color: #909399; font-size: 12px; }
:deep(.code-input textarea) { font-family: 'Consolas', 'Courier New', monospace; font-size: 13px; }

/* ── Debug panel ── */
.debug-panel {
  margin: 0 24px; background: #1e1e2e; border-radius: 8px;
  border: 1px solid #45475a; overflow: hidden;
}
.debug-head {
  display: flex; align-items: center; gap: 10px;
  padding: 8px 14px; background: #181825; font-size: 13px;
  color: #cdd6f4; font-family: 'Consolas', 'Courier New', monospace;
}
.debug-dot { width: 8px; height: 8px; border-radius: 50%; }
.debug-dot.ok { background: #a6e3a1; }
.debug-dot.err { background: #f38ba8; }
.debug-status { color: #f38ba8; font-weight: 600; }
.debug-time { margin-left: auto; color: #6c7086; font-size: 12px; }
.debug-body { padding: 8px 14px 12px; max-height: 360px; overflow-y: auto; }
.debug-body details { margin-bottom: 6px; }
.debug-body summary {
  font-size: 13px; font-weight: 500; color: #a6adc8; cursor: pointer;
  padding: 4px 0; user-select: none;
}
.debug-json {
  margin: 4px 0; padding: 10px 14px; background: #11111b;
  border-radius: 6px; font-size: 12px; line-height: 1.5;
  color: #cdd6f4; white-space: pre-wrap; word-break: break-all;
  max-height: 240px; overflow: auto;
}
</style>
