<script setup>
import { reactive, computed } from 'vue'

const props = defineProps({ config: Object })
const emit = defineEmits(['update:config'])

const c = reactive(props.config)

function reset() {
  Object.assign(c, { host: '127.0.0.1', port: 80, timeout: 5, useTls: false, ignoreSslErrors: false, connectTimeoutMs: 15000, readTimeoutMs: 30000, writeTimeoutMs: 10000, enableRetry: false, retryCount: 2, retryDelayMs: 500, messagesText: '', responseContains: '', latencyLessThan: null })
}

function validate() {
  if (!c.host?.trim()) return 'Host 不能为空'
  return null
}

function buildTargetConfig() {
  return {
    Host: c.host?.trim() || '127.0.0.1',
    Port: Number(c.port || 80),
    Timeout: Number(c.timeout || 5),
    Messages: (c.messagesText || '').split('\n').map(x => x.trim()).filter(Boolean),
    UseTls: !!c.useTls,
    IgnoreSslErrors: !!c.ignoreSslErrors,
    ConnectTimeoutMs: Number(c.connectTimeoutMs || 0),
    ReadTimeoutMs: Number(c.readTimeoutMs || 0),
    WriteTimeoutMs: Number(c.writeTimeoutMs || 0),
    EnableRetry: !!c.enableRetry,
    RetryCount: Number(c.retryCount || 2),
    RetryDelayMs: Number(c.retryDelayMs || 500)
  }
}

function buildAssertions() {
  const arr = []
  const resp = (c.responseContains || '').trim()
  if (resp) arr.push({ Field: 'Response', Operator: 'Contains', Expected: resp })
  if (c.latencyLessThan != null && c.latencyLessThan !== '') arr.push({ Field: 'LatencyMs', Operator: 'LessThan', Expected: String(c.latencyLessThan) })
  return arr
}

function loadFrom(cfg, monitor) {
  c.host = cfg.Host ?? cfg.host ?? '127.0.0.1'
  c.port = cfg.Port ?? cfg.port ?? 80
  c.timeout = cfg.Timeout ?? cfg.timeout ?? 5
  c.useTls = !!(cfg.UseTls ?? cfg.useTls)
  c.ignoreSslErrors = !!(cfg.IgnoreSslErrors ?? cfg.ignoreSslErrors)
  c.connectTimeoutMs = cfg.ConnectTimeoutMs ?? cfg.connectTimeoutMs ?? 15000
  c.readTimeoutMs = cfg.ReadTimeoutMs ?? cfg.readTimeoutMs ?? 30000
  c.writeTimeoutMs = cfg.WriteTimeoutMs ?? cfg.writeTimeoutMs ?? 10000
  c.enableRetry = !!(cfg.EnableRetry ?? cfg.enableRetry)
  c.retryCount = cfg.RetryCount ?? cfg.retryCount ?? 2
  c.retryDelayMs = cfg.RetryDelayMs ?? cfg.retryDelayMs ?? 500
  c.messagesText = Array.isArray(cfg.Messages ?? cfg.messages) ? (cfg.Messages ?? cfg.messages).join('\n') : ''
  c.responseContains = ''
  c.latencyLessThan = null
  const assertions = monitor.assertions || monitor.Assertions || []
  for (const a of assertions) {
    if ((a.type || a.Type) !== 'TCP') continue
    const ac = safeJsonParse(a.configJson || a.ConfigJson)
    if (!ac) continue
    if ((ac.Field || ac.field) === 'Response' && (ac.Operator || ac.operator) === 'Contains') c.responseContains = ac.Expected ?? ac.expected
    else if ((ac.Field || ac.field) === 'LatencyMs' && (ac.Operator || ac.operator) === 'LessThan') c.latencyLessThan = Number(ac.Expected ?? ac.expected)
  }
}

function safeJsonParse(s) { try { return JSON.parse(s) } catch { return null } }

defineExpose({ validate, buildTargetConfig, buildAssertions, loadFrom, reset })
</script>

<template>
  <div class="kv-grid">
    <div class="kv-item">
      <div class="kv-label">Host</div>
      <el-input v-model="c.host" placeholder="127.0.0.1" />
    </div>
    <div class="kv-item">
      <div class="kv-label">Port</div>
      <el-input-number v-model="c.port" :min="1" :max="65535" :step="1" style="width: 100%" />
    </div>
    <div class="kv-item">
      <div class="kv-label">Timeout(s)</div>
      <el-input-number v-model="c.timeout" :min="1" :max="300" :step="1" style="width: 100%" />
    </div>
    <div class="kv-item">
      <div class="kv-label">UseTls</div>
      <el-switch v-model="c.useTls" />
    </div>
    <div class="kv-item" v-if="c.useTls">
      <div class="kv-label">IgnoreSslErrors</div>
      <el-switch v-model="c.ignoreSslErrors" />
    </div>
    <div class="kv-item kv-span2">
      <div class="kv-label">Messages（每行一条，可选）</div>
      <el-input v-model="c.messagesText" type="textarea" :rows="6" placeholder="例如：PING" />
    </div>
    <div class="kv-item kv-span2">
      <div class="kv-label">响应包含（可选）</div>
      <el-input v-model="c.responseContains" placeholder="例如：PONG" />
    </div>
    <div class="kv-item">
      <div class="kv-label">延迟要求小于(ms)（可选）</div>
      <el-input-number v-model="c.latencyLessThan" :min="0" :step="100" style="width: 100%" placeholder="例如：500" />
    </div>
  </div>
  <el-collapse style="margin-top: 12px;">
    <el-collapse-item title="高级选项" name="adv">
      <div class="kv-grid" style="margin-top: 8px;">
        <div class="kv-item"><div class="kv-label">ConnectTimeout(ms)</div><el-input-number v-model="c.connectTimeoutMs" :min="0" :max="60000" :step="1000" style="width: 100%" /></div>
        <div class="kv-item"><div class="kv-label">ReadTimeout(ms)</div><el-input-number v-model="c.readTimeoutMs" :min="0" :max="120000" :step="1000" style="width: 100%" /></div>
        <div class="kv-item"><div class="kv-label">WriteTimeout(ms)</div><el-input-number v-model="c.writeTimeoutMs" :min="0" :max="60000" :step="1000" style="width: 100%" /></div>
        <div class="kv-item"><div class="kv-label">EnableRetry</div><el-switch v-model="c.enableRetry" /></div>
        <template v-if="c.enableRetry">
          <div class="kv-item"><div class="kv-label">RetryCount</div><el-input-number v-model="c.retryCount" :min="1" :max="10" :step="1" style="width: 100%" /></div>
          <div class="kv-item"><div class="kv-label">RetryDelay(ms)</div><el-input-number v-model="c.retryDelayMs" :min="100" :max="30000" :step="500" style="width: 100%" /></div>
        </template>
      </div>
    </el-collapse-item>
  </el-collapse>
</template>
