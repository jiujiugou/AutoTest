using System.Data;

namespace AutoTest.Application;

public interface IUnitOfWork
{
    public Task ExecuteAsync(Func<IDbTransaction, Task> action);
    public Task BeginAsync();
    public IDbTransaction Transaction { get; }
    public Task CommitAsync();
    Task RollbackAsync();
}
