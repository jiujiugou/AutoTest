<script setup>
import { reactive, ref } from 'vue'

const props = defineProps({ config: Object })
const c = reactive(props.config)
const fileInput = ref(null)

function addKv(arr) { arr.push({ key: '', value: '' }) }
function removeKv(arr, i) { if (arr.length > 1) arr.splice(i, 1) }

function reset() {
  Object.assign(c, { scriptPath: '', scriptContent: '', scriptFileName: '', pythonExecutable: 'python', timeoutSeconds: 60, workingDirectory: '', argsText: '', enableRetry: false, retryCount: 0, retryDelayMs: 1000, enableRateLimit: false, maxConcurrency: 1, env: [{ key: '', value: '' }], successExitCodesText: '0', stdOutContains: '', stdErrContains: '' })
}

function validate() {
  if (!(c.scriptPath || '').trim() && !(c.scriptContent || '').trim()) return 'ScriptPath 不能为空'
  return null
}

function buildTargetConfig() {
  const env = {}
  for (const item of c.env || []) { const k = (item.key || '').trim(); if (k) env[k] = String(item.value ?? '') }
  const codes = (c.successExitCodesText || '').split(',').map(x => +x.trim()).filter(x => Number.isFinite(x))
  return {
    ScriptPath: (c.scriptPath || '').trim(),
    ScriptContent: (c.scriptContent || '').trim() || null,
    Args: (c.argsText || '').split('\n').map(x => x.trim()).filter(Boolean),
    WorkingDirectory: (c.workingDirectory || '').trim() || null,
    PythonExecutable: (c.pythonExecutable || 'python').trim(),
    TimeoutSeconds: Number(c.timeoutSeconds || 60),
    EnableRetry: !!c.enableRetry,
    RetryCount: Number(c.retryCount || 0),
    RetryDelayMs: Number(c.retryDelayMs || 1000),
    EnableRateLimit: !!c.enableRateLimit,
    MaxConcurrency: Number(c.maxConcurrency || 1),
    Env: Object.keys(env).length ? env : null,
    SuccessExitCodes: codes.length ? codes : [0]
  }
}

function buildAssertions() {
  const arr = []
  const out = (c.stdOutContains || '').trim()
  if (out) arr.push({ Field: 'StdOut', Operator: 'Contains', Expected: out })
  const err = (c.stdErrContains || '').trim()
  if (err) arr.push({ Field: 'StdErr', Operator: 'Contains', Expected: err })
  return arr
}

function safeJsonParse(s) { try { return JSON.parse(s) } catch { return null } }

function loadFrom(cfg, monitor) {
  c.scriptPath = cfg.ScriptPath ?? cfg.scriptPath ?? ''
  c.scriptContent = cfg.ScriptContent ?? cfg.scriptContent ?? ''
  c.scriptFileName = ''
  c.pythonExecutable = cfg.PythonExecutable ?? cfg.pythonExecutable ?? 'python'
  c.timeoutSeconds = cfg.TimeoutSeconds ?? cfg.timeoutSeconds ?? 60
  c.workingDirectory = cfg.WorkingDirectory ?? cfg.workingDirectory ?? ''
  c.argsText = Array.isArray(cfg.Args ?? cfg.args) ? (cfg.Args ?? cfg.args).join('\n') : ''
  c.enableRetry = !!(cfg.EnableRetry ?? cfg.enableRetry)
  c.retryCount = cfg.RetryCount ?? cfg.retryCount ?? 0
  c.retryDelayMs = cfg.RetryDelayMs ?? cfg.retryDelayMs ?? 1000
  c.enableRateLimit = !!(cfg.EnableRateLimit ?? cfg.enableRateLimit)
  c.maxConcurrency = cfg.MaxConcurrency ?? cfg.maxConcurrency ?? 1
  c.successExitCodesText = Array.isArray(cfg.SuccessExitCodes) ? cfg.SuccessExitCodes.join(',') : '0'
  c.stdOutContains = ''
  c.stdErrContains = ''
  if (monitor) {
    const assertions = monitor.assertions || monitor.Assertions || []
    for (const a of assertions) {
      if ((a.type || a.Type) !== 'PYTHON') continue
      const ac = safeJsonParse(a.configJson || a.ConfigJson)
      if (!ac) continue
      if ((ac.Field || ac.field) === 'StdOut' && (ac.Operator || ac.operator) === 'Contains') c.stdOutContains = ac.Expected ?? ac.expected ?? ''
      else if ((ac.Field || ac.field) === 'StdErr' && (ac.Operator || ac.operator) === 'Contains') c.stdErrContains = ac.Expected ?? ac.expected ?? ''
    }
  }
  const envObj = cfg.Env ?? cfg.env
  c.env = envObj ? Object.entries(envObj).map(([k, v]) => ({ key: k, value: String(v) })) : [{ key: '', value: '' }]
}

function handleFile(e) {
  const file = e?.target?.files?.[0]; if (!file) return
  const reader = new FileReader()
  reader.onload = (ev) => { c.scriptContent = ev.target?.result || ''; c.scriptFileName = file.name; c.scriptPath = file.name }
  reader.readAsText(file)
}

function chooseFile() { fileInput.value?.click() }

defineExpose({ validate, buildTargetConfig, buildAssertions, loadFrom, reset, chooseFile })
</script>

<template>
  <div class="kv-grid">
    <div class="kv-item kv-span2"><div class="kv-label">ScriptPath</div><el-input v-model="c.scriptPath" placeholder="例如：scripts/check.py" /></div>
    <div class="kv-item kv-span2">
      <div class="kv-label">选择脚本文件</div>
      <div class="file-row">
        <input ref="fileInput" class="hidden-file" type="file" accept=".py,text/x-python" @change="handleFile" />
        <el-button size="small" type="primary" plain @click="chooseFile">选择文件</el-button>
        <span class="muted" v-if="c.scriptFileName">{{ c.scriptFileName }}</span>
        <el-button v-if="c.scriptContent" size="small" type="danger" plain @click="c.scriptContent='';c.scriptFileName=''">清除</el-button>
      </div>
    </div>
    <div class="kv-item"><div class="kv-label">PythonExecutable</div><el-input v-model="c.pythonExecutable" placeholder="python" /></div>
    <div class="kv-item"><div class="kv-label">Timeout(s)</div><el-input-number v-model="c.timeoutSeconds" :min="1" :max="3600" :step="1" style="width:100%" /></div>
    <div class="kv-item kv-span2"><div class="kv-label">WorkingDirectory</div><el-input v-model="c.workingDirectory" placeholder="D:\\work" /></div>
    <div class="kv-item kv-span2"><div class="kv-label">Args（每行一个）</div><el-input v-model="c.argsText" type="textarea" :rows="5" placeholder="--env\nprod" /></div>
    <div class="kv-item"><div class="kv-label">EnableRetry</div><el-switch v-model="c.enableRetry" /></div>
    <div class="kv-item"><div class="kv-label">RetryCount</div><el-input-number v-model="c.retryCount" :min="0" :max="20" :step="1" style="width:100%" :disabled="!c.enableRetry" /></div>
    <div class="kv-item"><div class="kv-label">RetryDelayMs</div><el-input-number v-model="c.retryDelayMs" :min="0" :max="60000" :step="100" style="width:100%" :disabled="!c.enableRetry" /></div>
    <div class="kv-item"><div class="kv-label">EnableRateLimit</div><el-switch v-model="c.enableRateLimit" /></div>
    <div class="kv-item"><div class="kv-label">MaxConcurrency</div><el-input-number v-model="c.maxConcurrency" :min="1" :max="50" :step="1" style="width:100%" :disabled="!c.enableRateLimit" /></div>
    <div class="kv-item"><div class="kv-label">SuccessExitCodes</div><el-input v-model="c.successExitCodesText" placeholder="0,2" /></div>
    <div class="kv-item kv-span2"><div class="kv-label">StdOut Contains</div><el-input v-model="c.stdOutContains" placeholder="OK" /></div>
    <div class="kv-item kv-span2"><div class="kv-label">StdErr Contains</div><el-input v-model="c.stdErrContains" placeholder="Traceback" /></div>
  </div>
  <div class="kv-list" style="margin-top:10px">
    <div class="kv-label">Env（可选）</div>
    <div v-for="(h,idx) in c.env" :key="idx" class="kv-row">
      <el-input v-model="h.key" placeholder="KEY" /><el-input v-model="h.value" placeholder="VALUE" />
      <el-button type="danger" :icon="'Delete'" plain @click="removeKv(c.env,idx)" />
    </div>
    <el-button type="primary" plain :icon="'Plus'" @click="addKv(c.env)">新增 Env</el-button>
  </div>
</template>
