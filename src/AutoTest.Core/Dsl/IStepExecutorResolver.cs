namespace AutoTest.Core.Dsl;

public interface IStepExecutorResolver
{
    IStepExecutor Resolve(string type);
}
