# Tasks

- [x] Task 1: 创建 TraceLogEntry DTO
  - [x] 创建文件 `d:\AutoTest\src\AutoTest.Core\AI\TraceLogEntry.cs`
  - [x] 定义包含 Timestamp、Level、Message、Exception 属性的轻量 DTO
- [x] Task 2: 扩展 ILogService 接口
  - [x] 在 `d:\AutoTest\src\AutoTest.Application\ILogService.cs` 添加 `GetAiErrorContextAsync` 方法
- [x] Task 3: 在 LogService 中实现 GetAiErrorContextAsync
  - [x] 在 `d:\AutoTest\src\AutoTest.Infrastructure\Log\LogService.cs` 实现 `ILogService.GetAiErrorContextAsync`
  - [x] 将 `ElasticLogDocument` 映射为 `TraceLogEntry`
- [x] Task 4: 创建 TraceContextBuilder 类
  - [x] 创建文件 `d:\AutoTest\src\AutoTest.AI\TraceContextBuilder.cs`
  - [x] 实现 `BuildTraceContextAsync` 方法，输出 LLM 友好的 Markdown
  - [x] 添加 KernelFunction 属性
- [x] Task 5: 在 KernelFactory.Create() 中集成 TraceContextBuilder
  - [x] 修改 `d:\AutoTest\src\AutoTest.AI\KernelFactory.cs` 的 Create 方法接受 `ILogService` 参数
  - [x] 内部注册 TraceContextBuilder 为 Kernel Plugin
- [x] Task 6: 更新 SkAiClient
  - [x] 修改 `d:\AutoTest\src\AutoTest.AI\SkAiClient.cs` 构造函数接受 `ILogService`
- [x] Task 7: 注册 DI 服务
  - [x] 在 `d:\AutoTest\src\AutoTest.Infrastructure\AddInfrastructureServiceCollectionExtensions.cs` 注册 SkAiClient (Scoped)

# Task Dependencies
- [Task 2] depends on [Task 1]
- [Task 3] depends on [Task 1, Task 2]
- [Task 4] depends on [Task 1, Task 2]
- [Task 5] depends on [Task 4]
- [Task 6] depends on [Task 5]
- [Task 7] depends on [Task 5]
