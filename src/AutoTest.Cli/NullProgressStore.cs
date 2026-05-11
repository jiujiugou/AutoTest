using AutoTest.Core.Dsl;

namespace AutoTest.Cli;

internal class NullProgressStore : IProgressStore
{
    public Task SaveAsync(DslRuntimeContext ctx) => Task.CompletedTask;
    public Task<DslRuntimeContext?> RestoreAsync(string executionId) => Task.FromResult<DslRuntimeContext?>(null);
    public Task CompleteAsync(string executionId) => Task.CompletedTask;
}
