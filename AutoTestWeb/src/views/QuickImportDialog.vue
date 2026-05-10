<!--
  QuickImportDialog.vue — 一键导入弹窗
  从使用者视角设计：选场景 → 看说明 → 填参数 → 看到会创建什么 → 一键创建
-->
<script setup>
import { ref, computed, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { Warning } from '@element-plus/icons-vue'
import { presets } from './presets'

const props = defineProps({
  visible: { type: Boolean, default: false },
  presetKey: { type: String, default: '' }
})
const emit = defineEmits(['close', 'done'])

const importing = ref(false)
const runAfterImport = ref(true)

const preset = computed(() => presets[props.presetKey] || null)

// 初始化参数值（用 defaultValue）
const params = ref({})
watch(() => props.presetKey, (key) => {
  const p = presets[key]
  if (!p) { params.value = {}; return }
  const init = {}
  for (const f of p.fields) {
    init[f.key] = f.defaultValue || ''
  }
  params.value = init
  runAfterImport.value = true
}, { immediate: true })

const summary = computed(() => {
  const p = preset.value
  if (!p) return ''
  try { return p.summary(params.value) } catch { return '' }
})

const typeTagType = { HTTP: 'success', TCP: 'warning', DB: '', PYTHON: 'danger', TEMPLATE: 'primary' }
const typeNames = { HTTP: 'HTTP 接口', TCP: 'TCP 端口', DB: '数据库查询', PYTHON: 'Python 脚本', TEMPLATE: '模板流程' }

async function doImport() {
  const p = preset.value
  if (!p) return

  // 校验必填
  for (const f of p.fields) {
    if (!params.value[f.key]?.trim()) {
      ElMessage.warning(`请填写「${f.label}」`)
      return
    }
  }

  importing.value = true
  try {
    const dto = p.build(params.value)
    const { MonitorsApi } = await import('../api/monitors')
    const result = await MonitorsApi.create(dto)

    // 导入后立即执行一次
    if (runAfterImport.value && result?.id) {
      try { await MonitorsApi.run(result.id) } catch { /* 执行失败不影响导入结果 */ }
    }

    ElMessage.success(`已创建: ${dto.Name}` + (runAfterImport.value ? '，并触发首次执行' : ''))
    emit('done')
  } catch (e) {
    ElMessage.error('导入失败: ' + (e.message || e))
  } finally {
    importing.value = false
  }
}

function handleClose() {
  if (!importing.value) emit('close')
}
</script>

<template>
  <el-dialog
    :model-value="visible"
    :title="null"
    width="520px"
    :close-on-click-modal="false"
    :close-on-press-escape="!importing"
    @close="handleClose"
    destroy-on-close
  >
    <!-- 头部说明 -->
    <div v-if="preset" class="qd-head">
      <h3 class="qd-title">{{ preset.label }}</h3>
      <el-tag size="small" :type="typeTagType[preset.type]">{{ typeNames[preset.type] || preset.type }}</el-tag>
      <p class="qd-desc">{{ preset.description }}</p>
    </div>

    <!-- 参数表单 -->
    <div v-if="preset" class="qd-form">
      <div v-for="f in preset.fields" :key="f.key" class="qd-field">
        <label class="qd-label">{{ f.label }}</label>
        <el-input
          v-model="params[f.key]"
          :placeholder="f.placeholder"
          clearable
        />
      </div>
    </div>

    <!-- 摘要 — 告诉用户会创建怎样的监控 -->
    <div v-if="summary" class="qd-summary">
      <el-icon><Warning /></el-icon>
      <span>{{ summary }}</span>
    </div>

    <!-- 选项 -->
    <div v-if="preset" class="qd-options">
      <el-checkbox v-model="runAfterImport">导入后立即执行一次，验证能否跑通</el-checkbox>
    </div>

    <!-- 底部按钮 -->
    <template #footer>
      <el-button @click="handleClose" :disabled="importing">取消</el-button>
      <el-button type="primary" @click="doImport" :loading="importing">
        {{ importing ? '导入中...' : '一键导入' }}
      </el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.qd-head { margin-bottom: 20px; }
.qd-title { display: inline; margin: 0 8px 0 0; font-size: 18px; font-weight: 600; vertical-align: middle; }
.qd-desc { margin: 10px 0 0; color: #606266; line-height: 1.6; font-size: 14px; }

.qd-form { margin-bottom: 16px; }
.qd-field { margin-bottom: 14px; }
.qd-label { display: block; margin-bottom: 6px; font-size: 13px; color: #303133; font-weight: 500; }

.qd-summary {
  display: flex; align-items: flex-start; gap: 8px;
  background: #f0f5ff; border: 1px solid #c6d8ff; border-radius: 6px;
  padding: 10px 14px; font-size: 13px; color: #1d4ed8; line-height: 1.5;
}
.qd-summary .el-icon { margin-top: 2px; flex-shrink: 0; }

.qd-options { margin-top: 14px; }
</style>
