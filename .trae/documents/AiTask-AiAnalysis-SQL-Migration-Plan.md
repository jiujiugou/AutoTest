# AiTaskService SQL 审计 + AiTask/AIAnalysis 表迁移生成计划

## 一、SQL 审计结果（AiTaskService.cs）

### 问题 1：`EnqueueAsync` — INSERT 列与 AiTask 模型不匹配

**当前 SQL：**
```sql
INSERT INTO AiTask
(Id, OutboxMessageId, InputJson, OutputJson, Attempts, Status, NextRunAt, CreatedAt)
VALUES
(@Id, @OutboxMessageId, @InputJson, @OutputJson, @Attempts, @Status, @NextRunAt, @CreatedAt)
```

**问题清单：**

| 问题 | 说明 |
|------|------|
| ❌ `OutboxMessageId` 不存在于 AiTask 模型 | 模型中是 `BizId` (Guid?)，SQL 用 `OutboxMessageId` → Dapper 传参时会传 null |
| ❌ 缺少 `TaskType` | 模型有 `TaskType` (string)，SQL 未包含 |
| ❌ 缺少 `LockedBy` | 模型有 `LockedBy` (string?)，SQL 未包含 |
| ❌ 缺少 `LockedAt` | 模型有 `LockedAt` (DateTime?)，SQL 未包含 |
| ❌ 缺少 `Error` | 模型有 `Error` (string?)，SQL 未包含 |

### 问题 2：`TakeBatchAsync` — 使用 SQL Server 专有语法

**当前 SQL：**
```sql
UPDATE TOP (@Take) AiTask
SET Status = 'Processing', LockedBy = @Worker, LockedAt = @Now
OUTPUT INSERTED.*
WHERE Status = 'Pending' AND NextRunAt <= @Now
```

- `UPDATE TOP` 和 `OUTPUT INSERTED.*` 是 SQL Server 专有语法
- 项目中同时支持 SQLite → 在 SQLite 下会出错
- **建议**：修改为跨数据库兼容的方式（先 SELECT 再 UPDATE）

### 问题 3：`MarkCompletedAsync` — 正确，无需修改

### 问题 4：`MarkFailedAsync` — 正确，无需修改

---

## 二、FluentMigrator 迁移生成

### 2.1 重写 `CreateAiTaskTable.cs`

当前状态：`Up()` 和 `Down()` 都是 `throw new NotImplementedException()`。

需要创建的 **AiTask** 表结构（严格对齐 AiTask 模型）：

| 列名 | 类型 | 约束 |
|------|------|------|
| Id | Guid | PK |
| TaskType | string(100) | NOT NULL |
| BizId | Guid? | NULLABLE (对应 OutboxMessageId 业务含义) |
| InputJson | string(MAX) | NOT NULL |
| OutputJson | string(MAX) | NULLABLE |
| Attempts | int | NOT NULL, DEFAULT 0 |
| Status | string(50) | NOT NULL |
| NextRunAt | DateTime | NOT NULL |
| LockedBy | string(200) | NULLABLE |
| LockedAt | DateTime | NULLABLE |
| Error | string(MAX) | NULLABLE |
| CreatedAt | DateTime | NOT NULL |

索引：
- `IX_AiTask_Status_NextRunAt` — 复合索引 (Status, NextRunAt) — 用于任务轮询
- `IX_AiTask_BizId` — BizId 查询加速

### 2.2 `CreateAIAnalysisTable.cs` — 已验证无需修改

字段和索引定义正确。

---

## 三、修复步骤

### Step 1：重写 `CreateAiTaskTable.cs`

- 文件：`d:\AutoTest\src\AutoTest.Migrations\CreateAiTaskTable.cs`
- 迁移编号：`2026042701`（当前日期）
- 实现完整的 `Up()` 和 `Down()`

### Step 2：修正 `AiTaskService.cs` 的 SQL

- 修正 `EnqueueAsync`：匹配 AiTask 模型字段（BizId 而非 OutboxMessageId，补全 TaskType/LockedBy/LockedAt/Error）
- 修正 `TakeBatchAsync`：改为跨数据库兼容的方式（先 SELECT Id 再逐条 UPDATE）

---

## 四、执行顺序

```
Step 1 → CreateAiTaskTable.cs 迁移实现
   ↓
Step 2 → AiTaskService.cs SQL 修正
   ↓
验证 → dotnet build 编译检查
```
