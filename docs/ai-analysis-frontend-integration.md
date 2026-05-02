# AI 分析结果前后端对接方案

---

## 一、现状与问题

### 当前链路

```
执行失败 → OutboxMessage → AiTask → AiWorker → AIAnalysis（落库）
```

AIAnalysis 表中已存储了 `RootCause`（根因）和 `Suggestion`（修复建议），但：

### 断点

| 环节 | 问题 |
|------|------|
| **AIAnalysis 表** | `OutboxMessageId` 关联到 OutboxMessage，但前端只知道 `ExecutionRecordId`，两者没有直接关联 |
| **IAnalysisRepository** | 只有 `AddAsync`，没有任何查询方法 |
| **后端 API** | 没有暴露 AI 分析结果的端点 |
| **前端 Monitor.vue** | 不展示任何 AI 分析信息 |
| **前端 api/monitors.js** | 没有获取分析结果的方法 |

### 数据关联关系

```
ExecutionRecord (Id = executionId)
    └── Orchestrator 写入 OutboxMessage (Id = outboxId, PayloadJson 含 executionId)
            └── AiAnalysisConsumer → AiTask (BizId = outboxId)
                    └── AiWorker → AIAnalysis (OutboxMessageId = outboxId)
```

**问题**：前端只知道 `executionId`，但 AIAnalysis 只存了 `OutboxMessageId`，无法直接查询。

---

## 二、改动方案

### 方案选择：ExecutionRecordId 唯一业务查询入口

**设计原则**：
- `OutboxMessageId` **保留**，仅作为内部追踪字段（用于调试和关联 Outbox 消息）
- `ExecutionRecordId` **新增**，作为唯一的业务查询入口

```
查询路径：前端 executionId → AIAnalysis.ExecutionRecordId（直接查询，无需 JSON 解析）
内部追踪：AIAnalysis.OutboxMessageId → OutboxMessage（调试用，对前端透明）
```

---

## 三、后端改动

### 3.1 AIAnalysis 模型加字段

**文件**：`src/AutoTest.Core/AI/AIAnalysis.cs`

```csharp
public sealed class AIAnalysis
{
    // ... 现有字段不变 ...

    /// <summary>
    /// 关联的 Outbox 消息 ID。
    /// 内部追踪字段，用于调试和关联 Outbox 消息链，对前端透明。
    /// </summary>
    public Guid OutboxMessageId { get; init; }

    /// <summary>
    /// 关联的执行记录 ID。
    /// 唯一业务查询入口，前端通过 executionId 直接查询。
    /// </summary>
    public Guid ExecutionRecordId { get; init; }
}
```

### 3.2 IAnalysisRepository 加查询方法

**文件**：`src/AutoTest.Core/Repositories/IAnalysisRepository.cs`

```csharp
public interface IAnalysisRepository
{
    Task AddAsync(AIAnalysis analysis);

    /// <summary>
    /// 按执行记录 ID 获取 AI 分析结果
    /// </summary>
    Task<AIAnalysis?> GetByExecutionRecordIdAsync(Guid executionRecordId);

    /// <summary>
    /// 按监控任务 ID 获取分析结果列表（最近 N 条）
    /// </summary>
    Task<List<AIAnalysis>> GetByMonitorIdAsync(Guid monitorId, int take = 20);
}
```

### 3.3 AnalysisRepository 实现

**文件**：`src/AutoTest.Infrastructure/AnalysisRepository.cs`

```csharp
public async Task<AIAnalysis?> GetByExecutionRecordIdAsync(Guid executionRecordId)
{
    const string sql = """
        SELECT Id, OutboxMessageId, ExecutionRecordId, Type, Severity, Category,
               RootCause, Suggestion, Summary, Confidence, Model, PromptVersion,
               CreatedAt, ProcessedAt
        FROM AIAnalysis
        WHERE ExecutionRecordId = @ExecutionRecordId
        """;
    return await _connection.QuerySingleOrDefaultAsync<AIAnalysis>(sql, new { ExecutionRecordId = executionRecordId });
}

public async Task<List<AIAnalysis>> GetByMonitorIdAsync(Guid monitorId, int take = 20)
{
    const string sql = """
        SELECT a.Id, a.OutboxMessageId, a.ExecutionRecordId, a.Type, a.Severity, a.Category,
               a.RootCause, a.Suggestion, a.Summary, a.Confidence, a.Model, a.PromptVersion,
               a.CreatedAt, a.ProcessedAt
        FROM AIAnalysis a
        INNER JOIN ExecutionRecord e ON e.Id = a.ExecutionRecordId
        WHERE e.MonitorId = @MonitorId
        ORDER BY a.CreatedAt DESC
        LIMIT @Take
        """;
    var rows = await _connection.QueryAsync<AIAnalysis>(sql, new { MonitorId = monitorId, Take = take });
    return rows.ToList();
}
```

### 3.4 AiWorker 写入时填充 ExecutionRecordId

**文件**：`src/AutoTest.Infrastructure/AI/AiWorker.cs`（ProcessOne 方法）

```csharp
// 现有逻辑中已有 input.TraceId = executionId
var analysis = new AIAnalysis
{
    // ... 现有字段 ...
    ExecutionRecordId = Guid.TryParse(input?.TraceId, out var execId) ? execId : Guid.Empty,
};
```

### 3.5 添加 API 端点

**文件**：`src/AutoTest.Webapi/Controllers/MonitorController.cs`

```csharp
[HttpGet("executions/{executionId}/analysis")]
public async Task<IActionResult> GetExecutionAnalysis(Guid executionId)
{
    var analysis = await _analysisRepository.GetByExecutionRecordIdAsync(executionId);
    if (analysis == null)
        return NotFound(new { message = "暂无 AI 分析结果" });
    return Ok(new
    {
        analysis.Id,
        analysis.Type,
        analysis.Severity,
        analysis.Category,
        analysis.RootCause,
        analysis.Suggestion,
        analysis.Summary,
        analysis.Confidence,
        analysis.CreatedAt
    });
}

[HttpGet("{monitorId}/analysis-list")]
public async Task<IActionResult> GetAnalysisList(Guid monitorId, [FromQuery] int take = 20)
{
    var list = await _analysisRepository.GetByMonitorIdAsync(monitorId, take);
    return Ok(list.Select(a => new
    {
        a.Id,
        a.ExecutionRecordId,
        a.Type,
        a.Severity,
        a.Category,
        a.Summary,
        a.Confidence,
        a.CreatedAt
    }));
}
```

**依赖注入**：在 `MonitorController` 构造函数中加入 `IAnalysisRepository`：

```csharp
private readonly IAnalysisRepository _analysisRepository;

public MonitorController(IMonitorService monitorService, IWorkflowScheduler workflowScheduler,
    IAnalysisRepository analysisRepository)
{
    _monitorService = monitorService;
    _workflowScheduler = workflowScheduler;
    _analysisRepository = analysisRepository;
}
```

### 3.6 FluentMigrator

**文件**：`src/AutoTest.Migrations/AddExecutionRecordIdToAIAnalysis.cs`

```csharp
[Migration(2026042802)]
public class AddExecutionRecordIdToAIAnalysis : Migration
{
    public override void Up()
    {
        Alter.Table("AIAnalysis")
            .AddColumn("ExecutionRecordId").AsGuid().Nullable();

        Create.Index("IX_AIAnalysis_ExecutionRecordId")
            .OnTable("AIAnalysis")
            .OnColumn("ExecutionRecordId");
    }

    public override void Down()
    {
        Delete.Index("IX_AIAnalysis_ExecutionRecordId").OnTable("AIAnalysis");
        Delete.Column("ExecutionRecordId").FromTable("AIAnalysis");
    }
}
```

---

## 四、前端改动

### 4.1 api/monitors.js 新增方法

```javascript
executionAnalysis(executionId) {
  return api.get(`/api/monitor/executions/${encodeURIComponent(String(executionId))}/analysis`);
},

analysisList(monitorId, take = 20) {
  return api.get(`/api/monitor/${encodeURIComponent(String(monitorId))}/analysis-list?take=${take}`);
}
```

### 4.2 Monitor.vue — 在失败记录行添加"AI 分析"按钮

在执行记录表格中增加一列操作按钮（仅在失败记录显示）：

```html
<el-table-column label="AI 分析" width="100" align="center">
  <template #default="{ row }">
    <el-button
      v-if="!row.isExecutionSuccess"
      size="small"
      type="warning"
      plain
      @click="showAiAnalysis(row)"
    >
      分析
    </el-button>
  </template>
</el-table-column>
```

### 4.3 Monitor.vue — 添加 AI 分析结果抽屉

```html
<!-- AI 分析结果抽屉 -->
<el-drawer
  v-model="analysisDrawerVisible"
  title="AI 分析结果"
  size="500px"
  direction="rtl"
>
  <template v-if="analysisLoading">
    <div style="text-align:center;padding:40px">
      <el-skeleton :rows="6" animated />
    </div>
  </template>

  <template v-else-if="analysisError">
    <el-empty description="暂无 AI 分析结果" />
    <p style="color:#999;font-size:13px;text-align:center">
      分析任务可能正在排队，请稍后重试
    </p>
  </template>

  <template v-else-if="analysisData">
    <div class="analysis-detail">
      <!-- 严重级别 -->
      <div class="analysis-section">
        <span class="label">严重级别</span>
        <el-tag :type="severityTagType(analysisData.severity)" size="default">
          {{ severityLabel(analysisData.severity) }}
        </el-tag>
        <el-tag type="info" size="default" style="margin-left:8px">
          置信度 {{ (analysisData.confidence * 100).toFixed(0) }}%
        </el-tag>
        <el-tag v-if="analysisData.type" type="info" size="default" style="margin-left:8px">
          {{ analysisData.type }}
        </el-tag>
      </div>

      <!-- 分类 -->
      <div class="analysis-section">
        <span class="label">问题分类</span>
        <span>{{ analysisData.category || '未分类' }}</span>
      </div>

      <!-- 摘要 -->
      <div class="analysis-section">
        <span class="label">摘要</span>
        <p>{{ analysisData.summary }}</p>
      </div>

      <!-- 根因分析 -->
      <div class="analysis-section">
        <span class="label">根因分析</span>
        <div class="code-block">{{ analysisData.rootCause }}</div>
      </div>

      <!-- 修复建议 -->
      <div class="analysis-section">
        <span class="label">修复建议</span>
        <div class="code-block suggestion">{{ analysisData.suggestion }}</div>
      </div>

      <!-- 分析时间 -->
      <div class="analysis-section">
        <span class="label">分析时间</span>
        <span>{{ formatTime(analysisData.createdAt) }}</span>
      </div>
    </div>
  </template>
</el-drawer>
```

### 4.4 Monitor.vue — 响应式数据和逻辑

```javascript
// 新增响应式数据
const analysisDrawerVisible = ref(false)
const analysisLoading = ref(false)
const analysisError = ref(false)
const analysisData = ref(null)

// 新增方法
async function showAiAnalysis(record) {
  analysisDrawerVisible.value = true
  analysisLoading.value = true
  analysisError.value = false
  analysisData.value = null

  try {
    const res = await MonitorsApi.executionAnalysis(record.id)
    if (res && res.id) {
      analysisData.value = res
    } else {
      analysisError.value = true
    }
  } catch {
    analysisError.value = true
  } finally {
    analysisLoading.value = false
  }
}

function severityTagType(severity) {
  const map = { critical: 'danger', high: 'warning', medium: 'warning', low: 'info' }
  return map[severity] || 'info'
}

function severityLabel(severity) {
  const map = {
    critical: '严重',
    high: '高',
    medium: '中',
    low: '低'
  }
  return map[severity] || severity || '未知'
}
```

### 4.5 Monitor.vue — 样式

```css
.analysis-detail {
  padding: 0 8px;
}

.analysis-section {
  margin-bottom: 20px;
}

.analysis-section .label {
  display: block;
  font-size: 13px;
  color: #909399;
  margin-bottom: 6px;
  font-weight: 500;
}

.analysis-section p {
  margin: 0;
  line-height: 1.6;
  color: #303133;
}

.code-block {
  background: #f5f7fa;
  border: 1px solid #e4e7ed;
  border-radius: 6px;
  padding: 12px 16px;
  font-size: 14px;
  line-height: 1.7;
  white-space: pre-wrap;
  word-break: break-word;
  color: #303133;
}

.code-block.suggestion {
  border-left: 3px solid #e6a23c;
}
```

---

## 五、完整数据流

```
执行失败
    ↓
AIAnalysis 落库（含 ExecutionRecordId）
    ↓
前端刷新执行记录列表
    ↓
用户在失败记录上点击「分析」按钮
    ↓
GET /api/monitor/executions/{executionId}/analysis
    ↓
后端从 AIAnalysis 表查 ExecutionRecordId
    ↓
返回 { rootCause, suggestion, severity, confidence, ... }
    ↓
前端展示抽屉面板
    ├── 严重级别 + 置信度 + 分类
    ├── 根因分析（code block）
    ├── 修复建议（code block）
    └── 分析时间
```

---

## 六、改动清单

| 步骤 | 内容 | 文件 |
|------|------|------|
| 1 | AIAnalysis 加 `ExecutionRecordId` 字段 | `AutoTest.Core/AI/AIAnalysis.cs` |
| 2 | IAnalysisRepository 加查询接口 | `AutoTest.Core/Repositories/IAnalysisRepository.cs` |
| 3 | AnalysisRepository 实现查询方法 | `AutoTest.Infrastructure/AnalysisRepository.cs` |
| 4 | AiWorker ProcessOne 填充 ExecutionRecordId | `AutoTest.Infrastructure/AI/AiWorker.cs` |
| 5 | 新增 API 端点 (execution/analysis + monitor/analysis-list) | `AutoTest.Webapi/Controllers/MonitorController.cs` |
| 6 | FluentMigrator 迁移 (加列 + 索引) | `AutoTest.Migrations/AddExecutionRecordIdToAIAnalysis.cs` |
| 7 | api/monitors.js 新增方法 | `AutoTestWeb/src/api/monitors.js` |
| 8 | Monitor.vue 新增分析按钮 + 抽屉面板 + 逻辑 | `AutoTestWeb/src/views/Monitor.vue` |

### 改动量估算

| 侧 | 文件数 | 新增代码（约） |
|----|--------|---------------|
| 后端 | 6 个文件 | ~100 行 |
| 前端 | 2 个文件 | ~120 行 |
| 迁移 | 1 个文件 | ~30 行 |
| **合计** | **9 个文件** | **~250 行** |
