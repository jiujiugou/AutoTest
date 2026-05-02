# 配置项

## 9.1 appsettings.json 模板

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=AutoTestDb;Trusted_Connection=True;TrustServerCertificate=True;",
    "HangfireConnection": "Server=.;Database=AutoTestDb_Hangfire;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Database": {
    "Provider": "SqlServer"
  },
  "Logging": {
    "ElasticNodes": "http://localhost:9200",
    "EnableElasticsearch": true
  },
  "Jwt": {
    "SigningKey": "your-secret-key-32-chars-minimum-here",
    "ExpireMinutes": 1440
  },
  "AI": {
    "Endpoint": "https://api.doubao.com/v1",
    "ApiKey": "your-api-key",
    "Model": "doubao-pro",
    "MaxTokens": 4096,
    "Temperature": 0.3
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000"]
  },
  "App": {
    "EnableHttpsRedirection": true
  },
  "BootstrapAdmin": {
    "Username": "admin",
    "Password": "admin123456"
  },
  "Outbox": {
    "BatchSize": 20,
    "LockDurationSeconds": 120,
    "PollingIntervalMs": 2000,
    "Webhook": {
      "Enabled": false,
      "Url": "",
      "RetryCount": 3
    }
  }
}
```

## 9.2 配置项说明

### 数据库

| 键 | 默认值 | 说明 |
|----|--------|------|
| `ConnectionStrings:DefaultConnection` | 必填 | 业务数据库连接串 |
| `ConnectionStrings:HangfireConnection` | 同上 | Hangfire 数据库连接串 |
| `Database:Provider` | SqlServer | `SqlServer` / `Sqlite` |

### 日志

| 键 | 默认值 | 说明 |
|----|--------|------|
| `Logging:ElasticNodes` | http://localhost:9200 | ES 节点地址（逗号分隔） |
| `Logging:EnableElasticsearch` | true | 是否启用 ES 日志写入 |

### AI

| 键 | 默认值 | 说明 |
|----|--------|------|
| `AI:Endpoint` | 必填 | LLM API 端点 |
| `AI:ApiKey` | 必填 | API Key |
| `AI:Model` | doubao-pro | 模型名称 |
| `AI:MaxTokens` | 4096 | 最大 Token 数 |
| `AI:Temperature` | 0.3 | 推理温度（越低越确定） |

### 认证

| 键 | 默认值 | 说明 |
|----|--------|------|
| `Jwt:SigningKey` | 必填 | JWT 签名密钥（≥32字符） |
| `Jwt:ExpireMinutes` | 1440 | Token 过期时间（分钟） |
| `BootstrapAdmin:Username` | admin | 初始管理员用户名 |
| `BootstrapAdmin:Password` | admin123456 | 初始管理员密码 |

### Outbox

| 键 | 默认值 | 说明 |
|----|--------|------|
| `Outbox:BatchSize` | 20 | 每轮领取消息数 |
| `Outbox:LockDurationSeconds` | 120 | 消息锁时长 |
| `Outbox:Webhook:Enabled` | false | 是否启用 Webhook |
| `Outbox:Webhook:Url` | "" | Webhook 回调地址 |

## 9.3 环境变量覆盖

所有配置项可通过环境变量覆盖，使用双下划线分隔：

```bash
# Linux / Docker
export ConnectionStrings__DefaultConnection="Server=prod-db;..."
export AI__ApiKey="sk-prod-xxx"

# Windows PowerShell
$env:ConnectionStrings__DefaultConnection = "Server=prod-db;..."
$env:AI__ApiKey = "sk-prod-xxx"
```
