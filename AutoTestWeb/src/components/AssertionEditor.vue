<script setup>
import { computed } from 'vue'
import { Plus, Delete } from '@element-plus/icons-vue'

const props = defineProps({
  targetType: { type: String, required: true },
  modelValue: { type: Array, default: () => [] }
})

const emit = defineEmits(['update:modelValue'])

const fieldOptions = {
  HTTP: ['StatusCode', 'Body', 'ResponseTime', 'Header'],
  TCP: ['Connected', 'Response', 'LatencyMs', 'SequenceCorrect'],
  DB: ['RowCount', 'Value', 'ExecutionTime'],
  PYTHON: ['ExitCode', 'StdOut', 'StdErr', 'ElapsedMs']
}

const operatorOptions = ['Equal', 'NotEqual', 'Contains', 'LessThan', 'GreaterThan', 'LessThanOrEqual', 'GreaterThanOrEqual']

const supportedFields = computed(() => fieldOptions[props.targetType] || [])

function add() {
  const next = [...props.modelValue, { id: '', field: supportedFields.value[0] || '', operator: 'Equal', expected: '', headerKey: '' }]
  emit('update:modelValue', next)
}

function remove(index) {
  const next = [...props.modelValue]
  next.splice(index, 1)
  emit('update:modelValue', next)
}

function update(index, key, value) {
  const next = props.modelValue.map((a, i) => i === index ? { ...a, [key]: value } : a)
  emit('update:modelValue', next)
}
</script>

<template>
  <div class="assertion-editor">
    <div class="assertion-head">
      <span class="assertion-label">断言规则</span>
      <el-button size="small" type="primary" :icon="Plus" @click="add" plain>添加断言</el-button>
    </div>
    <div v-if="modelValue.length === 0" class="assertion-empty">暂无断言规则</div>
    <div v-for="(a, i) in modelValue" :key="i" class="assertion-row">
      <el-select :model-value="a.field" @update:model-value="v => update(i, 'field', v)" placeholder="字段" style="width:140px" size="small">
        <el-option v-for="f in supportedFields" :key="f" :label="f" :value="f" />
      </el-select>
      <el-select :model-value="a.operator" @update:model-value="v => update(i, 'operator', v)" placeholder="操作符" style="width:150px" size="small">
        <el-option v-for="o in operatorOptions" :key="o" :label="o" :value="o" />
      </el-select>
      <el-input
        :model-value="a.expected"
        @update:model-value="v => update(i, 'expected', v)"
        placeholder="期望值"
        style="width:180px"
        size="small"
      />
      <el-input
        v-if="a.field === 'Header'"
        :model-value="a.headerKey"
        @update:model-value="v => update(i, 'headerKey', v)"
        placeholder="Header Key"
        style="width:150px"
        size="small"
      />
      <el-button size="small" type="danger" :icon="Delete" @click="remove(i)" circle plain />
    </div>
  </div>
</template>

<style scoped>
.assertion-editor { display: flex; flex-direction: column; gap: 8px; }
.assertion-head { display: flex; align-items: center; gap: 12px; }
.assertion-label { font-size: 13px; color: #606266; font-weight: 500; }
.assertion-empty { font-size: 13px; color: #909399; padding: 4px 0; }
.assertion-row { display: flex; gap: 8px; align-items: center; }
</style>
