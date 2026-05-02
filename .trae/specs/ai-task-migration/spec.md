# AiTask Migration + SQL 修正 Spec

## Why
- `CreateAiTaskTable.cs` 的 `Up()` 和 `Down()` 都是 `throw new NotImplementedException()`，导致 AiTask 表从未在数据库中创建
- `AiTaskService.cs` 的 SQL 语句与 `AiTask` 模型字段不匹配（使用不存在的 `OutboxMessageId` 列，缺少 `TaskType` 等字段）
- `TakeBatchAsync` 使用 SQL Server 专有语法（`UPDATE TOP` + `OUTPUT INSERTED`），与项目中同时支持的 SQLite 不兼容

## What Changes
- **重写** `CreateAiTaskTable.cs` — 实现完整的 FluentMigrator 迁移
- **修正** `AiTaskService.cs` 的 EnqueueAsync SQL — 匹配 AiTask 模型字段
- **修正** `AiTaskService.cs` 的 TakeBatchAsync — 改为跨数据库兼容的写法
- CreateAIAnalysisTable.cs 已验证，**无需修改**

## Impact
- Affected specs: AiTask 数据库表创建、AiTask 任务队列 SQL
- Affected code: `CreateAiTaskTable.cs`（重写）、`AiTaskService.cs`（SQL 修正）

## ADDED Requirements

### Requirement: AiTask 表迁移
系统通过 FluentMigrator 创建 AiTask 表，严格对齐 `AiTask` 模型。

#### Scenario: 迁移 Up
- **WHEN** 执行迁移
- **THEN** 创建 `AiTask` 表，包含 Id(PK), TaskType, BizId, InputJson, OutputJson, Attempts, Status, NextRunAt, LockedBy, LockedAt, Error, CreatedAt
- **THEN** 创建 `IX_AiTask_Status_NextRunAt` 和 `IX_AiTask_BizId` 索引

#### Scenario: 迁移 Down
- **WHEN** 回滚迁移
- **THEN** 删除 `AiTask` 表

### Requirement: 跨数据库兼容的 TakeBatch
`AiTaskService.TakeBatchAsync` 应在 SQL Server 和 SQLite 下均能正常工作。

#### Scenario: 任务轮询
- **WHEN** Worker 调用 `TakeBatchAsync`
- **THEN** 先 SELECT 出待处理的任务，再逐条 UPDATE 锁定
- **THEN** 返回锁定成功的任务列表

## MODIFIED Requirements

### Requirement: EnqueueAsync SQL
INSERT 语句的列列表必须与 AiTask 模型完全匹配：使用 `BizId` 替代 `OutboxMessageId`，补全 `TaskType`、`LockedBy`、`LockedAt`、`Error`。
