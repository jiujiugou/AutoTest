// ══════════════════════════════════════
// 一键导入预设 — 纯数据，不含 UI 逻辑
// ══════════════════════════════════════

function buildAssertion(type, field, operator, expected, headerKey = '') {
  const id = crypto.randomUUID()
  return {
    Id: id,
    Type: type,
    ConfigJson: JSON.stringify({ Id: id, Field: field, Operator: operator, Expected: String(expected), HeaderKey: headerKey || '' })
  }
}

function tcpTarget(host, port, timeout = 5, useTls = false) {
  return JSON.stringify({ Host: host, Port: Number(port), TimeoutSeconds: Number(timeout), UseTls: useTls, ConnectTimeoutMs: 15000, ReadTimeoutMs: 30000, WriteTimeoutMs: 10000 })
}

function httpTarget(url, method = 'Get', timeout = 10) {
  return JSON.stringify({ Url: url, Method: method, TimeoutSeconds: Number(timeout) })
}

function dbTarget(connStr, sql, dbType = 'SqlServer', commandType = 'Query', timeout = 30) {
  return JSON.stringify({ ConnectionString: connStr, Sql: sql, DbType: dbType, CommandType: commandType, TimeoutSeconds: Number(timeout) })
}

function pythonTarget(scriptContent, scriptPath = '', timeout = 60) {
  return JSON.stringify({ ScriptContent: scriptContent, ScriptPath: scriptPath, PythonExecutable: 'python', TimeoutSeconds: Number(timeout), WorkingDirectory: '', SuccessExitCodesText: '0' })
}

// preset 结构说明（面向最终使用者）：
//   label        — 显示在下拉菜单里的短名称
//   description  — 一句话告诉用户这个监控干什么、什么时候会通知你
//   type         — TargetType（HTTP/TCP/DB/PYTHON/TEMPLATE）
//   fields[]     — 用户需要填的参数，每个字段有 key / label / placeholder / defaultValue
//   summary()    — 根据参数生成一句人类可读的摘要，展示在确认区
//   build()      — 生成 POST /api/monitor 的 DTO

export const presets = {
  // ── HTTP ──────────────────────────
  http_health: {
    label: 'API 健康检查',
    description: '定时请求一个 API 地址，状态码不是 200 就钉钉通知你。最常见的接口监控。',
    type: 'HTTP',
    fields: [
      { key: 'url', label: 'API 地址', placeholder: 'https://你的服务地址/health', defaultValue: '' }
    ],
    summary(p) { return `每隔一段时间访问 ${p.url}，检查状态码是否为 200` },
    build(p) {
      return {
        Name: `${p.url} 健康检查`,
        TargetType: 'HTTP',
        TargetConfig: httpTarget(p.url),
        IsEnabled: true,
        AutoDailyEnabled: true,
        AutoDailyTime: '09:00',
        Assertions: [buildAssertion('HTTP', 'StatusCode', 'Equal', '200')]
      }
    }
  },

  // ── TCP ───────────────────────────
  redis_port: {
    label: 'Redis 端口探测',
    description: '检查 Redis 能不能连通、能不能正常响应 PING，延迟超过 100ms 也会通知你。',
    type: 'TCP',
    fields: [
      { key: 'host', label: 'Redis 地址', placeholder: '127.0.0.1', defaultValue: '127.0.0.1' },
      { key: 'port', label: 'Redis 端口', placeholder: '6379', defaultValue: '6379' }
    ],
    summary(p) { return `连接 ${p.host}:${p.port}，验证能连通、能 PONG、延迟 <100ms` },
    build(p) {
      return {
        Name: `Redis ${p.host}:${p.port} 连通性`,
        TargetType: 'TCP',
        TargetConfig: tcpTarget(p.host, p.port),
        IsEnabled: true,
        AutoDailyEnabled: true,
        AutoDailyTime: '09:00',
        Assertions: [
          buildAssertion('TCP', 'Connected', 'Equal', 'True'),
          buildAssertion('TCP', 'Response', 'Contains', 'PONG'),
          buildAssertion('TCP', 'LatencyMs', 'LessThan', '100')
        ]
      }
    }
  },

  mysql_port: {
    label: 'MySQL 端口探测',
    description: '检查 MySQL 数据库端口能不能连通，连不上就通知你。适合用来发现数据库宕机。',
    type: 'TCP',
    fields: [
      { key: 'host', label: 'MySQL 地址', placeholder: '127.0.0.1', defaultValue: '127.0.0.1' },
      { key: 'port', label: 'MySQL 端口', placeholder: '3306', defaultValue: '3306' }
    ],
    summary(p) { return `连接 ${p.host}:${p.port}，验证端口可达` },
    build(p) {
      return {
        Name: `MySQL ${p.host}:${p.port} 连通性`,
        TargetType: 'TCP',
        TargetConfig: tcpTarget(p.host, p.port),
        IsEnabled: true,
        AutoDailyEnabled: true,
        AutoDailyTime: '09:00',
        Assertions: [buildAssertion('TCP', 'Connected', 'Equal', 'True')]
      }
    }
  },

  external_reachable: {
    label: '外部服务可达性',
    description: '检查外部服务（百度、微信支付、钉钉等）能不能连通，判断服务器出网是否正常。',
    type: 'TCP',
    fields: [
      { key: 'host', label: '服务地址', placeholder: 'www.baidu.com', defaultValue: 'www.baidu.com' },
      { key: 'port', label: '端口', placeholder: '443', defaultValue: '443' }
    ],
    summary(p) { return `检测 ${p.host}:${p.port} 是否可达，连不上就通知你` },
    build(p) {
      const friendlyNames = { 'www.baidu.com': '百度', 'www.weixin.qq.com': '微信', 'oapi.dingtalk.com': '钉钉', 'api.weixin.qq.com': '微信支付' }
      const friendly = friendlyNames[p.host] || p.host
      return {
        Name: `${friendly}(${p.host}:${p.port}) 可达性`,
        TargetType: 'TCP',
        TargetConfig: tcpTarget(p.host, p.port),
        IsEnabled: true,
        AutoDailyEnabled: true,
        AutoDailyTime: '09:00',
        Assertions: [buildAssertion('TCP', 'Connected', 'Equal', 'True')]
      }
    }
  },

  // ── TEMPLATE ──────────────────────
  login_check_userinfo: {
    label: '登录 → 查用户信息',
    description: '模拟用户登录获取 Token，再用 Token 调用一个需要登录的接口。验证整套认证链路是否正常。',
    type: 'TEMPLATE',
    fields: [
      { key: 'baseUrl', label: 'API 基础地址', placeholder: 'https://你的API地址', defaultValue: '' },
      { key: 'username', label: '登录用户名', placeholder: 'admin', defaultValue: 'admin' },
      { key: 'password', label: '登录密码', placeholder: '', defaultValue: '' }
    ],
    summary(p) { return `先登录 ${p.baseUrl} 获取 Token，再带着 Token 查用户信息，两步都返回 200 才算正常` },
    build(p) {
      const dslJson = JSON.stringify({
        steps: [
          {
            name: 'login',
            type: 'http',
            input: {
              url: `${p.baseUrl}/api/auth/login`,
              method: 'Post',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ username: p.username, password: p.password }),
              timeout: 10
            },
            assertions: [{ field: 'StatusCode', operator: 'Equal', expected: '200' }],
            timeout: '10s',
            extracts: { token: '$.token' }
          },
          {
            name: 'getUserInfo',
            type: 'http',
            input: {
              url: `${p.baseUrl}/api/auth/me`,
              method: 'Get',
              headers: { 'Authorization': 'Bearer {{token}}' },
              timeout: 10
            },
            assertions: [{ field: 'StatusCode', operator: 'Equal', expected: '200' }],
            timeout: '10s',
            dependsOn: ['login']
          }
        ]
      })
      return {
        Name: `${p.baseUrl} 登录-用户信息`,
        TargetType: 'TEMPLATE',
        TargetConfig: dslJson,
        IsEnabled: true,
        AutoDailyEnabled: true,
        AutoDailyTime: '09:00',
        IsTemplate: true,
        TemplateVariablesJson: null,
        Assertions: []
      }
    }
  },

  // ── PYTHON ────────────────────────
  cert_expiry: {
    label: 'SSL 证书过期检查',
    description: '检查 HTTPS 证书还有多少天过期，剩余不足 N 天就钉钉通知你。避免证书过期导致用户访问不了。',
    type: 'PYTHON',
    fields: [
      { key: 'domain', label: '域名', placeholder: 'example.com', defaultValue: '' },
      { key: 'warnDays', label: '提前多少天告警', placeholder: '30', defaultValue: '30' }
    ],
    summary(p) { return `检查 ${p.domain} 的 HTTPS 证书，剩余不足 ${p.warnDays} 天就通知你` },
    build(p) {
      const days = p.warnDays || '30'
      const script = [
        'import ssl, socket, sys',
        'from datetime import datetime',
        '',
        `domain = "${p.domain}"`,
        `warn_days = ${days}`,
        '',
        'ctx = ssl.create_default_context()',
        'with ctx.wrap_socket(socket.socket(), server_hostname=domain) as s:',
        '    s.settimeout(10)',
        '    s.connect((domain, 443))',
        '    cert = s.getpeercert()',
        '',
        "expiry = datetime.strptime(cert['notAfter'], '%b %d %H:%M:%S %Y %Z')",
        'remaining = (expiry - datetime.now()).days',
        '',
        'if remaining < 0:',
        '    print(f"CERT_EXPIRED: {domain} 证书已于 {-remaining} 天前过期")',
        '    sys.exit(1)',
        'elif remaining < warn_days:',
        '    print(f"CERT_WARN: {domain} 证书将在 {remaining} 天后过期")',
        '    sys.exit(1)',
        'else:',
        '    print(f"CERT_OK: {domain} 证书还有 {remaining} 天有效")',
        '    sys.exit(0)'
      ].join('\n')
      return {
        Name: `${p.domain} 证书检查(不足${days}天告警)`,
        TargetType: 'PYTHON',
        TargetConfig: pythonTarget(script, '', 30),
        IsEnabled: true,
        AutoDailyEnabled: true,
        AutoDailyTime: '08:00',
        Assertions: [buildAssertion('PYTHON', 'ExitCode', 'Equal', '0')]
      }
    }
  },

  slow_task: {
    label: '定时脚本监控',
    description: '监控一个定时运行的 Python 脚本是否正常跑完，挂了或超时就通知你。适合数据同步、报表这些批处理任务。',
    type: 'PYTHON',
    fields: [
      { key: 'scriptPath', label: '脚本路径（服务器上的绝对路径）', placeholder: '/opt/scripts/nightly_job.py', defaultValue: '' },
      { key: 'timeoutSec', label: '超时时间（秒）', placeholder: '300', defaultValue: '300' }
    ],
    summary(p) { return `运行 ${p.scriptPath}，超时 ${p.timeoutSec} 秒，退出码非 0 就通知你` },
    build(p) {
      const timeout = p.timeoutSec || '300'
      const script = [
        'import subprocess, sys, time',
        `start = time.time()`,
        `result = subprocess.run(["python", "${p.scriptPath}"], capture_output=True, text=True, timeout=${timeout})`,
        'elapsed = time.time() - start',
        'print(f"EXIT: {result.returncode}  ELAPSED: {elapsed:.1f}s")',
        'print(result.stdout)',
        'if result.stderr: print(result.stderr, file=sys.stderr)',
        'sys.exit(result.returncode)'
      ].join('\n')
      return {
        Name: `${p.scriptPath} 执行监控`,
        TargetType: 'PYTHON',
        TargetConfig: pythonTarget(script, p.scriptPath, Number(timeout) + 30),
        IsEnabled: true,
        AutoDailyEnabled: false,
        AutoDailyTime: null,
        MaxRuns: 1,
        Assertions: [buildAssertion('PYTHON', 'ExitCode', 'Equal', '0')]
      }
    }
  },

  // ── DB ────────────────────────────
  db_rowcount: {
    label: '数据库行数检查',
    description: '查询一张表的数据量，低于预期值就通知你。适合监控订单表、用户表这些关键表是否正常增长。',
    type: 'DB',
    fields: [
      { key: 'connStr', label: '数据库连接串', placeholder: 'Server=你的服务器;Database=你的库;User Id=sa;Password=...', defaultValue: '' },
      { key: 'tableName', label: '表名', placeholder: 'Orders', defaultValue: '' },
      { key: 'minRows', label: '最少行数（低于这个值就告警）', placeholder: '100', defaultValue: '100' }
    ],
    summary(p) { return `查询 ${p.tableName} 表的行数，少于 ${p.minRows} 行就通知你` },
    build(p) {
      return {
        Name: `${p.tableName} 行数检查(期望≥${p.minRows})`,
        TargetType: 'DB',
        TargetConfig: dbTarget(p.connStr, `SELECT COUNT(1) FROM ${p.tableName}`),
        IsEnabled: true,
        AutoDailyEnabled: true,
        AutoDailyTime: '09:00',
        Assertions: [buildAssertion('DB', 'Scalar', 'GreaterOrEqual', p.minRows)]
      }
    }
  }
}

// 保持向后兼容
export default presets
