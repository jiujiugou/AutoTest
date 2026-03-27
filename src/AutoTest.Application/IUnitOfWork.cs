using System.Data;

namespace AutoTest.Application;

public interface IUnitOfWork
{
    IDbTransaction Transaction { get; }
    Task CommitAsync();
    Task RollbackAsync();
}
