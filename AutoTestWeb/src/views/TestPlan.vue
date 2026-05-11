<template>
  <div class="testplan-page">
    <div class="page-header">
      <h2>测试计划</h2>
      <el-button type="primary" @click="showCreateDialog">创建计划</el-button>
    </div>

    <!-- Plan list -->
    <el-table :data="plans" stripe v-loading="loading" style="width:100%">
      <el-table-column prop="name" label="计划名称" min-width="180" />
      <el-table-column prop="description" label="描述" min-width="200" show-overflow-tooltip />
      <el-table-column label="监控数" width="90">
        <template #default="{ row }">{{ row.monitorCount }}</template>
      </el-table-column>
      <el-table-column prop="createdAt" label="创建时间" width="170" />
      <el-table-column label="操作" width="280" fixed="right">
        <template #default="{ row }">
          <el-button size="small" @click="runPlan(row)">执行</el-button>
          <el-button size="small" @click="editPlan(row)">编辑</el-button>
          <el-button size="small" @click="viewReport(row)">报告</el-button>
          <el-button size="small" type="danger" @click="deletePlan(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- Create / Edit dialog -->
    <el-dialog v-model="dialogVisible" :title="isEdit ? '编辑计划' : '创建计划'" width="600px">
      <el-form :model="form" label-width="100px">
        <el-form-item label="名称">
          <el-input v-model="form.name" placeholder="计划名称" />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="form.description" type="textarea" :rows="2" placeholder="可选描述" />
        </el-form-item>
        <el-form-item label="监控任务">
          <el-select v-model="form.monitorIds" multiple filterable placeholder="选择监控任务" style="width:100%">
            <el-option v-for="m in monitors" :key="m.id" :label="m.name" :value="m.id" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="savePlan">保存</el-button>
      </template>
    </el-dialog>

    <!-- Report dialog -->
    <el-dialog v-model="reportVisible" title="执行报告" width="800px">
      <div v-if="report">
        <h3>{{ report.testPlanName }}</h3>
        <p>执行时间: {{ report.executedAt }} | 通过率:
          <span :style="{ color: report.passRate >= 1 ? '#16a34a' : '#dc2626', fontWeight:'bold' }">
            {{ (report.passRate * 100).toFixed(1) }}%
          </span>
        </p>
        <div style="background:#e5e7eb;height:8px;border-radius:4px;margin:8px 0">
          <div :style="{width:(report.passRate*100)+'%',background:'#16a34a',height:'100%',borderRadius:'4px'}" />
        </div>
        <p>总计 {{ report.totalCount }} / 通过 {{ report.passCount }} / 失败 {{ report.failCount }}
          | 最快 {{ report.durationMinMs }}ms / 平均 {{ report.durationAvgMs }}ms / 最慢 {{ report.durationMaxMs }}ms</p>

        <el-table :data="report.items" stripe size="small" style="margin-top:16px">
          <el-table-column prop="monitorName" label="监控" min-width="140" />
          <el-table-column label="状态" width="70">
            <template #default="{ row }">
              <span :style="{ color: row.passed ? '#16a34a' : '#dc2626' }">{{ row.passed ? '通过' : '失败' }}</span>
            </template>
          </el-table-column>
          <el-table-column prop="durationMs" label="耗时(ms)" width="100" />
          <el-table-column prop="errorMessage" label="错误" min-width="200" show-overflow-tooltip />
        </el-table>
      </div>
      <div v-else-if="reportLoading">加载中...</div>
      <div v-else>
        <p>选择一次执行记录查看报告:</p>
        <el-table :data="planRuns" stripe size="small" @row-click="loadRunReport" highlight-current-row>
          <el-table-column prop="planRunId" label="执行批次" width="280" />
          <el-table-column prop="executedAt" label="时间" width="170" />
          <el-table-column label="通过率" width="100">
            <template #default="{ row }">{{ (row.passRate*100).toFixed(0) }}%</template>
          </el-table-column>
          <el-table-column label="通过/总计" width="100">
            <template #default="{ row }">{{ row.passCount }}/{{ row.totalCount }}</template>
          </el-table-column>
        </el-table>
      </div>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { TestPlanApi } from '../api/testplan'
import { MonitorsApi } from '../api/monitors'

const plans = ref([])
const monitors = ref([])
const loading = ref(false)
const dialogVisible = ref(false)
const isEdit = ref(false)
const editId = ref(null)
const form = ref({ name: '', description: '', monitorIds: [] })

const reportVisible = ref(false)
const report = ref(null)
const reportLoading = ref(false)
const planRuns = ref([])
const currentPlan = ref(null)

onMounted(() => {
  loadPlans()
  loadMonitors()
})

async function loadPlans() {
  loading.value = true
  try {
    plans.value = await TestPlanApi.list()
  } finally {
    loading.value = false
  }
}

async function loadMonitors() {
  try {
    monitors.value = await MonitorsApi.list()
  } catch { /* ignore */ }
}

function showCreateDialog() {
  isEdit.value = false
  editId.value = null
  form.value = { name: '', description: '', monitorIds: [] }
  dialogVisible.value = true
}

function editPlan(row) {
  isEdit.value = true
  editId.value = row.id
  form.value = { name: row.name, description: row.description || '', monitorIds: row.monitorIds || [] }
  dialogVisible.value = true
}

async function savePlan() {
  const data = {
    name: form.value.name,
    description: form.value.description,
    monitorIds: form.value.monitorIds
  }
  try {
    if (isEdit.value) {
      await TestPlanApi.update(editId.value, data)
      ElMessage.success('计划已更新')
    } else {
      await TestPlanApi.create(data)
      ElMessage.success('计划已创建')
    }
    dialogVisible.value = false
    await loadPlans()
  } catch (e) {
    ElMessage.error('保存失败: ' + (e.message || ''))
  }
}

async function runPlan(row) {
  try {
    await ElMessageBox.confirm(`确定执行计划 "${row.name}" 吗？`, '确认执行')
    const result = await TestPlanApi.run(row.id)
    ElMessage.success(`执行完成，批次: ${result.planRunId}`)
    await loadPlans()
  } catch { /* cancelled or error */ }
}

async function deletePlan(row) {
  try {
    await ElMessageBox.confirm(`确定删除计划 "${row.name}" 吗？`, '确认删除', { type: 'warning' })
    await TestPlanApi.remove(row.id)
    ElMessage.success('已删除')
    await loadPlans()
  } catch { /* cancelled */ }
}

async function viewReport(row) {
  currentPlan.value = row
  report.value = null
  reportLoading.value = false
  reportVisible.value = true
  try {
    planRuns.value = await TestPlanApi.getRuns(row.id)
  } catch {
    planRuns.value = []
  }
}

async function loadRunReport(runRow) {
  reportLoading.value = true
  try {
    report.value = await TestPlanApi.getReport(currentPlan.value.id, runRow.planRunId)
  } catch (e) {
    ElMessage.error('加载报告失败: ' + (e.message || ''))
  } finally {
    reportLoading.value = false
  }
}
</script>

<style scoped>
.testplan-page { padding: 0; }
.page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
.page-header h2 { margin: 0; font-size: 1.25rem; }
</style>
