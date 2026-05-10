<script setup>
import { reactive } from 'vue'

const props = defineProps({ config: Object })
const c = reactive(props.config)

function reset() {
  Object.assign(c, { dbType: 'sqlserver', commandType: 'Query', timeoutSeconds: 30, enableRetry: false, retryCount: 2, retryDelayMs: 500, connectionString: '', sql: '' })
}

function validate() {
  if (!(c.connectionString || '').trim()) return 'ConnectionString 不能为空'
  if (!(c.sql || '').trim()) return 'SQL 不能为空'
  return null
}

function buildTargetConfig() {
  return {
    ConnectionString: (c.connectionString || '').trim(),
    Sql: (c.sql || '').trim(),
    DbType: c.dbType || 'sqlserver',
    CommandType: c.commandType || 'Query',
    TimeoutSeconds: Number(c.timeoutSeconds || 30),
    EnableRetry: !!c.enableRetry,
    RetryCount: Number(c.retryCount || 2),
    RetryDelayMs: Number(c.retryDelayMs || 500)
  }
}

function buildAssertions() { return [] }

function loadFrom(cfg) {
  c.dbType = cfg.DbType ?? cfg.dbType ?? 'sqlserver'
  c.commandType = cfg.CommandType ?? cfg.commandType ?? 'Query'
  c.timeoutSeconds = cfg.TimeoutSeconds ?? cfg.timeoutSeconds ?? 30
  c.enableRetry = !!(cfg.EnableRetry ?? cfg.enableRetry)
  c.retryCount = cfg.RetryCount ?? cfg.retryCount ?? 2
  c.retryDelayMs = cfg.RetryDelayMs ?? cfg.retryDelayMs ?? 500
  c.connectionString = cfg.ConnectionString ?? cfg.connectionString ?? ''
  c.sql = cfg.Sql ?? cfg.sql ?? ''
}

defineExpose({ validate, buildTargetConfig, buildAssertions, loadFrom, reset })
</script>

<template>
  <div class="kv-grid">
    <div class="kv-item">
      <div class="kv-label">DbType</div>
      <el-select v-model="c.dbType" style="width: 100%">
        <el-option label="SQL Server" value="sqlserver" />
        <el-option label="MySQL" value="mysql" />
        <el-option label="PostgreSQL" value="postgresql" />
      </el-select>
    </div>
    <div class="kv-item">
      <div class="kv-label">CommandType</div>
      <el-select v-model="c.commandType" style="width: 100%">
        <el-option label="Query" value="Query" />
        <el-option label="NonQuery" value="NonQuery" />
        <el-option label="Scalar" value="Scalar" />
      </el-select>
    </div>
    <div class="kv-item">
      <div class="kv-label">Timeout(s)</div>
      <el-input-number v-model="c.timeoutSeconds" :min="1" :max="3600" :step="1" style="width: 100%" />
    </div>
    <div class="kv-item">
      <div class="kv-label">EnableRetry</div>
      <el-switch v-model="c.enableRetry" />
    </div>
    <template v-if="c.enableRetry">
      <div class="kv-item"><div class="kv-label">RetryCount</div><el-input-number v-model="c.retryCount" :min="1" :max="10" :step="1" style="width: 100%" /></div>
      <div class="kv-item"><div class="kv-label">RetryDelay(ms)</div><el-input-number v-model="c.retryDelayMs" :min="100" :max="30000" :step="500" style="width: 100%" /></div>
    </template>
    <div class="kv-item kv-span2">
      <div class="kv-label">ConnectionString</div>
      <el-input v-model="c.connectionString" type="textarea" :rows="3" placeholder="Server=...;Database=...;" />
    </div>
    <div class="kv-item kv-span2">
      <div class="kv-label">SQL</div>
      <el-input v-model="c.sql" type="textarea" :rows="6" placeholder="SELECT 1" />
    </div>
  </div>
</template>
