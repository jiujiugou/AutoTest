# 部署方案

## 10.1 架构拓扑

```
┌──────────────────────────────────────────────────────────┐
│                       反向代理                             │
│                  Nginx / Caddy / LB                      │
└────────────┬──────────────────────────┬──────────────────┘
             │                          │
             ▼                          ▼
┌──────────────────────┐   ┌──────────────────────────────┐
│   AutoTest Web API   │   │   AutoTest Web API           │
│   (实例 1)           │   │   (实例 2)                    │
│   Port 5000          │   │   Port 5001                  │
├──────────────────────┤   ├──────────────────────────────┤
│   Hangfire Server    │   │   Hangfire Server            │
│   AiWorker           │   │   AiWorker                   │
│   Outbox Dispatcher  │   │   Outbox Dispatcher          │
└──────┬───────────────┘   └──────────┬───────────────────┘
       │                              │
       └──────────────┬───────────────┘
                      │
         ┌────────────┴────────────┐
         │       SQL Server        │
         │   (业务 + Hangfire)      │
         └─────────────────────────┘
                      │
                      ▼
         ┌─────────────────────────┐
         │    Elasticsearch        │
         │    (日志存储)            │
         └─────────────────────────┘
                      │
                      ▼
         ┌─────────────────────────┐
         │    Redis (可选)         │
         │    (缓存 + 分布式锁)     │
         └─────────────────────────┘
```

## 10.2 部署方式

### Docker Compose（推荐开发环境）

```yaml
version: '3.8'

services:
  autotest-api:
    build: .
    ports:
      - "5000:5000"
    environment:
      - ConnectionStrings__DefaultConnection=Server=sql-server;Database=AutoTestDb;...
      - Logging__ElasticNodes=http://elasticsearch:9200
      - AI__Endpoint=https://api.doubao.com/v1
      - AI__ApiKey=${AI_API_KEY}
    depends_on:
      - sql-server
      - elasticsearch

  sql-server:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourPassword123!

  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:8.12.0
    environment:
      - discovery.type=single-node
      - xpack.security.enabled=false
    ports:
      - "9200:9200"
```

## 10.3 基础设施依赖

| 组件 | 版本要求 | 说明 |
|------|---------|------|
| .NET | 8.0+ | 运行时 |
| SQL Server | 2019+ | 支持 SQLite（开发模式） |
| Elasticsearch | 8.x | 日志存储（7.x 兼容） |
| Redis | 6.x | 可选，兜底 MemoryCache |

## 10.4 扩展性

### 水平扩展

- **Web API 实例**：无状态，可横向扩展（JWT 认证）
- **Hangfire Server**：多个实例共享 DB，自动调度
- **AiWorker**：多实例通过 DB 乐观锁竞争消费 AiTask

### 扩展注意事项

```sql
-- AiTask 的分布式锁依赖 UPDATE 的排他性
UPDATE AiTask SET Status = 'Processing', LockedBy = @Worker
WHERE Id IN (SELECT TOP 10 Id FROM AiTask WHERE Status = 'Pending' AND NextRunAt <= @Now)

-- 多实例同时执行时，DB 行锁确保同一任务不会被重复消费
```

## 10.5 迁移与初始化

```bash
# 数据库迁移（应用启动时自动执行）
dot run -- 启动时 FluentMigrator 自动 MigrateUp()

# 手动回滚
# 在 Program.cs 中取消注释或通过 FluentMigrator CLI
```

## 10.6 环境配置

| 环境 | 数据库 | ES | AI | 说明 |
|------|--------|----|----|------|
| Dev | SQLite | 本地 9200 | Mock | 单机开发 |
| Staging | SQL Server | 测试 ES | 测试模型 | CI 验证 |
| Production | SQL Server 集群 | 生产 ES | 豆包/OpenAI | 高可用 |
