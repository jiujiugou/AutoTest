<script setup>
import { reactive, ref } from 'vue'

const props = defineProps({ config: Object })
const c = reactive(props.config)
const fileInput = ref(null)

function isValidJson(str) { try { JSON.parse(str); return true } catch { return false } }

function reset() { Object.assign(c, { dslJson: '', variablesJson: '' }) }

function validate() {
  const dsl = (c.dslJson || '').trim()
  if (!dsl) return 'DSL 定义不能为空'
  if (!isValidJson(dsl)) return 'DSL 定义不是有效的 JSON 格式'
  const vars = (c.variablesJson || '').trim()
  if (vars && !isValidJson(vars)) return '模板变量不是有效的 JSON 格式'
  return null
}

function buildTargetConfig() { return { __raw: (c.dslJson || '').trim() } }

function buildTargetConfigText() { return (c.dslJson || '').trim() }

function buildAssertions() { return [] }

function loadFrom(cfgText, monitor) {
  c.dslJson = cfgText || ''
  c.variablesJson = monitor.templateVariablesJson || monitor.TemplateVariablesJson || ''
}

function handleFileImport(e) {
  const file = e?.target?.files?.[0]
  if (!file) return
  const reader = new FileReader()
  reader.onload = (ev) => {
    const content = ev.target?.result
    if (typeof content !== 'string') return
    if (!isValidJson(content)) return
    c.dslJson = content
    const ph = new Set()
    const regex = /\{\{(\w+)\}\}/g
    let m; while ((m = regex.exec(content)) !== null) ph.add(m[1])
    if (ph.size > 0) c.variablesJson = JSON.stringify(Object.fromEntries([...ph].map(k => [k, ''])), null, 2)
  }
  reader.readAsText(file)
}

function chooseFile() { fileInput.value?.click() }
function clearFile() { c.dslJson = ''; c.variablesJson = '' }

defineExpose({ validate, buildTargetConfig, buildTargetConfigText, buildAssertions, loadFrom, reset, chooseFile, clearFile })
</script>

<template>
  <div style="width: 100%;">
    <div class="file-row" style="margin-bottom: 12px;">
      <input ref="fileInput" class="hidden-file" type="file" accept=".json" @change="handleFileImport" />
      <el-button size="small" type="primary" plain @click="chooseFile">导入JSON文件</el-button>
      <el-button v-if="c.dslJson" size="small" type="danger" plain @click="clearFile">清除</el-button>
    </div>
    <div class="kv-label">DSL 定义（JSON）</div>
    <el-input v-model="c.dslJson" type="textarea" :rows="14" placeholder="导入 JSON 文件或在此编辑 DSL 定义" class="code-input" />
    <div class="kv-label" style="margin-top: 12px;">模板变量（JSON 键值对，可选）</div>
    <el-input v-model="c.variablesJson" type="textarea" :rows="4" placeholder='{"host": "https://example.com"}' class="code-input" style="margin-top: 4px;" />
  </div>
</template>
