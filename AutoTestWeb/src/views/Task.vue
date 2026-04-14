<template>
  <div class="page-container">
    <div class="toolbar">
      <div class="toolbar-left">
        <el-input v-model="keyword" placeholder="搜索任务名称" style="width: 260px" clearable>
          <template #prefix>
            <el-icon><Search /></el-icon>
          </template>
        </el-input>
      </div>
      <div class="toolbar-right">
        <el-button :icon="'Refresh'" @click="refresh">刷新列表</el-button>
        <el-button type="primary" :icon="'Plus'" @click="openCreate">创建任务</el-button>
      </div>
    </div>

    <el-row :gutter="20">
      <el-col :span="10">
        <el-card shadow="never" class="list-card">
          <template #header>
            <div class="card-header">
              <span>任务列表（调度控制）</span>
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
            <el-table-column prop="name" label="任务" min-width="140" show-overflow-tooltip />
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
            <el-table-column label="每日执行" width="90" align="center">
              <template #default="{ row }">
                <el-tag size="small" :type="row.autoDailyEnabled ? 'success' : 'info'">
                  {{ row.autoDailyEnabled ? '是' : '否' }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="autoDailyTime" label="执行时间" width="90" align="center" />
            <el-table-column label="次数" width="110" align="center">
              <template #default="{ row }">
                <span class="muted">{{ row.executedCount }}</span>
                <span class="muted"> / </span>
                <span class="muted">{{ row.maxRuns ?? '∞' }}</span>
              </template>
            </el-table-column>
            <el-table-column label="暂停/恢复" width="120" align="center">
              <template #default="{ row }">
                <el-switch
                  :model-value="row.isEnabled"
                  :loading="row.__toggling"
                  @change="val => toggleEnabled(row, val)"
                />
              </template>
            </el-table-column>
            <el-table-column label="执行" width="90" align="center">
              <template #default="{ row }">
                <el-button size="small" type="primary" link :disabled="isRunningStatus(row.status)" @click.stop="runOnce(row)">
                  {{ isRunningStatus(row.status) ? '运行中' : '执行' }}
                </el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </el-col>

      <el-col :span="14">
        <el-card shadow="never" class="detail-card">
          <template #header>
            <div class="card-header">
              <span class="detail-title">{{ form.id ? '编辑任务：' + (form.name || '') : '创建任务' }}</span>
              <div class="actions">
                <el-button type="primary" :icon="'Select'" :loading="saving" @click="save">保存</el-button>
                <el-button type="danger" :icon="'Delete'" :disabled="!form.id" :loading="saving" @click="remove">删除</el-button>
              </div>
            </div>
          </template>

          <el-form label-width="110px" label-position="left">
            <el-form-item label="任务 ID" v-if="form.id">
              <el-input v-model="form.id" readonly disabled />
            </el-form-item>
            <el-form-item label="任务名称" required>
              <el-input v-model="form.name" placeholder="例如：登录接口巡检" />
            </el-form-item>
            <el-form-item label="目标类型">
              <el-radio-group v-model="form.targetType" @change="fillTemplate">
                <el-radio-button label="HTTP">HTTP / API</el-radio-button>
                <el-radio-button label="TCP">TCP</el-radio-button>
                <el-radio-button label="PYTHON">Python 脚本</el-radio-button>
              </el-radio-group>
            </el-form-item>
            <el-form-item label="暂停/恢复">
              <el-switch v-model="form.isEnabled" />
            </el-form-item>

            <el-form-item label="自动执行">
              <el-switch v-model="form.autoDailyEnabled" />
              <span class="muted" style="margin-left: 10px">启用后由 Hangfire 按时间重复执行</span>
            </el-form-item>
            <el-form-item label="执行时间" v-if="form.autoDailyEnabled">
              <el-time-picker
                v-model="form.autoDailyTime"
                format="HH:mm"
                value-format="HH:mm"
                placeholder="选择时间"
                style="width: 160px"
              />
              <span class="muted" style="margin-left: 10px">每天执行一次</span>
            </el-form-item>
            <el-form-item label="执行次数上限（自动累计）" v-if="form.autoDailyEnabled">
              <el-input-number v-model="form.maxRuns" :min="0" :step="1" />
              <span class="muted" style="margin-left: 10px">0 表示无限；达到上限后将停止后续每日执行</span>
              <span class="muted" style="margin-left: 14px">已执行：{{ form.executedCount }}</span>
            </el-form-item>

            <el-divider />

            <el-form-item label="HTTP 配置" v-if="form.targetType === 'HTTP'">
              <el-collapse v-model="httpPanels">
                <el-collapse-item title="基础" name="base">
                  <div class="kv-grid">
                    <div class="kv-item kv-span2">
                      <div class="kv-label">Url</div>
                      <el-input v-model="httpConfig.url" placeholder="https://example.com" />
                    </div>
                    <div class="kv-item">
                      <div class="kv-label">Method</div>
                      <el-select v-model="httpConfig.method" style="width: 100%">
                        <el-option label="Get" value="Get" />
                        <el-option label="Post" value="Post" />
                        <el-option label="Put" value="Put" />
                        <el-option label="Delete" value="Delete" />
                      </el-select>
                    </div>
                    <div class="kv-item">
                      <div class="kv-label">Timeout(s)</div>
                      <el-input-number v-model="httpConfig.timeout" :min="1" :max="300" :step="1" style="width: 100%" />
                    </div>
                  </div>
                </el-collapse-item>

                <el-collapse-item title="Headers" name="headers">
                  <div class="kv-list">
                    <div v-for="(h, idx) in httpConfig.headers" :key="idx" class="kv-row">
                      <el-input v-model="h.key" placeholder="Header Key" />
                      <el-input v-model="h.value" placeholder="Value (可用逗号分隔多个)" />
                      <el-button type="danger" :icon="'Delete'" plain @click="removeKv(httpConfig.headers, idx)" />
                    </div>
                    <el-button type="primary" plain :icon="'Plus'" @click="addKv(httpConfig.headers)">新增 Header</el-button>
                  </div>
                </el-collapse-item>

                <el-collapse-item title="Query" name="query">
                  <div class="kv-list">
                    <div v-for="(q, idx) in httpConfig.query" :key="idx" class="kv-row">
                      <el-input v-model="q.key" placeholder="Query Key" />
                      <el-input v-model="q.value" placeholder="Value" />
                      <el-button type="danger" :icon="'Delete'" plain @click="removeKv(httpConfig.query, idx)" />
                    </div>
                    <el-button type="primary" plain :icon="'Plus'" @click="addKv(httpConfig.query)">新增 Query</el-button>
                  </div>
                </el-collapse-item>

                <el-collapse-item title="Body" name="body">
                  <div class="kv-grid">
                    <div class="kv-item">
                      <div class="kv-label">BodyType</div>
                      <el-select v-model="httpConfig.bodyType" style="width: 100%">
                        <el-option label="Json" value="Json" />
                        <el-option label="FormUrlEncoded" value="FormUrlEncoded" />
                        <el-option label="Raw" value="Raw" />
                      </el-select>
                    </div>
                    <div class="kv-item">
                      <div class="kv-label">ContentType</div>
                      <el-input v-model="httpConfig.bodyContentType" placeholder="application/json" />
                    </div>
                  </div>

                  <div v-if="httpConfig.bodyType === 'FormUrlEncoded'" class="kv-list" style="margin-top: 12px">
                    <div v-for="(f, idx) in httpConfig.formFields" :key="idx" class="kv-row">
                      <el-input v-model="f.key" placeholder="Field" />
                      <el-input v-model="f.value" placeholder="Value" />
                      <el-button type="danger" :icon="'Delete'" plain @click="removeKv(httpConfig.formFields, idx)" />
                    </div>
                    <el-button type="primary" plain :icon="'Plus'" @click="addKv(httpConfig.formFields)">新增字段</el-button>
                  </div>

                  <div v-else class="kv-item kv-span2" style="margin-top: 12px">
                    <div class="kv-label">Value</div>
                    <el-input v-model="httpConfig.bodyText" type="textarea" :rows="8" placeholder="可为空" />
                  </div>
                </el-collapse-item>

                <el-collapse-item title="认证" name="auth">
                  <div class="kv-grid">
                    <div class="kv-item">
                      <div class="kv-label">AuthType</div>
                      <el-select v-model="httpConfig.authType" style="width: 100%">
                        <el-option label="None" value="None" />
                        <el-option label="Bearer" value="Bearer" />
                        <el-option label="Basic" value="Basic" />
                        <el-option label="ApiKeyHeader (X-Api-Key)" value="ApiKeyHeader" />
                      </el-select>
                    </div>

                    <div v-if="httpConfig.authType === 'Bearer' || httpConfig.authType === 'ApiKeyHeader'" class="kv-item">
                      <div class="kv-label">Token</div>
                      <el-input v-model="httpConfig.authToken" placeholder="token" />
                    </div>

                    <div v-if="httpConfig.authType === 'Basic'" class="kv-item">
                      <div class="kv-label">Username</div>
                      <el-input v-model="httpConfig.authUsername" placeholder="username" />
                    </div>
                    <div v-if="httpConfig.authType === 'Basic'" class="kv-item">
                      <div class="kv-label">Password</div>
                      <el-input v-model="httpConfig.authPassword" type="password" show-password placeholder="password" />
                    </div>
                  </div>
                </el-collapse-item>

                <el-collapse-item title="高级" name="advanced">
                  <div class="kv-grid">
                    <div class="kv-item">
                      <div class="kv-label">UseCookies</div>
                      <el-switch v-model="httpConfig.useCookies" />
                    </div>
                    <div class="kv-item">
                      <div class="kv-label">AllowAutoRedirect</div>
                      <el-switch v-model="httpConfig.allowAutoRedirect" />
                    </div>
                    <div class="kv-item">
                      <div class="kv-label">MaxRedirects</div>
                      <el-input-number v-model="httpConfig.maxRedirects" :min="0" :max="50" :step="1" style="width: 100%" />
                    </div>
                    <div class="kv-item">
                      <div class="kv-label">IgnoreSslErrors</div>
                      <el-switch v-model="httpConfig.ignoreSslErrors" />
                    </div>

                    <div class="kv-item kv-span2">
                      <div class="kv-label">ProxyUrl</div>
                      <el-input v-model="httpConfig.proxyUrl" placeholder="http://127.0.0.1:7890" />
                    </div>
                    <div class="kv-item">
                      <div class="kv-label">ProxyUser</div>
                      <el-input v-model="httpConfig.proxyUser" placeholder="user" />
                    </div>
                    <div class="kv-item">
                      <div class="kv-label">ProxyPass</div>
                      <el-input v-model="httpConfig.proxyPass" type="password" show-password placeholder="pass" />
                    </div>

                    <div class="kv-item">
                      <div class="kv-label">EnableRetry</div>
                      <el-switch v-model="httpConfig.enableRetry" />
                    </div>
                    <div class="kv-item">
                      <div class="kv-label">RetryCount</div>
                      <el-input-number v-model="httpConfig.retryCount" :min="1" :max="20" :step="1" style="width: 100%" :disabled="!httpConfig.enableRetry" />
                    </div>
                    <div class="kv-item">
                      <div class="kv-label">RetryDelayMs</div>
                      <el-input-number v-model="httpConfig.retryDelayMs" :min="0" :max="60000" :step="100" style="width: 100%" :disabled="!httpConfig.enableRetry" />
                    </div>

                    <div class="kv-item">
                      <div class="kv-label">EnableRateLimit</div>
                      <el-switch v-model="httpConfig.enableRateLimit" />
                    </div>
                  </div>
                </el-collapse-item>
              </el-collapse>
            </el-form-item>

            <el-form-item label="TCP 配置" v-if="form.targetType === 'TCP'">
              <div class="kv-grid">
                <div class="kv-item">
                  <div class="kv-label">Host</div>
                  <el-input v-model="tcpConfig.host" placeholder="127.0.0.1" />
                </div>
                <div class="kv-item">
                  <div class="kv-label">Port</div>
                  <el-input-number v-model="tcpConfig.port" :min="1" :max="65535" :step="1" style="width: 100%" />
                </div>
                <div class="kv-item">
                  <div class="kv-label">Timeout(s)</div>
                  <el-input-number v-model="tcpConfig.timeout" :min="1" :max="300" :step="1" style="width: 100%" />
                </div>
                <div class="kv-item kv-span2">
                  <div class="kv-label">Messages（每行一条，可选）</div>
                  <el-input v-model="tcpConfig.messagesText" type="textarea" :rows="6" placeholder="例如：PING" />
                </div>
                <div class="kv-item kv-span2">
                  <div class="kv-label">响应包含（可选）</div>
                  <el-input v-model="tcpConfig.responseContains" placeholder="例如：PONG" />
                </div>
                <div class="kv-item">
                  <div class="kv-label">延迟要求小于(ms)（可选）</div>
                  <el-input-number v-model="tcpConfig.latencyLessThan" :min="0" :step="100" style="width: 100%" placeholder="例如：500" />
                </div>
              </div>
            </el-form-item>
            <el-form-item label="Python 配置" v-if="form.targetType === 'PYTHON'">
              <div class="kv-grid">
                <div class="kv-item kv-span2">
                  <div class="kv-label">ScriptPath</div>
                  <el-input v-model="pythonConfig.scriptPath" placeholder="例如：scripts/check.py" />
                </div>
                <div class="kv-item">
                  <div class="kv-label">PythonExecutable</div>
                  <el-input v-model="pythonConfig.pythonExecutable" placeholder="python / python3 / venv\\Scripts\\python.exe" />
                </div>
                <div class="kv-item">
                  <div class="kv-label">Timeout(s)</div>
                  <el-input-number v-model="pythonConfig.timeoutSeconds" :min="1" :max="3600" :step="1" style="width: 100%" />
                </div>
                <div class="kv-item kv-span2">
                  <div class="kv-label">WorkingDirectory（可选）</div>
                  <el-input v-model="pythonConfig.workingDirectory" placeholder="例如：D:\\work\\scripts" />
                </div>
                <div class="kv-item kv-span2">
                  <div class="kv-label">Args（每行一个参数）</div>
                  <el-input v-model="pythonConfig.argsText" type="textarea" :rows="5" placeholder="例如：--env\nprod" />
                </div>

                <div class="kv-item">
                  <div class="kv-label">EnableRetry</div>
                  <el-switch v-model="pythonConfig.enableRetry" />
                </div>
                <div class="kv-item">
                  <div class="kv-label">RetryCount</div>
                  <el-input-number v-model="pythonConfig.retryCount" :min="0" :max="20" :step="1" style="width: 100%" :disabled="!pythonConfig.enableRetry" />
                </div>
                <div class="kv-item">
                  <div class="kv-label">RetryDelayMs</div>
                  <el-input-number v-model="pythonConfig.retryDelayMs" :min="0" :max="60000" :step="100" style="width: 100%" :disabled="!pythonConfig.enableRetry" />
                </div>
                <div class="kv-item">
                  <div class="kv-label">EnableRateLimit</div>
                  <el-switch v-model="pythonConfig.enableRateLimit" />
                </div>
                <div class="kv-item">
                  <div class="kv-label">MaxConcurrency</div>
                  <el-input-number v-model="pythonConfig.maxConcurrency" :min="1" :max="50" :step="1" style="width: 100%" :disabled="!pythonConfig.enableRateLimit" />
                </div>
                <div class="kv-item">
                  <div class="kv-label">SuccessExitCodes</div>
                  <el-input v-model="pythonConfig.successExitCodesText" placeholder="例如：0,2" />
                </div>
                <div class="kv-item kv-span2">
                  <div class="kv-label">StdOut Contains（可选）</div>
                  <el-input v-model="pythonConfig.stdOutContains" placeholder="例如：OK" />
                </div>
                <div class="kv-item kv-span2">
                  <div class="kv-label">StdErr Contains（可选）</div>
                  <el-input v-model="pythonConfig.stdErrContains" placeholder="例如：Traceback" />
                </div>
              </div>

              <div class="kv-list" style="margin-top: 10px">
                <div class="kv-label">Env（可选）</div>
                <div v-for="(h, idx) in pythonConfig.env" :key="idx" class="kv-row">
                  <el-input v-model="h.key" placeholder="KEY" />
                  <el-input v-model="h.value" placeholder="VALUE" />
                  <el-button type="danger" :icon="'Delete'" plain @click="removeKv(pythonConfig.env, idx)" />
                </div>
                <el-button type="primary" plain :icon="'Plus'" @click="addKv(pythonConfig.env)">新增 Env</el-button>
              </div>
            </el-form-item>
            <el-form-item label="HTTP 校验" v-if="form.targetType === 'HTTP'">
              <el-input v-model="form.assertExpected" placeholder="期望 StatusCode，例如 200；留空不创建断言" style="width: 320px" />
            </el-form-item>

            <el-divider />

            <div class="execute-bar">
              <el-button type="success" :icon="'VideoPlay'" :disabled="!form.id" :loading="running" @click="runOnce()">
                执行任务
              </el-button>
              <el-button :icon="'RefreshRight'" :disabled="!form.id" :loading="running" @click="loadLastSummary">
                刷新上次结果
              </el-button>
            </div>

            <div v-if="lastSummary" class="summary">
              <el-descriptions :column="2" border size="small">
                <el-descriptions-item label="开始时间">{{ formatTime(lastSummary.startedAt) }}</el-descriptions-item>
                <el-descriptions-item label="结束时间">{{ lastSummary.finishedAt ? formatTime(lastSummary.finishedAt) : '-' }}</el-descriptions-item>
                <el-descriptions-item label="执行结果">
                  <el-tag :type="lastSummary.isExecutionSuccess ? 'success' : 'danger'">
                    {{ lastSummary.isExecutionSuccess ? '成功' : '失败' }}
                  </el-tag>
                </el-descriptions-item>
                <el-descriptions-item label="耗时">
                  {{ lastSummary.elapsedMs != null ? `${lastSummary.elapsedMs} ms` : '-' }}
                </el-descriptions-item>
                <el-descriptions-item label="状态码" v-if="form.targetType === 'HTTP'">
                  {{ lastSummary.statusCode != null ? lastSummary.statusCode : '-' }}
                </el-descriptions-item>
                <el-descriptions-item label="已连接" v-if="form.targetType === 'TCP'">
                  <el-tag :type="lastSummary.connected ? 'success' : 'danger'" size="small">
                    {{ lastSummary.connected ? '是' : '否' }}
                  </el-tag>
                </el-descriptions-item>
                <el-descriptions-item label="响应内容" v-if="form.targetType === 'TCP'" :span="2">
                  <div style="max-height: 100px; overflow: auto; white-space: pre-wrap;" class="muted">
                    {{ lastSummary.response || '-' }}
                  </div>
                </el-descriptions-item>
                <el-descriptions-item label="ExitCode" v-if="form.targetType === 'PYTHON'">
                  {{ lastSummary.exitCode != null ? lastSummary.exitCode : '-' }}
                </el-descriptions-item>
                <el-descriptions-item label="TimedOut" v-if="form.targetType === 'PYTHON'">
                  {{ lastSummary.timedOut ? '是' : '否' }}
                </el-descriptions-item>
                <el-descriptions-item label="错误信息">
                  <span class="muted">{{ lastSummary.errorMessage || '-' }}</span>
                </el-descriptions-item>
              </el-descriptions>
            </div>
          </el-form>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { MonitorsApi } from '../api/monitors'
import { ensureMonitorHubStarted, getMonitorHubConnection } from '../realtime/monitorHub'

const loading = ref(false)
const saving = ref(false)
const running = ref(false)
const keyword = ref('')

const monitors = ref([])
const lastSummary = ref(null)
const httpPanels = ref(['base', 'headers', 'query', 'body', 'auth', 'advanced'])

const form = ref({
  id: '',
  name: '',
  targetType: 'HTTP',
  isEnabled: true,
  assertExpected: '200',
  autoDailyEnabled: false,
  autoDailyTime: '09:00',
  maxRuns: 0,
  executedCount: 0
})

const httpConfig = ref({
  url: 'https://example.com',
  method: 'Get',
  timeout: 10,
  headers: [{ key: '', value: '' }],
  query: [{ key: '', value: '' }],
  authType: 'None',
  authToken: '',
  authUsername: '',
  authPassword: '',
  useCookies: false,
  allowAutoRedirect: true,
  maxRedirects: 5,
  ignoreSslErrors: false,
  proxyUrl: '',
  proxyUser: '',
  proxyPass: '',
  enableRetry: false,
  retryCount: 1,
  retryDelayMs: 200,
  enableRateLimit: false,
  bodyType: 'Json',
  bodyContentType: 'application/json',
  bodyText: '',
  formFields: [{ key: '', value: '' }]
})

const tcpConfig = ref({
  host: '127.0.0.1',
  port: 80,
  timeout: 5,
  messagesText: '',
  responseContains: '',
  latencyLessThan: null
})

const pythonConfig = ref({
  scriptPath: '',
  pythonExecutable: 'python',
  timeoutSeconds: 60,
  workingDirectory: '',
  argsText: '',
  enableRetry: false,
  retryCount: 0,
  retryDelayMs: 1000,
  enableRateLimit: false,
  maxConcurrency: 1,
  env: [{ key: '', value: '' }],
  successExitCodesText: '0',
  stdOutContains: '',
  stdErrContains: ''
})

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

function isRunningStatus(status) {
  return Number(status) === 1
}

function setRowStatus(monitorId, status) {
  const idx = (monitors.value || []).findIndex(x => String(x.id) === String(monitorId))
  if (idx >= 0) monitors.value[idx].status = status
}

const currentMonitorStatus = computed(() => {
  const id = form.value.id
  if (!id) return null
  const row = (monitors.value || []).find(x => String(x.id) === String(id))
  return row?.status ?? null
})

function httpTargetTemplate() {
  return {
    Url: 'https://example.com',
    Method: 'Get',
    Body: null,
    Headers: {},
    Query: {},
    Timeout: 10,
    AuthType: 'None',
    AuthToken: '',
    AuthUsername: '',
    AuthPassword: '',
    UseCookies: false,
    AllowAutoRedirect: true,
    MaxRedirects: 5,
    IgnoreSslErrors: false,
    ProxyUrl: '',
    ProxyUser: '',
    ProxyPass: '',
    EnableRetry: false,
    RetryCount: 1,
    RetryDelayMs: 200,
    EnableRateLimit: false
  }
}

function tcpTargetTemplate() {
  return { Host: '127.0.0.1', Port: 80, Timeout: 5 }
}

function pythonTargetTemplate() {
  return {
    ScriptPath: '',
    Args: [],
    WorkingDirectory: null,
    PythonExecutable: 'python',
    TimeoutSeconds: 60,
    EnableRetry: false,
    RetryCount: 0,
    RetryDelayMs: 1000,
    EnableRateLimit: false,
    MaxConcurrency: 1,
    Env: null,
    SuccessExitCodes: [0]
  }
}

function fillTemplate() {
  if (form.value.targetType === 'HTTP') {
    httpConfig.value = {
      url: 'https://example.com',
      method: 'Get',
      timeout: 10,
      headers: [{ key: '', value: '' }],
      query: [{ key: '', value: '' }],
      authType: 'None',
      authToken: '',
      authUsername: '',
      authPassword: '',
      useCookies: false,
      allowAutoRedirect: true,
      maxRedirects: 5,
      ignoreSslErrors: false,
      proxyUrl: '',
      proxyUser: '',
      proxyPass: '',
      enableRetry: false,
      retryCount: 1,
      retryDelayMs: 200,
      enableRateLimit: false,
      bodyType: 'Json',
      bodyContentType: 'application/json',
      bodyText: '',
      formFields: [{ key: '', value: '' }]
    }
  } else if (form.value.targetType === 'TCP') {
    tcpConfig.value = { host: '127.0.0.1', port: 80, timeout: 5, messagesText: '', responseContains: '', latencyLessThan: null }
  } else if (form.value.targetType === 'PYTHON') {
    pythonConfig.value = {
      scriptPath: '',
      pythonExecutable: 'python',
      timeoutSeconds: 60,
      workingDirectory: '',
      argsText: '',
      enableRetry: false,
      retryCount: 0,
      retryDelayMs: 1000,
      enableRateLimit: false,
      maxConcurrency: 1,
      env: [{ key: '', value: '' }],
      successExitCodesText: '0',
      stdOutContains: '',
      stdErrContains: ''
    }
  }
}

function clearForm() {
  form.value = {
    id: '',
    name: '',
    targetType: 'HTTP',
    isEnabled: true,
    assertExpected: '200',
    autoDailyEnabled: false,
    autoDailyTime: '09:00',
    maxRuns: 0,
    executedCount: 0
  }
  httpConfig.value = {
    url: 'https://example.com',
    method: 'Get',
    timeout: 10,
    headers: [{ key: '', value: '' }],
    query: [{ key: '', value: '' }],
    authType: 'None',
    authToken: '',
    authUsername: '',
    authPassword: '',
    useCookies: false,
    allowAutoRedirect: true,
    maxRedirects: 5,
    ignoreSslErrors: false,
    proxyUrl: '',
    proxyUser: '',
    proxyPass: '',
    enableRetry: false,
    retryCount: 1,
    retryDelayMs: 200,
    enableRateLimit: false,
    bodyType: 'Json',
    bodyContentType: 'application/json',
    bodyText: '',
    formFields: [{ key: '', value: '' }]
  }
  tcpConfig.value = { host: '127.0.0.1', port: 80, timeout: 5, messagesText: '', responseContains: '', latencyLessThan: null }
  pythonConfig.value = {
    scriptPath: '',
    pythonExecutable: 'python',
    timeoutSeconds: 60,
    workingDirectory: '',
    argsText: '',
    enableRetry: false,
    retryCount: 0,
    retryDelayMs: 1000,
    enableRateLimit: false,
    maxConcurrency: 1,
    env: [{ key: '', value: '' }],
    successExitCodesText: '0',
    stdOutContains: '',
    stdErrContains: ''
  }
  lastSummary.value = null
}

function addKv(arr) {
  arr.push({ key: '', value: '' })
}

function removeKv(arr, idx) {
  arr.splice(idx, 1)
  if (arr.length === 0) arr.push({ key: '', value: '' })
}

function formatTime(s) {
  try {
    return new Date(s).toLocaleString()
  } catch {
    return String(s || '')
  }
}

async function refresh() {
  loading.value = true
  try {
    const rows = await MonitorsApi.list()
    monitors.value = (rows || []).map(x => ({
      id: x.id || x.Id,
      name: x.name || x.Name,
      targetType: x.targetType || x.TargetType,
      status: x.status ?? x.Status,
      isEnabled: x.isEnabled ?? x.IsEnabled,
      autoDailyEnabled: x.autoDailyEnabled ?? x.AutoDailyEnabled,
      autoDailyTime: x.autoDailyTime ?? x.AutoDailyTime,
      maxRuns: x.maxRuns ?? x.MaxRuns,
      executedCount: x.executedCount ?? x.ExecutedCount
    }))
  } catch (e) {
    ElMessage.error(e.message || String(e))
  } finally {
    loading.value = false
  }
}

async function selectRow(row) {
  if (!row?.id) return
  loading.value = true
  try {
    const m = await MonitorsApi.get(row.id)
    form.value.id = m.id || m.Id
    form.value.name = m.name || m.Name
    form.value.targetType = m.targetType || m.TargetType || m.target?.type || 'HTTP'
    form.value.isEnabled = !!m.isEnabled
    form.value.autoDailyEnabled = m.autoDailyEnabled ?? m.AutoDailyEnabled ?? false
    form.value.autoDailyTime = m.autoDailyTime ?? m.AutoDailyTime ?? '09:00'
    form.value.maxRuns = m.maxRuns ?? m.MaxRuns ?? 0
    form.value.executedCount = m.executedCount ?? m.ExecutedCount ?? 0

    const cfgText = m.targetConfig || m.TargetConfig
    const cfg = cfgText ? safeJsonParse(cfgText) : (m.target || {})

    if (form.value.targetType === 'HTTP') {
      const t = cfg || {}
      httpConfig.value.url = t.Url ?? t.url ?? ''
      httpConfig.value.method = t.Method ?? t.method ?? 'Get'
      httpConfig.value.timeout = t.Timeout ?? t.timeout ?? 10

      const headersObj = t.Headers ?? t.headers
      httpConfig.value.headers = headersObj
        ? Object.entries(headersObj).map(([k, v]) => ({
          key: k,
          value: Array.isArray(v) ? v.join(',') : String(v ?? '')
        }))
        : [{ key: '', value: '' }]
      if (httpConfig.value.headers.length === 0) httpConfig.value.headers = [{ key: '', value: '' }]

      const queryObj = t.Query ?? t.query
      httpConfig.value.query = queryObj
        ? Object.entries(queryObj).map(([k, v]) => ({ key: k, value: String(v ?? '') }))
        : [{ key: '', value: '' }]
      if (httpConfig.value.query.length === 0) httpConfig.value.query = [{ key: '', value: '' }]

      httpConfig.value.authType = t.AuthType ?? t.authType ?? 'None'
      httpConfig.value.authToken = t.AuthToken ?? t.authToken ?? ''
      httpConfig.value.authUsername = t.AuthUsername ?? t.authUsername ?? ''
      httpConfig.value.authPassword = t.AuthPassword ?? t.authPassword ?? ''

      httpConfig.value.useCookies = t.UseCookies ?? t.useCookies ?? false
      httpConfig.value.allowAutoRedirect = t.AllowAutoRedirect ?? t.allowAutoRedirect ?? true
      httpConfig.value.maxRedirects = t.MaxRedirects ?? t.maxRedirects ?? 5
      httpConfig.value.ignoreSslErrors = t.IgnoreSslErrors ?? t.ignoreSslErrors ?? false

      httpConfig.value.proxyUrl = t.ProxyUrl ?? t.proxyUrl ?? ''
      httpConfig.value.proxyUser = t.ProxyUser ?? t.proxyUser ?? ''
      httpConfig.value.proxyPass = t.ProxyPass ?? t.proxyPass ?? ''

      httpConfig.value.enableRetry = t.EnableRetry ?? t.enableRetry ?? false
      httpConfig.value.retryCount = t.RetryCount ?? t.retryCount ?? 1
      httpConfig.value.retryDelayMs = t.RetryDelayMs ?? t.retryDelayMs ?? 200
      httpConfig.value.enableRateLimit = t.EnableRateLimit ?? t.enableRateLimit ?? false

      const body = t.Body ?? t.body
      if (body?.Type || body?.type) {
        httpConfig.value.bodyType = body.Type ?? body.type ?? 'Json'
        httpConfig.value.bodyContentType = body.ContentType ?? body.contentType ?? 'application/json'

        if (httpConfig.value.bodyType === 'FormUrlEncoded') {
          const val = body.Value ?? body.value
          if (val && typeof val === 'object') {
            httpConfig.value.formFields = Object.entries(val).map(([k, v]) => ({ key: k, value: String(v ?? '') }))
          } else {
            httpConfig.value.formFields = [{ key: '', value: '' }]
          }
          if (httpConfig.value.formFields.length === 0) httpConfig.value.formFields = [{ key: '', value: '' }]
          httpConfig.value.bodyText = ''
        } else {
          const val = body.Value ?? body.value
          httpConfig.value.bodyText = val == null ? '' : (typeof val === 'object' ? JSON.stringify(val, null, 2) : String(val))
          httpConfig.value.formFields = [{ key: '', value: '' }]
        }
      } else {
        httpConfig.value.bodyType = 'Json'
        httpConfig.value.bodyContentType = 'application/json'
        httpConfig.value.bodyText = ''
        httpConfig.value.formFields = [{ key: '', value: '' }]
      }
    } else if (form.value.targetType === 'TCP') {
      const t = cfg || {}
      tcpConfig.value.host = t.Host ?? t.host ?? '127.0.0.1'
      tcpConfig.value.port = t.Port ?? t.port ?? 80
      tcpConfig.value.timeout = t.Timeout ?? t.timeout ?? 5
      const msgs = t.Messages ?? t.messages
      if (Array.isArray(msgs)) tcpConfig.value.messagesText = msgs.join('\n')
      else tcpConfig.value.messagesText = ''
      
      tcpConfig.value.responseContains = ''
      tcpConfig.value.latencyLessThan = null
      const tcpAssertions = m.assertions || m.Assertions || []
      for (const a of tcpAssertions) {
        if ((a.type || a.Type) !== 'TCP') continue
        const cfgText = a.configJson || a.ConfigJson
        const cfg = safeJsonParse(cfgText)
        if (!cfg) continue
        const field = cfg.Field || cfg.field
        const op = cfg.Operator || cfg.operator
        const exp = cfg.Expected || cfg.expected
        
        if (field === 'Response' && op === 'Contains') {
          tcpConfig.value.responseContains = exp
        } else if (field === 'LatencyMs' && op === 'LessThan') {
          tcpConfig.value.latencyLessThan = Number(exp)
        }
      }
    } else if (form.value.targetType === 'PYTHON') {
      const t = cfg || {}
      pythonConfig.value.scriptPath = t.ScriptPath ?? t.scriptPath ?? ''
      pythonConfig.value.pythonExecutable = t.PythonExecutable ?? t.pythonExecutable ?? 'python'
      pythonConfig.value.timeoutSeconds = t.TimeoutSeconds ?? t.timeoutSeconds ?? 60
      pythonConfig.value.workingDirectory = t.WorkingDirectory ?? t.workingDirectory ?? ''
      const args = t.Args ?? t.args
      if (Array.isArray(args)) pythonConfig.value.argsText = args.map(x => String(x)).join('\n')
      else pythonConfig.value.argsText = ''
      pythonConfig.value.enableRetry = t.EnableRetry ?? t.enableRetry ?? false
      pythonConfig.value.retryCount = t.RetryCount ?? t.retryCount ?? 0
      pythonConfig.value.retryDelayMs = t.RetryDelayMs ?? t.retryDelayMs ?? 1000
      pythonConfig.value.enableRateLimit = t.EnableRateLimit ?? t.enableRateLimit ?? false
      pythonConfig.value.maxConcurrency = t.MaxConcurrency ?? t.maxConcurrency ?? 1
      const env = t.Env ?? t.env
      pythonConfig.value.env = env && typeof env === 'object'
        ? Object.entries(env).map(([k, v]) => ({ key: k, value: String(v ?? '') }))
        : [{ key: '', value: '' }]
      if (pythonConfig.value.env.length === 0) pythonConfig.value.env = [{ key: '', value: '' }]
      const codes = t.SuccessExitCodes ?? t.successExitCodes
      pythonConfig.value.successExitCodesText = Array.isArray(codes) ? codes.join(',') : '0'
      pythonConfig.value.stdOutContains = ''
      pythonConfig.value.stdErrContains = ''
    }
    lastSummary.value = null
  } catch (e) {
    ElMessage.error(e.message || String(e))
  } finally {
    loading.value = false
  }
}

function buildDto() {
  const name = String(form.value.name || '').trim()
  if (!name) throw new Error('名称不能为空')

  let targetConfigObj = {}
  if (form.value.targetType === 'HTTP') {
    const url = String(httpConfig.value.url || '').trim()
    if (!url) throw new Error('Url 不能为空')

    const headers = {}
    for (const item of httpConfig.value.headers || []) {
      const k = String(item.key || '').trim()
      if (!k) continue
      const values = String(item.value || '')
        .split(',')
        .map(x => x.trim())
        .filter(Boolean)
      if (values.length === 0) continue
      headers[k] = values
    }

    const query = {}
    for (const item of httpConfig.value.query || []) {
      const k = String(item.key || '').trim()
      if (!k) continue
      query[k] = String(item.value ?? '')
    }

    let body = null
    const bodyType = String(httpConfig.value.bodyType || 'Json')
    const bodyContentType = String(httpConfig.value.bodyContentType || '').trim()

    if (bodyType === 'FormUrlEncoded') {
      const dict = {}
      for (const item of httpConfig.value.formFields || []) {
        const k = String(item.key || '').trim()
        if (!k) continue
        dict[k] = String(item.value ?? '')
      }
      if (Object.keys(dict).length > 0) {
        body = { Type: 'FormUrlEncoded', ContentType: bodyContentType || 'application/x-www-form-urlencoded', Value: dict }
      }
    } else if (bodyType === 'Raw') {
      const raw = String(httpConfig.value.bodyText || '')
      if (raw.trim()) body = { Type: 'Raw', ContentType: bodyContentType || 'text/plain', Value: raw }
    } else {
      const raw = String(httpConfig.value.bodyText || '')
      if (raw.trim()) {
        let parsed = raw
        try {
          parsed = JSON.parse(raw)
        } catch {
        }
        body = { Type: 'Json', ContentType: bodyContentType || 'application/json', Value: parsed }
      }
    }

    targetConfigObj = {
      ...httpTargetTemplate(),
      Url: url,
      Method: String(httpConfig.value.method || 'Get'),
      Timeout: Number(httpConfig.value.timeout || 10),
      Headers: headers,
      Query: query,
      AuthType: String(httpConfig.value.authType || 'None'),
      AuthToken: String(httpConfig.value.authToken || ''),
      AuthUsername: String(httpConfig.value.authUsername || ''),
      AuthPassword: String(httpConfig.value.authPassword || ''),
      UseCookies: !!httpConfig.value.useCookies,
      AllowAutoRedirect: !!httpConfig.value.allowAutoRedirect,
      MaxRedirects: Number(httpConfig.value.maxRedirects || 5),
      IgnoreSslErrors: !!httpConfig.value.ignoreSslErrors,
      ProxyUrl: String(httpConfig.value.proxyUrl || ''),
      ProxyUser: String(httpConfig.value.proxyUser || ''),
      ProxyPass: String(httpConfig.value.proxyPass || ''),
      EnableRetry: !!httpConfig.value.enableRetry,
      RetryCount: Number(httpConfig.value.retryCount || 1),
      RetryDelayMs: Number(httpConfig.value.retryDelayMs || 200),
      EnableRateLimit: !!httpConfig.value.enableRateLimit,
      Body: body
    }
  } else if (form.value.targetType === 'TCP') {
    const host = String(tcpConfig.value.host || '').trim()
    if (!host) throw new Error('Host 不能为空')

    const messages = String(tcpConfig.value.messagesText || '')
      .split('\n')
      .map(x => x.trim())
      .filter(Boolean)

    targetConfigObj = {
      ...tcpTargetTemplate(),
      Host: host,
      Port: Number(tcpConfig.value.port || 80),
      Timeout: Number(tcpConfig.value.timeout || 5),
      Messages: messages
    }
  } else if (form.value.targetType === 'PYTHON') {
    const scriptPath = String(pythonConfig.value.scriptPath || '').trim()
    if (!scriptPath) throw new Error('ScriptPath 不能为空')

    const args = String(pythonConfig.value.argsText || '')
      .split('\n')
      .map(x => x.trim())
      .filter(Boolean)

    const envDict = {}
    for (const item of pythonConfig.value.env || []) {
      const k = String(item.key || '').trim()
      if (!k) continue
      envDict[k] = String(item.value ?? '')
    }

    const codes = String(pythonConfig.value.successExitCodesText || '')
      .split(',')
      .map(x => x.trim())
      .filter(Boolean)
      .map(x => Number.parseInt(x, 10))
      .filter(x => Number.isFinite(x))

    targetConfigObj = {
      ...pythonTargetTemplate(),
      ScriptPath: scriptPath,
      Args: args,
      WorkingDirectory: String(pythonConfig.value.workingDirectory || '').trim() || null,
      PythonExecutable: String(pythonConfig.value.pythonExecutable || 'python').trim() || 'python',
      TimeoutSeconds: Number(pythonConfig.value.timeoutSeconds || 60),
      EnableRetry: !!pythonConfig.value.enableRetry,
      RetryCount: Number(pythonConfig.value.retryCount || 0),
      RetryDelayMs: Number(pythonConfig.value.retryDelayMs || 1000),
      EnableRateLimit: !!pythonConfig.value.enableRateLimit,
      MaxConcurrency: Number(pythonConfig.value.maxConcurrency || 1),
      Env: Object.keys(envDict).length ? envDict : null,
      SuccessExitCodes: codes.length ? codes : [0]
    }
  } else {
    throw new Error('不支持的目标类型')
  }

  const targetConfigText = JSON.stringify(targetConfigObj)

  const assertions = []
  const expected = String(form.value.assertExpected || '').trim()
  if (expected && form.value.targetType === 'HTTP') {
    const id = crypto.randomUUID()
    assertions.push({
      Id: id,
      Type: 'HTTP',
      ConfigJson: JSON.stringify({
        Id: id,
        Field: 'StatusCode',
        HeaderKey: '',
        Expected: expected,
        Operator: 'Equal'
      })
    })
  }
  if (form.value.targetType === 'TCP') {
    const responseContains = String(tcpConfig.value.responseContains || '').trim()
    if (responseContains) {
      const id = crypto.randomUUID()
      assertions.push({
        Id: id,
        Type: 'TCP',
        ConfigJson: JSON.stringify({
          Id: id,
          Field: 'Response',
          Operator: 'Contains',
          Expected: responseContains
        })
      })
    }
    const latencyLessThan = tcpConfig.value.latencyLessThan
    if (latencyLessThan !== null && latencyLessThan !== undefined && latencyLessThan !== '') {
      const id = crypto.randomUUID()
      assertions.push({
        Id: id,
        Type: 'TCP',
        ConfigJson: JSON.stringify({
          Id: id,
          Field: 'LatencyMs',
          Operator: 'LessThan',
          Expected: String(latencyLessThan)
        })
      })
    }
  }
  if (form.value.targetType === 'PYTHON') {
    const outContains = String(pythonConfig.value.stdOutContains || '').trim()
    if (outContains) {
      const id = crypto.randomUUID()
      assertions.push({
        Id: id,
        Type: 'PYTHON',
        ConfigJson: JSON.stringify({
          Id: id,
          Field: 'StdOut',
          Operator: 'Contains',
          Expected: outContains
        })
      })
    }
    const errContains = String(pythonConfig.value.stdErrContains || '').trim()
    if (errContains) {
      const id = crypto.randomUUID()
      assertions.push({
        Id: id,
        Type: 'PYTHON',
        ConfigJson: JSON.stringify({
          Id: id,
          Field: 'StdErr',
          Operator: 'Contains',
          Expected: errContains
        })
      })
    }
  }

  return {
    Name: name,
    TargetType: form.value.targetType,
    TargetConfig: targetConfigText,
    IsEnabled: !!form.value.isEnabled,
    Assertions: assertions,
    AutoDailyEnabled: !!form.value.autoDailyEnabled,
    AutoDailyTime: form.value.autoDailyEnabled ? String(form.value.autoDailyTime || '').trim() : null,
    MaxRuns: form.value.autoDailyEnabled ? (Number(form.value.maxRuns || 0) > 0 ? Number(form.value.maxRuns) : null) : null,
    ExecutedCount: Number(form.value.executedCount || 0)
  }
}

async function save() {
  saving.value = true
  try {
    const dto = buildDto()
    if (!form.value.id) {
      const id = await MonitorsApi.create(dto)
      form.value.id = String(id)
      ElMessage.success('创建成功')
    } else {
      await MonitorsApi.update(form.value.id, dto)
      ElMessage.success('更新成功')
    }
    await refresh()
  } catch (e) {
    ElMessage.error(e.message || String(e))
  } finally {
    saving.value = false
  }
}

async function remove() {
  if (!form.value.id) return
  try {
    await ElMessageBox.confirm('确认删除该任务？', '提示', { type: 'warning' })
  } catch {
    return
  }
  saving.value = true
  try {
    await MonitorsApi.remove(form.value.id)
    ElMessage.success('已删除')
    clearForm()
    await refresh()
  } catch (e) {
    ElMessage.error(e.message || String(e))
  } finally {
    saving.value = false
  }
}

async function toggleEnabled(row, isEnabled) {
  if (!row?.id) return
  row.__toggling = true
  try {
    await MonitorsApi.setEnabled(row.id, isEnabled)
    row.isEnabled = !!isEnabled
    if (form.value.id && String(form.value.id) === String(row.id)) {
      form.value.isEnabled = !!isEnabled
    }
    ElMessage.success(isEnabled ? '已恢复' : '已暂停')
  } catch (e) {
    ElMessage.error(e.message || String(e))
    await refresh()
  } finally {
    row.__toggling = false
  }
}

function openCreate() {
  clearForm()
}

function safeJsonParse(s) {
  try {
    return JSON.parse(String(s || ''))
  } catch {
    return null
  }
}

async function loadLastSummary() {
  if (!form.value.id) return
  running.value = true
  try {
    const res = await MonitorsApi.latestExecution(form.value.id)
    const record = res?.record
    const payload = safeJsonParse(record?.resultJson)
    lastSummary.value = {
      startedAt: record?.startedAt,
      finishedAt: record?.finishedAt,
      isExecutionSuccess: !!record?.isExecutionSuccess,
      errorMessage: record?.errorMessage || payload?.ErrorMessage || '',
      statusCode: payload?.StatusCode ?? payload?.statusCode,
      elapsedMs: payload?.ElapsedMilliseconds ?? payload?.elapsedMilliseconds ?? payload?.ElapsedMs ?? payload?.elapsedMs,
      connected: payload?.Connected ?? payload?.connected,
      response: payload?.Response ?? payload?.response,
      exitCode: payload?.ExitCode ?? payload?.exitCode,
      timedOut: !!(payload?.TimedOut ?? payload?.timedOut)
    }
  } catch (e) {
    ElMessage.error(e.message || String(e))
  } finally {
    running.value = false
  }
}

async function runOnce(row) {
  const id = row?.id || form.value.id
  if (!id) return
  if (isRunningStatus(row?.status ?? currentMonitorStatus.value)) {
    ElMessage.warning('任务正在运行中')
    return
  }
  running.value = true
  try {
    await MonitorsApi.run(id)
    setRowStatus(id, 1)
    ElMessage.success('已触发执行')
  } catch (e) {
    ElMessage.error(e.message || String(e))
  } finally {
    running.value = false
  }
}

let hubConn = null
function onMonitorUpdated(payload) {
  const mid = payload?.monitorId
  if (!mid) return

  if (payload.status === 'running') {
    setRowStatus(mid, 1)
    if (form.value.id && String(form.value.id) === String(mid)) ElMessage.info('开始执行…')
    return
  }

  if (payload.status === 'finished') {
    const recordStatus = payload?.record?.status ?? payload?.record?.Status
    if (recordStatus != null) setRowStatus(mid, recordStatus)
    if (form.value.id && String(form.value.id) === String(mid)) {
      loadLastSummary()
      ElMessage.success('执行完成')
    }
    return
  }
}

onMounted(async () => {
  clearForm()
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

.execute-bar {
  display: flex;
  gap: 12px;
}

.summary {
  margin-top: 14px;
}

.muted {
  color: #909399;
}

.kv-grid {
  width: 100%;
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.kv-item {
  background: #f8f9fa;
  border: 1px solid #ebeef5;
  border-radius: 8px;
  padding: 12px;
}

.kv-span2 {
  grid-column: span 2;
}

.kv-label {
  font-size: 12px;
  color: #909399;
  margin-bottom: 8px;
}

.kv-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.kv-row {
  display: grid;
  grid-template-columns: 1fr 1fr auto;
  gap: 10px;
  align-items: center;
}

.code-editor :deep(textarea) {
  font-family: Consolas, Monaco, monospace;
  font-size: 13px;
  background-color: #f8f9fa;
  color: #333;
}
</style>
