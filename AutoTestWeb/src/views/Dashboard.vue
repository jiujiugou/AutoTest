<template>
  <div class="dashboard-container">
    <div class="toolbar">
      <el-radio-group v-model="range" size="default">
        <el-radio-button label="1h">最近1小时</el-radio-button>
        <el-radio-button label="24h">最近24小时</el-radio-button>
        <el-radio-button label="7d">最近7天</el-radio-button>
      </el-radio-group>
      <el-button type="primary" :icon="'Refresh'" @click="refresh"
        >刷新</el-button
      >
    </div>

    <!-- 核心指标 -->
    <el-row :gutter="20" class="stat-row">
      <el-col :span="6" v-for="(item, index) in statCards" :key="index">
        <el-card shadow="hover" class="stat-card" :class="item.type">
          <div class="stat-header">
            <span class="stat-title">{{ item.title }}</span>
            <el-icon class="stat-icon"><component :is="item.icon" /></el-icon>
          </div>
          <div class="stat-body">
            <el-statistic
              :value="item.value"
              :precision="item.precision"
              :suffix="item.suffix"
            />
          </div>
        </el-card>
      </el-col>
    </el-row>

    <el-row :gutter="20" class="content-row">
      <!-- 最慢接口 -->
      <el-col :span="8">
        <el-card shadow="hover" class="list-card">
          <template #header>
            <div class="card-header">
              <span>最慢接口排行</span>
              <el-tag type="warning" size="small">Top 5</el-tag>
            </div>
          </template>
          <div v-for="(item, index) in slowApis" :key="index" class="list-item">
            <span class="item-index">{{ index + 1 }}</span>
            <span class="item-name">{{ item.api }}</span>
            <span class="item-value warning">{{ item.time }} ms</span>
          </div>
        </el-card>
      </el-col>

      <!-- 失败最多 -->
      <el-col :span="8">
        <el-card shadow="hover" class="list-card">
          <template #header>
            <div class="card-header">
              <span>失败次数排行</span>
              <el-tag type="danger" size="small">Top 5</el-tag>
            </div>
          </template>
          <div v-for="(item, index) in failApis" :key="index" class="list-item">
            <span class="item-index">{{ index + 1 }}</span>
            <span class="item-name">{{ item.api }}</span>
            <span class="item-value danger">{{ item.count }} 次</span>
          </div>
        </el-card>
      </el-col>

      <!-- 最近失败 -->
      <el-col :span="8">
        <el-card shadow="hover" class="list-card">
          <template #header>
            <div class="card-header">
              <span>最近失败记录</span>
              <el-button link type="primary">查看全部</el-button>
            </div>
          </template>
          <el-timeline>
            <el-timeline-item
              v-for="activity in recentFails"
              :key="activity.id"
              :type="'danger'"
              :timestamp="activity.time"
              size="small"
            >
              <div class="timeline-content">
                <div class="api-name">{{ activity.api }}</div>
                <div class="error-msg">{{ activity.error }}</div>
              </div>
            </el-timeline-item>
          </el-timeline>
        </el-card>
      </el-col>
    </el-row>

    <!-- 最近执行记录 -->
    <el-card shadow="hover" class="table-card">
      <template #header>
        <div class="card-header">
          <span>最近执行记录</span>
        </div>
      </template>
      <el-table :data="records" stripe style="width: 100%">
        <el-table-column prop="api" label="监控接口" />
        <el-table-column prop="status" label="执行状态" width="120">
          <template #default="{ row }">
            <el-tag :type="row.status === 'success' ? 'success' : 'danger'">
              {{ row.status === "success" ? "成功" : "失败" }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="time" label="耗时" width="120">
          <template #default="{ row }">
            <span :class="row.time > 500 ? 'warning-text' : ''"
              >{{ row.time }} ms</span
            >
          </template>
        </el-table-column>
        <el-table-column prop="date" label="执行时间" width="180" />
      </el-table>
    </el-card>
  </div>
</template>

<script setup>
import { ref, computed, watch } from "vue";
import { ElMessage } from "element-plus";
import api from "../api/http";

const range = ref("24h");
const loading = ref(false);

const stats = ref({
  monitorTotal: 0,
  running: 0,
  execTotal: 0,
  execSuccess: 0,
  execFail: 0,
  avgTime: 0,
});

const successRate = computed(
  () => (stats.value.execTotal ? (stats.value.execSuccess / stats.value.execTotal) * 100 : 0),
);

const statCards = computed(() => [
  {
    title: "监控总数",
    value: stats.value.monitorTotal,
    icon: "Menu",
    type: "primary",
  },
  {
    title: "运行中任务",
    value: stats.value.running,
    icon: "Loading",
    type: "warning",
  },
  {
    title: "平均成功率",
    value: successRate.value,
    precision: 1,
    suffix: "%",
    icon: "CircleCheck",
    type: "success",
  },
  {
    title: "平均响应时间",
    value: stats.value.avgTime,
    suffix: " ms",
    icon: "Timer",
    type: "info",
  },
]);

const slowApis = ref([]);
const failApis = ref([]);
const recentFails = ref([]);
const records = ref([]);

function refresh() {
  loadDashboard();
}

async function loadDashboard() {
  loading.value = true;
  try {
    const res = await api.get("/api/dashboard", { params: { range: range.value } });
    stats.value = {
      monitorTotal: res?.stats?.monitorTotal ?? 0,
      running: res?.stats?.running ?? 0,
      execTotal: res?.stats?.execTotal ?? 0,
      execSuccess: res?.stats?.execSuccess ?? 0,
      execFail: res?.stats?.execFail ?? 0,
      avgTime: res?.stats?.avgTime ?? 0,
    };
    slowApis.value = Array.isArray(res?.slowApis) ? res.slowApis : [];
    failApis.value = Array.isArray(res?.failApis) ? res.failApis : [];
    recentFails.value = Array.isArray(res?.recentFails) ? res.recentFails : [];
    records.value = Array.isArray(res?.records) ? res.records : [];
  } catch (err) {
    ElMessage.error(err?.message || "获取仪表盘数据失败");
  } finally {
    loading.value = false;
  }
}

watch(range, () => {
  loadDashboard();
}, { immediate: true });
</script>

<style scoped>
.dashboard-container {
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

.stat-row {
  margin-bottom: 20px;
}

.stat-card {
  border: none;
  border-radius: 8px;
  transition: transform 0.3s;
}

.stat-card:hover {
  transform: translateY(-2px);
}

.stat-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  color: #606266;
  font-size: 14px;
  margin-bottom: 12px;
}

.stat-icon {
  font-size: 20px;
  opacity: 0.8;
}

/* 统计颜色 */
.primary :deep(.el-statistic__content) {
  color: #409eff;
}
.success :deep(.el-statistic__content) {
  color: #67c23a;
}
.warning :deep(.el-statistic__content) {
  color: #e6a23c;
}
.danger :deep(.el-statistic__content) {
  color: #f56c6c;
}

.content-row {
  margin-bottom: 20px;
}

.list-card {
  height: 320px;
  border: none;
  border-radius: 8px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-weight: 600;
}

/* ✅ 合并后的 list-item */
.list-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 0;
  margin-top: 8px;
  border-bottom: 1px solid #ebeef5;
}

.list-item:last-child {
  border-bottom: none;
}

.item-index {
  width: 24px;
  height: 24px;
  background: #f4f4f5;
  color: #909399;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  margin-right: 12px;
}

.list-item:nth-child(1) .item-index {
  background: #f56c6c;
  color: #fff;
}
.list-item:nth-child(2) .item-index {
  background: #e6a23c;
  color: #fff;
}
.list-item:nth-child(3) .item-index {
  background: #e6a23c;
  color: #fff;
  opacity: 0.8;
}

.item-name {
  flex: 1;
  font-size: 13px;
  color: #303133;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.item-value {
  font-size: 13px;
  font-weight: 600;
}

.warning {
  color: #e6a23c;
}
.danger {
  color: #f56c6c;
}

.timeline-content {
  display: flex;
  flex-direction: column;
}

.api-name {
  font-size: 13px;
  color: #303133;
}

.error-msg {
  font-size: 12px;
  color: #f56c6c;
  margin-top: 4px;
}

/* 中间布局 */
.middle {
  display: grid;
  grid-template-columns: 1fr 1fr 1fr;
  gap: 16px;
}

.fail-item {
  margin-top: 8px;
}

.fail-msg {
  color: #ef4444;
  font-size: 12px;
}

.time {
  font-size: 12px;
  color: #999;
}

/* ✅ 合并后的 table-card */
.table-card {
  border: none;
  border-radius: 8px;
  margin-top: 10px;
}

.warning-text {
  color: #e6a23c;
  font-weight: 600;
}
</style>
