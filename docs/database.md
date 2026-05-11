# 数据库设计

## Monitor — 监控任务

| 列 | 类型 | 说明 |
|----|------|------|
| Id | Guid PK | |
| Name | nvarchar(200) | 任务名称 |
| Status | int | 0=Pending, 1=Running, 2=Success, 3=Failed, 4=Timeout, 5=Canceled |
| LastRunTime | datetime2? | 最近执行时间 |
| IsEnabled | bit | 是否启用 |
| TargetType | varchar(50) | HTTP / TCP / DB / PYTHON / TEMPLATE |
| TargetConfig | nvarchar(max) | 目标配置 JSON |
| AutoDailyEnabled | bit | 每日自动执行 |
| AutoDailyTime | varchar(10)? | 执行时间 HH:mm |
| MaxRuns | int? | 最大执行次数 |
| ExecutedCount | int | 已执行次数，默认 0 |
| TemplateVariablesJson | nvarchar(max)? | 模板变量 JSON |
| CreatedAt | datetime2 | |

索引：`TargetType`, `CreatedAt DESC`

`MonitorEntity.IsTemplate` 是计算属性 `=> Target?.Type == "TEMPLATE"`，不依赖 DB 列。

---

## Assertion — 断言规则

| 列 | 类型 | 说明 |
|----|------|------|
| Id | Guid PK | |
| MonitorId | Guid FK → Monitor | |
| Type | varchar(50) | HTTP / TCP / DB / PYTHON |
| ConfigJson | nvarchar(max) | 断言配置 JSON |

---

## AssertionResult — 断言执行结果

| 列 | 类型 | 说明 |
|----|------|------|
| Id | Guid PK | |
| AssertionId | Guid FK → Assertion CASCADE | |
| ExecutionId | Guid FK? | |
| Target | varchar(50) | 断言目标字段 |
| IsSuccess | bit | 是否通过 |
| Actual | nvarchar(max)? | 实际值 |
| Expected | nvarchar(max)? | 期望值 |
| Message | nvarchar(max)? | 失败原因 |
| Timestamp | datetime2 | |

索引：`ExecutionId`, `Timestamp`

---

## ExecutionRecord — 执行记录

| 列 | 类型 | 说明 |
|----|------|------|
| Id | Guid PK | |
| MonitorId | Guid FK → Monitor | |
| Status | int | 执行状态 |
| StartedAt | datetime2 | |
| FinishedAt | datetime2? | |
| IsExecutionSuccess | bit? | 执行阶段是否成功（不含断言） |
| ErrorMessage | nvarchar(max)? | 错误信息 |
| ResultType | varchar(50)? | 结果类型 |
| ResultJson | nvarchar(max)? | 结果 JSON |
| IdempotencyKey | nvarchar(200)? | 幂等键，唯一过滤索引 |
| LockedBy | nvarchar(200)? | 执行者标识 |
| HeartbeatAtUtc | datetime2? | 心跳时间 |
| PlanRunId | uniqueidentifier? | 测试计划批次 ID |

索引：`HeartbeatAtUtc`（看门狗用），`PlanRunId`（报告查询）

---

## TestPlan — 测试计划

| 列 | 类型 | 说明 |
|----|------|------|
| Id | Guid PK | |
| Name | nvarchar(200) | 计划名称 |
| Description | nvarchar(2000)? | 描述 |
| MonitorIdsJson | nvarchar(max) | 监控 ID 数组 JSON，如 `["guid1","guid2"]` |
| CreatedAt | datetime2 | |
| UpdatedAt | datetime2 | |

索引：`CreatedAt DESC`

---

## OutboxMessage — 事务发件箱

| 列 | 类型 | 说明 |
|----|------|------|
| Id | Guid PK | |
| Type | varchar(200) | 事件类型 |
| PayloadJson | nvarchar(max) | 负载 JSON |
| OccurredAt | datetime2 | |
| Status | int | Pending/Processing/Sent/Failed/DeadLetter |
| Attempts | int | |
| NextAttemptAt | datetime2? | 重试时间 |
| LockedUntil | datetime2? | 分布式锁过期 |
| LockedBy | nvarchar(200)? | 锁持有者 |
| LastError | nvarchar(max)? | 错误详情 |
| SentAt | datetime2? | 发送时间 |

索引：`Status`, `NextAttemptAt`, `LockedUntil`, `OccurredAt`

---

## AIAnalysis — AI 分析结果

| 列 | 类型 | 说明 |
|----|------|------|
| Id | Guid PK | |
| OutboxMessageId | Guid? | 关联 Outbox |
| ExecutionRecordId | Guid? | 关联执行记录 |
| Type | varchar(100) | TestFailure/ApiError/PerformanceIssue/SecurityIssue/Unknown |
| Severity | varchar(50) | low/medium/high/critical |
| Category | varchar(200) | 自由分类 |
| Summary | nvarchar(max) | 摘要 |
| RootCause | nvarchar(max) | 根因 |
| Suggestion | nvarchar(max) | 建议 |
| Confidence | float | 置信度 |
| InputJson | nvarchar(max) | 输入 Prompt |
| OutputJson | nvarchar(max) | 原始输出 |
| Model | varchar(200) | 模型名 |
| CreatedAt | datetime2 | |

索引：`OutboxMessageId`, `ExecutionRecordId`, `CreatedAt`

---

## AiTask — AI 任务队列

| 列 | 类型 | 说明 |
|----|------|------|
| Id | Guid PK | |
| TaskType | varchar(100) | MonitorExecutionFailed |
| BizId | varchar(200) | 业务 ID |
| InputJson | nvarchar(max) | 任务输入 |
| OutputJson | nvarchar(max)? | 任务输出 |
| Status | int | Pending/Processing/Completed/Failed/DeadLetter |
| Attempts | int | |
| NextRunAt | datetime2 | |
| LockedBy | nvarchar(200)? | |
| LockedAt | datetime2? | |
| Error | nvarchar(max)? | |
| CreatedAt | datetime2 | |

索引：`Status`, `NextRunAt`

---

## 认证相关

### Users

| 列 | 类型 | 说明 |
|----|------|------|
| Id | int PK 自增 | |
| Username | nvarchar(100) 唯一 | |
| PasswordHash | nvarchar(500) | pbkdf2_sha256 格式 |
| IsActive | bit | |
| CreatedAt | datetime2 | |
| LastLoginAt | datetime2? | |
| IsDeleted | bit | 软删除 |

### RefreshTokens

| 列 | 类型 | 说明 |
|----|------|------|
| Id | int PK 自增 | |
| UserId | int FK → Users | |
| Token | nvarchar(500) 唯一 | |
| ExpireAt | datetime2 | |
| Revoked | bit | |
| CreatedAt | datetime2 | |
| ReplacedByToken | nvarchar(500)? | 轮换链 |

### Roles / Permissions / UserRoles / RolePermissions

标准 RBAC 多对多关系表。Permissions.Code 格式为 `perm:resource.action`。

---

## 迁移列表

| 编号 | 迁移 | 说明 |
|------|------|------|
| Init | InitMonitorTable | Monitor 表 |
| Init | InitAssertionTable | Assertion 表 |
| Init | InitExecutionRecordTable | ExecutionRecord 表 |
| Init | InitAssertionResultTable | AssertionResult 表 |
| - | CreateRefreshTokensTable | 认证 |
| - | CreateOutboxTable | OutboxMessage 表 |
| - | AddMonitorScheduleColumns | AutoDaily 调度字段 |
| - | AddExecutionRecordRuntimeColumns | IdempotencyKey/LockedBy/HeartbeatAtUtc |
| - | AddSqlServerPerformanceIndexes | 性能索引 |
| - | CreateAIAnalysisTable | AI 分析 |
| - | CreateAiTaskTable | AI 任务队列 |
| - | AddMonitorTemplateFields | TemplateVariablesJson |
| - | AddExecutionRecordIdToAIAnalysis | AIAnalysis.ExecutionRecordId FK |
| - | DropMonitorIsTemplateColumn | 删除废弃的 IsTemplate DB 列 |
| - | CreateTestPlanTable | TestPlan 表 |
| - | AddPlanRunIdToExecutionRecord | ExecutionRecord.PlanRunId 列 |

---

## ER 关系

```
Monitor 1──N Assertion
Monitor 1──N ExecutionRecord
ExecutionRecord 1──N AssertionResult
ExecutionRecord 1──0..1 AIAnalysis
TestPlan.N MonitorIds → Monitor.Ids (JSON数组, 非FK)
OutboxMessage 1──0..1 AIAnalysis
User 1──N RefreshToken
User N──M Role
Role N──M Permission
```
