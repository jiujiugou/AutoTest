<script setup>
import { reactive } from 'vue'

const props = defineProps({ config: Object })
const c = reactive(props.config)
const panels = reactive(['base'])

function addKv(arr) { arr.push({ key: '', value: '' }) }
function removeKv(arr, i) { if (arr.length > 1) arr.splice(i, 1) }

function reset() {
  Object.assign(c, { url: 'https://example.com', method: 'Get', timeout: 10, headers: [{ key: '', value: '' }], query: [{ key: '', value: '' }], authType: 'None', authToken: '', authUsername: '', authPassword: '', useCookies: false, allowAutoRedirect: false, maxRedirects: 5, ignoreSslErrors: false, proxyUrl: '', proxyUser: '', proxyPass: '', enableRetry: false, retryCount: 1, retryDelayMs: 200, enableRateLimit: false, bodyType: 'Json', bodyContentType: 'application/json', bodyText: '', formFields: [{ key: '', value: '' }] })
}

function validate() {
  if (!(c.url || '').trim()) return 'Url 不能为空'
  return null
}

function buildTargetConfig() {
  const headers = {}
  for (const h of c.headers || []) { const k = (h.key || '').trim(); if (k) headers[k] = (h.value || '').split(',').map(s => s.trim()).filter(Boolean) }
  const query = {}
  for (const q of c.query || []) { const k = (q.key || '').trim(); if (k) query[k] = q.value || '' }
  let body = null
  if (c.bodyType === 'FormUrlEncoded' && (c.formFields || []).some(f => (f.key || '').trim())) {
    body = { Type: 'FormUrlEncoded', ContentType: 'application/x-www-form-urlencoded', Data: Object.fromEntries((c.formFields || []).filter(f => (f.key || '').trim()).map(f => [f.key.trim(), f.value || ''])) }
  } else if (c.bodyText?.trim()) {
    body = { Type: c.bodyType, ContentType: c.bodyContentType || 'application/json', Text: c.bodyText }
  }
  return {
    Url: (c.url || '').trim(), Method: c.method || 'Get', Timeout: Number(c.timeout || 10),
    Headers: Object.keys(headers).length ? headers : null,
    Query: Object.keys(query).length ? query : null,
    Body: body,
    AuthType: c.authType || 'None', AuthToken: c.authToken || null, AuthUsername: c.authUsername || null, AuthPassword: c.authPassword || null,
    UseCookies: !!c.useCookies, AllowAutoRedirect: !!c.allowAutoRedirect, MaxRedirects: Number(c.maxRedirects || 5),
    IgnoreSslErrors: !!c.ignoreSslErrors,
    ProxyUrl: c.proxyUrl || '', ProxyUser: c.proxyUser || '', ProxyPass: c.proxyPass || '',
    EnableRetry: !!c.enableRetry, RetryCount: Number(c.retryCount || 1), RetryDelayMs: Number(c.retryDelayMs || 200),
    EnableRateLimit: !!c.enableRateLimit
  }
}

function buildAssertions(expected) {
  if (!expected) return []
  return [{ Field: 'StatusCode', Operator: 'Equal', Expected: expected }]
}

function loadFrom(cfg) {
  c.url = cfg.Url ?? cfg.url ?? ''
  c.method = cfg.Method ?? cfg.method ?? 'Get'
  c.timeout = cfg.Timeout ?? cfg.timeout ?? 10
  const h = cfg.Headers ?? cfg.headers
  c.headers = h ? Object.entries(h).map(([k, v]) => ({ key: k, value: Array.isArray(v) ? v.join(',') : String(v) })) : [{ key: '', value: '' }]
  if (c.headers.length === 0) c.headers = [{ key: '', value: '' }]
  const q = cfg.Query ?? cfg.query
  c.query = q ? Object.entries(q).map(([k, v]) => ({ key: k, value: String(v) })) : [{ key: '', value: '' }]
  if (c.query.length === 0) c.query = [{ key: '', value: '' }]
  c.authType = cfg.AuthType ?? cfg.authType ?? 'None'
  c.authToken = cfg.AuthToken ?? cfg.authToken ?? ''
  c.authUsername = cfg.AuthUsername ?? cfg.authUsername ?? ''
  c.authPassword = cfg.AuthPassword ?? cfg.authPassword ?? ''
  c.useCookies = !!(cfg.UseCookies ?? cfg.useCookies)
  c.allowAutoRedirect = !!(cfg.AllowAutoRedirect ?? cfg.allowAutoRedirect)
  c.maxRedirects = cfg.MaxRedirects ?? cfg.maxRedirects ?? 5
  c.ignoreSslErrors = !!(cfg.IgnoreSslErrors ?? cfg.ignoreSslErrors)
  c.proxyUrl = cfg.ProxyUrl ?? cfg.proxyUrl ?? ''
  c.proxyUser = cfg.ProxyUser ?? cfg.proxyUser ?? ''
  c.proxyPass = cfg.ProxyPass ?? cfg.proxyPass ?? ''
  c.enableRetry = !!(cfg.EnableRetry ?? cfg.enableRetry)
  c.retryCount = cfg.RetryCount ?? cfg.retryCount ?? 1
  c.retryDelayMs = cfg.RetryDelayMs ?? cfg.retryDelayMs ?? 200
  c.enableRateLimit = !!(cfg.EnableRateLimit ?? cfg.enableRateLimit)
  c.bodyType = 'Json'; c.bodyContentType = 'application/json'; c.bodyText = ''; c.formFields = [{ key: '', value: '' }]
  if (cfg.Body) {
    const b = cfg.Body
    if (b.Type === 'FormUrlEncoded' && b.Data) {
      c.bodyType = 'FormUrlEncoded'
      c.formFields = Object.entries(b.Data).map(([k, v]) => ({ key: k, value: String(v) }))
    } else {
      c.bodyType = b.Type || 'Json'
      c.bodyContentType = b.ContentType || 'application/json'
      c.bodyText = b.Text ?? ''
    }
  }
}

defineExpose({ validate, buildTargetConfig, buildAssertions, loadFrom, reset })
</script>

<template>
  <el-collapse v-model="panels">
    <el-collapse-item title="基础" name="base">
      <div class="kv-grid">
        <div class="kv-item kv-span2"><div class="kv-label">Url</div><el-input v-model="c.url" placeholder="https://example.com" /></div>
        <div class="kv-item"><div class="kv-label">Method</div><el-select v-model="c.method" style="width:100%"><el-option label="Get" value="Get"/><el-option label="Post" value="Post"/><el-option label="Put" value="Put"/><el-option label="Delete" value="Delete"/></el-select></div>
        <div class="kv-item"><div class="kv-label">Timeout(s)</div><el-input-number v-model="c.timeout" :min="1" :max="300" :step="1" style="width:100%"/></div>
      </div>
    </el-collapse-item>
    <el-collapse-item title="Headers" name="headers">
      <div class="kv-list">
        <div v-for="(h,idx) in c.headers" :key="idx" class="kv-row">
          <el-input v-model="h.key" placeholder="Key"/><el-input v-model="h.value" placeholder="Value"/>
          <el-button type="danger" :icon="'Delete'" plain @click="removeKv(c.headers,idx)"/>
        </div>
        <el-button type="primary" plain :icon="'Plus'" @click="addKv(c.headers)">新增</el-button>
      </div>
    </el-collapse-item>
    <el-collapse-item title="Query" name="query">
      <div class="kv-list">
        <div v-for="(q,idx) in c.query" :key="idx" class="kv-row">
          <el-input v-model="q.key" placeholder="Key"/><el-input v-model="q.value" placeholder="Value"/>
          <el-button type="danger" :icon="'Delete'" plain @click="removeKv(c.query,idx)"/>
        </div>
        <el-button type="primary" plain :icon="'Plus'" @click="addKv(c.query)">新增</el-button>
      </div>
    </el-collapse-item>
    <el-collapse-item title="Body" name="body">
      <div class="kv-grid">
        <div class="kv-item"><div class="kv-label">BodyType</div><el-select v-model="c.bodyType" style="width:100%"><el-option label="Json" value="Json"/><el-option label="Form" value="FormUrlEncoded"/><el-option label="Raw" value="Raw"/></el-select></div>
        <div class="kv-item"><div class="kv-label">ContentType</div><el-input v-model="c.bodyContentType" placeholder="application/json"/></div>
      </div>
      <div v-if="c.bodyType==='FormUrlEncoded'" class="kv-list" style="margin-top:12px">
        <div v-for="(f,idx) in c.formFields" :key="idx" class="kv-row">
          <el-input v-model="f.key" placeholder="Field"/><el-input v-model="f.value" placeholder="Value"/>
          <el-button type="danger" :icon="'Delete'" plain @click="removeKv(c.formFields,idx)"/>
        </div>
        <el-button type="primary" plain :icon="'Plus'" @click="addKv(c.formFields)">新增</el-button>
      </div>
      <div v-else class="kv-item kv-span2" style="margin-top:12px"><div class="kv-label">Value</div><el-input v-model="c.bodyText" type="textarea" :rows="8"/></div>
    </el-collapse-item>
    <el-collapse-item title="认证" name="auth">
      <div class="kv-grid">
        <div class="kv-item"><div class="kv-label">AuthType</div><el-select v-model="c.authType" style="width:100%"><el-option label="None" value="None"/><el-option label="Bearer" value="Bearer"/><el-option label="Basic" value="Basic"/><el-option label="ApiKeyHeader" value="ApiKeyHeader"/></el-select></div>
        <div v-if="c.authType==='Bearer'||c.authType==='ApiKeyHeader'" class="kv-item"><div class="kv-label">Token</div><el-input v-model="c.authToken" placeholder="token"/></div>
        <div v-if="c.authType==='Basic'" class="kv-item"><div class="kv-label">Username</div><el-input v-model="c.authUsername"/></div>
        <div v-if="c.authType==='Basic'" class="kv-item"><div class="kv-label">Password</div><el-input v-model="c.authPassword" type="password" show-password/></div>
      </div>
    </el-collapse-item>
    <el-collapse-item title="高级" name="advanced">
      <div class="kv-grid">
        <div class="kv-item"><div class="kv-label">UseCookies</div><el-switch v-model="c.useCookies"/></div>
        <div class="kv-item"><div class="kv-label">AllowAutoRedirect</div><el-switch v-model="c.allowAutoRedirect"/></div>
        <div class="kv-item"><div class="kv-label">MaxRedirects</div><el-input-number v-model="c.maxRedirects" :min="0" :max="50" :step="1" style="width:100%"/></div>
        <div class="kv-item"><div class="kv-label">IgnoreSslErrors</div><el-switch v-model="c.ignoreSslErrors"/></div>
        <div class="kv-item kv-span2"><div class="kv-label">ProxyUrl</div><el-input v-model="c.proxyUrl" placeholder="http://127.0.0.1:7890"/></div>
        <div class="kv-item"><div class="kv-label">ProxyUser</div><el-input v-model="c.proxyUser"/></div>
        <div class="kv-item"><div class="kv-label">ProxyPass</div><el-input v-model="c.proxyPass" type="password" show-password/></div>
        <div class="kv-item"><div class="kv-label">EnableRetry</div><el-switch v-model="c.enableRetry"/></div>
        <div class="kv-item"><div class="kv-label">RetryCount</div><el-input-number v-model="c.retryCount" :min="1" :max="20" :step="1" style="width:100%" :disabled="!c.enableRetry"/></div>
        <div class="kv-item"><div class="kv-label">RetryDelayMs</div><el-input-number v-model="c.retryDelayMs" :min="0" :max="60000" :step="100" style="width:100%" :disabled="!c.enableRetry"/></div>
        <div class="kv-item"><div class="kv-label">EnableRateLimit</div><el-switch v-model="c.enableRateLimit"/></div>
      </div>
    </el-collapse-item>
  </el-collapse>
</template>
