# 数据表设计

## 7.1 核心业务表

### Monitor（监控任务）

```sql
CREATE TABLE Monitor (
    Id              UNIQUEIDENTIFIER PRIMARY KEY,
    Name            NVARCHAR(256)    NOT NULL,
    Status          INT              NOT NULL,  -- Pending=0, Running=1, Success=2, Fail=3
    LastRunTime     DATETIME2        NULL,
    IsEnabled       BIT              NOT NULL DEFAULT 1,
    TargetType      NVARCHAR(64)     NOT NULL,  -- HTTP / TCP / DB / Python
    TargetConfig    NVARCHAR(MAX)    NOT NULL,  -- JSON 序列化的配置
    AutoDailyEnabled BIT             NOT NULL DEFAULT 0,
    AutoDailyTime   NVARCHAR(10)     NULL,      -- HH:mm 格式
    MaxRuns         INT              NULL,      -- 最大自动执行次数
    ExecutedCount   INT              NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
```

### ExecutionRecord（执行记录）

```sql
CREATE TABLE ExecutionRecord (
    Id                UNIQUEIDENTIFIER PRIMARY KEY,
    MonitorId         UNIQUEIDENTIFIER NOT NULL,
    Status            INT              NOT NULL,
    StartedAt         DATETIME2        NOT NULL,
    FinishedAt        DATETIME2        NULL,
    IsExecutionSuccess BIT             NOT NULL,
    ErrorMessage      NVARCHAR(MAX)    NULL,
    ResultType        NVARCHAR(128)    NOT NULL,
    ResultJson        NVARCHAR(MAX)    NOT NULL,
    IdempotencyKey    NVARCHAR(256)    NULL,   -- 幂等键
    LockedBy          NVARCHAR(128)    NULL,   -- 执行者标识
    HeartbeatAtUtc    DATETIME2        NULL,   -- 心跳时间
    FOREIGN KEY (MonitorId) REFERENCES Monitor(Id)
);

CREATE INDEX IX_ExecutionRecord_MonitorId ON ExecutionRecord(MonitorId);
CREATE UNIQUE INDEX IX_ExecutionRecord_IdempotencyKey ON ExecutionRecord(IdempotencyKey) WHERE IdempotencyKey IS NOT NULL;
```

### AssertionResult（断言结果）

```sql
CREATE TABLE AssertionResult (
    Id              UNIQUEIDENTIFIER PRIMARY KEY,
    ExecutionId     UNIQUEIDENTIFIER NOT NULL,
    AssertionId     UNIQUEIDENTIFIER NOT NULL,
    Target          NVARCHAR(256)    NOT NULL,
    IsSuccess       BIT              NOT NULL,
    Actual          NVARCHAR(MAX)    NULL,
    Expected        NVARCHAR(MAX)    NULL,
    Message         NVARCHAR(MAX)    NULL,
    Timestamp       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (ExecutionId) REFERENCES ExecutionRecord(Id)
);

CREATE INDEX IX_AssertionResult_ExecutionId ON AssertionResult(ExecutionId);
```

## 7.2 AI 分析表

### AiTask（AI 分析任务队列）

```sql
CREATE TABLE AiTask (
    Id              UNIQUEIDENTIFIER PRIMARY KEY,
    TaskType        NVARCHAR(128)    NOT NULL,  -- MonitorExecutionFailed
    BizId           UNIQUEIDENTIFIER NULL,      -- 业务 ID（如 OutboxMessageId）
    InputJson       NVARCHAR(MAX)    NOT NULL,  -- 输入给 LLM 的原始数据
    OutputJson      NVARCHAR(MAX)    NULL,      -- LLM 原始输出
    Attempts        INT              NOT NULL DEFAULT 0,
    Status          NVARCHAR(32)     NOT NULL,  -- Pending/Processing/Success/Failed/DeadLetter
    NextRunAt       DATETIME2        NOT NULL,
    LockedBy        NVARCHAR(128)    NULL,
    LockedAt        DATETIME2        NULL,
    Error           NVARCHAR(MAX)    NULL,
    CreatedAt       DATETIME2        NOT NULL
);

CREATE INDEX IX_AiTask_Status_NextRunAt ON AiTask(Status, NextRunAt) WHERE Status = 'Pending';
```

### AIAnalysis（分析结果）

```sql
CREATE TABLE AIAnalysis (
    Id              UNIQUEIDENTIFIER PRIMARY KEY,
    OutboxMessageId UNIQUEIDENTIFIER NOT NULL,   -- 关联的 Outbox 消息
    Type            NVARCHAR(64)     NOT NULL,   -- TestFailure/ApiError/...
    Severity        NVARCHAR(16)     NOT NULL,   -- low/medium/high/critical
    Category        NVARCHAR(64)     NOT NULL,   -- NULL_REFERENCE/TIMEOUT/...
    Summary         NVARCHAR(512)    NOT NULL,   -- AI 摘要
    RootCause       NVARCHAR(2048)   NOT NULL,   -- 根因分析
    Suggestion      NVARCHAR(2048)   NOT NULL,   -- 修复建议
    Impact          NVARCHAR(32)     NOT NULL,   -- single_request/module_level/system_level
    FaultService    NVARCHAR(128)    NULL,       -- 触发故障的服务
    Confidence      FLOAT            NOT NULL,   -- 置信度 0~1
    InputJson       NVARCHAR(MAX)    NOT NULL,   -- 输入快照
    OutputJson      NVARCHAR(MAX)    NOT NULL,   -- LLM 原始输出
    Model           NVARCHAR(64)     NOT NULL,   -- 模型名称
    PromptVersion   NVARCHAR(16)     NOT NULL,   -- Prompt 版本
    CreatedAt       DATETIME2        NOT NULL,
    ProcessedAt     DATETIME2        NULL
);

CREATE INDEX IX_AIAnalysis_OutboxMessageId ON AIAnalysis(OutboxMessageId);
CREATE INDEX IX_AIAnalysis_CreatedAt ON AIAnalysis(CreatedAt);
```

### AutoRecoveryRecord（自动恢复记录）

```sql
CREATE TABLE AutoRecoveryRecord (
    Id              UNIQUEIDENTIFIER PRIMARY KEY,
    AnalysisId      UNIQUEIDENTIFIER NOT NULL,   -- 关联 AIAnalysis
    ActionType      NVARCHAR(32)     NOT NULL,   -- Retry/ConfigFix/InfraFix/Notify
    ActionDetail    NVARCHAR(512)    NOT NULL,
    TargetService   NVARCHAR(128)    NOT NULL DEFAULT '',
    Status          NVARCHAR(32)     NOT NULL,   -- Pending/Success/Failed/Skipped
    ErrorMessage    NVARCHAR(MAX)    NULL,
    CreatedAt       DATETIME2        NOT NULL,
    ExecutedAt      DATETIME2        NULL,
    FOREIGN KEY (AnalysisId) REFERENCES AIAnalysis(Id)
);
```

## 7.3 事件与队列表

### OutboxMessage（事件总线）

```sql
CREATE TABLE OutboxMessage (
    Id              UNIQUEIDENTIFIER PRIMARY KEY,
    EventType       NVARCHAR(256)    NOT NULL,   -- 事件类型全名
    Payload         NVARCHAR(MAX)    NOT NULL,   -- JSON 序列化的事件体
    Status          INT              NOT NULL,   -- Pending=0, Sent=1, Failed=2
    LockedBy        NVARCHAR(128)    NULL,
    LockedAt        DATETIME2        NULL,
    RetryCount      INT              NOT NULL DEFAULT 0,
    SentAt          DATETIME2        NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_OutboxMessage_Status ON OutboxMessage(Status) WHERE Status = 0;
```

## 7.4 RBAC 权限表

```sql
CREATE TABLE Users (
    Id              INT PRIMARY KEY IDENTITY,
    Username        NVARCHAR(64)  NOT NULL UNIQUE,
    PasswordHash    NVARCHAR(256) NOT NULL,
    DisplayName     NVARCHAR(128) NULL,
    IsDeleted       BIT           NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2     NOT NULL DEFAULT GETUTCDATE()
);

CREATE TABLE Roles (
    Id              INT PRIMARY KEY IDENTITY,
    Name            NVARCHAR(64)  NOT NULL UNIQUE,
    Description     NVARCHAR(256) NULL
);

CREATE TABLE UserRoles (
    UserId          INT NOT NULL REFERENCES Users(Id),
    RoleId          INT NOT NULL REFERENCES Roles(Id),
    PRIMARY KEY (UserId, RoleId)
);

CREATE TABLE Permissions (
    Id              INT PRIMARY KEY IDENTITY,
    Name            NVARCHAR(128) NOT NULL UNIQUE,
    Description     NVARCHAR(256) NULL
);

CREATE TABLE RolePermissions (
    RoleId          INT NOT NULL REFERENCES Roles(Id),
    PermissionId    INT NOT NULL REFERENCES Permissions(Id),
    PRIMARY KEY (RoleId, PermissionId)
);
```

## 7.5 性能索引

```sql
-- AiTask 轮询加速
CREATE INDEX IX_AiTask_Status_NextRunAt ON AiTask(Status, NextRunAt) WHERE Status = 'Pending';

-- ExecutionRecord 看板统计
CREATE INDEX IX_ExecutionRecord_MonitorId_Status ON ExecutionRecord(MonitorId, Status, StartedAt DESC);

-- Outbox 轮询加速
CREATE INDEX IX_OutboxMessage_Status ON OutboxMessage(Status) WHERE Status = 0;
```
