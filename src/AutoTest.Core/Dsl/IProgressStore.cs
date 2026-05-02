namespace AutoTest.Core.Dsl;

public interface IProgressStore
{
    Task SaveAsync(DslRuntimeContext ctx);
    Task<DslRuntimeContext?> RestoreAsync(string executionId);
    Task CompleteAsync(string executionId);
}
