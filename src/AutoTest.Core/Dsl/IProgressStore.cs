namespace AutoTest.Core.Dsl;

/// <summary>
/// DSL 执行进度持久化 —— 用于断点续跑。
/// 每执行完一个步骤后保存快照，恢复时从上次中断处继续，避免重复执行已完成的步骤。
/// </summary>
public interface IProgressStore
{
    Task SaveAsync(DslRuntimeContext ctx);
    Task<DslRuntimeContext?> RestoreAsync(string executionId);
    Task CompleteAsync(string executionId);
}
