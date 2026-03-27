using System.Data;
using AutoTest.Application;

namespace AutoTest.Infrastructure;

public class UnitOfWork : IUnitOfWork, IDisposable
{
    readonly IDbConnection _dbConnection;
    private IDbTransaction? _transaction;
    public IDbTransaction Transaction => _transaction ?? throw new InvalidOperationException("No active transaction");
    public UnitOfWork(IDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
        if (_dbConnection.State != ConnectionState.Open)
        {
            _dbConnection.Open();
        }
        _transaction = _dbConnection.BeginTransaction();
    }
    public Task CommitAsync()
    {
        try
        {
            Transaction.Commit();

        }
        catch
        {
            Transaction.Rollback();
            throw;
        }
        finally
        {
            _transaction!.Dispose();
            _transaction = null;
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _transaction!.Dispose();
        _dbConnection.Dispose();
    }


    public Task RollbackAsync()
    {
        try
        {
            _transaction!.Rollback();
        }
        finally
        {
            _transaction!.Dispose();
            _transaction = null;
        }
        return Task.CompletedTask;
    }

}
