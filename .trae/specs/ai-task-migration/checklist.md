* [x] CreateAiTaskTable.cs 的 Up() 实现了完整的 AiTask 表创建

* [x] CreateAiTaskTable.cs 的 Down() 实现了 AiTask 表删除

* [x] 创建了 IX\_AiTask\_Status\_NextRunAt 复合索引

* [x] 创建了 IX\_AiTask\_BizId 索引

* [x] AiTaskService.EnqueueAsync 的 INSERT 使用 BizId 替代 OutboxMessageId

* [x] AiTaskService.EnqueueAsync 的 INSERT 包含 TaskType、LockedBy、LockedAt、Error 列

* [x] AiTaskService.TakeBatchAsync 使用跨数据库兼容写法（SELECT + 逐条 UPDATE）

* [x] AutoTest.Migrations 编译通过

