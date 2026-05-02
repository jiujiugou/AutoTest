# Tasks

- [x] Task 1: 重写 CreateAiTaskTable.cs — 完整的 FluentMigrator 迁移
  - [x] 实现 Up()：创建 AiTask 表（Id, TaskType, BizId, InputJson, OutputJson, Attempts, Status, NextRunAt, LockedBy, LockedAt, Error, CreatedAt）
  - [x] 创建索引 IX_AiTask_Status_NextRunAt（复合索引，用于任务轮询）
  - [x] 创建索引 IX_AiTask_BizId（查询加速）
  - [x] 实现 Down()：删除 AiTask 表
- [x] Task 2: 修正 AiTaskService.cs 的 EnqueueAsync SQL
  - [x] 将 OutboxMessageId 改为 BizId
  - [x] 补全 TaskType、LockedBy、LockedAt、Error 列
- [x] Task 3: 修正 AiTaskService.cs 的 TakeBatchAsync 为跨数据库兼容写法
  - [x] 先 SELECT Id 查询待处理任务
  - [x] 再逐条 UPDATE 锁定状态
  - [x] 返回完整任务列表
- [x] Task 4: 编译验证
  - [x] AutoTest.Migrations 编译成功

# Task Dependencies
- [Task 2] 不依赖其他任务，可并行
- [Task 3] 不依赖其他任务，可并行
- [Task 4] 依赖 [Task 1, Task 2, Task 3]
