using System.Data;
using AutoTest.Application;

namespace AutoTest.Infrastructure;

public class UnitOfWork : IUnitOfWork, IDisposable
{
    private readonly IDbConnection _dbConnection;
    private IDbTransaction? _transaction;

    public IDbTransaction Transaction =>
        _transaction ?? throw new InvalidOperationException("No active transaction");

    public UnitOfWork(IDbConnection dbConnection)
    {
        _dbConnection = dbConnection;

        if (_dbConnection.State != ConnectionState.Open)
            _dbConnection.Open();
    }

    public Task BeginAsync()
    {
        if (_transaction != null)
            return Task.CompletedTask;

        _transaction = _dbConnection.BeginTransaction();
        return Task.CompletedTask;
    }

    public Task CommitAsync()
    {
        try
        {
            _transaction?.Commit();
        }
        catch
        {
            _transaction?.Rollback();
            throw;
        }
        finally
        {
            _transaction?.Dispose();
            _transaction = null;
        }
        return Task.CompletedTask;
    }

    public Task RollbackAsync()
    {
        try
        {
            _transaction?.Rollback();
        }
        finally
        {
            _transaction?.Dispose();
            _transaction = null;
        }
        return Task.CompletedTask;
    }
    public async Task ExecuteAsync(Func<IDbTransaction, Task> action)
    {
        await BeginAsync();
        try
        {
            await action(Transaction);
            await CommitAsync();
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }
    public void Dispose()
    {
        _transaction?.Dispose();
        _dbConnection.Dispose();
    }
}
