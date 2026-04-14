using System.Data;
using AutoTest.Application;

namespace AutoTest.Infrastructure;

/// <summary>
/// 工作单元（Unit of Work）：封装数据库连接与事务生命周期，并提供以回调方式执行事务的便捷方法。
/// </summary>
public class UnitOfWork : IUnitOfWork, IDisposable
{
    private readonly IDbConnection _dbConnection;
    private IDbTransaction? _transaction;

    /// <summary>
    /// 当前活动事务。
    /// </summary>
    /// <exception cref="InvalidOperationException">当没有开启事务时抛出。</exception>
    public IDbTransaction Transaction =>
        _transaction ?? throw new InvalidOperationException("No active transaction");

    /// <summary>
    /// 初始化 <see cref="UnitOfWork"/> 并确保连接处于打开状态。
    /// </summary>
    /// <param name="dbConnection">数据库连接。</param>
    public UnitOfWork(IDbConnection dbConnection)
    {
        _dbConnection = dbConnection;

        if (_dbConnection.State != ConnectionState.Open)
            _dbConnection.Open();
    }

    /// <summary>
    /// 开启事务（如果尚未开启）。
    /// </summary>
    public Task BeginAsync()
    {
        if (_transaction != null)
            return Task.CompletedTask;

        _transaction = _dbConnection.BeginTransaction();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 提交事务；若提交失败则回滚并抛出异常。
    /// </summary>
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

    /// <summary>
    /// 回滚事务并清理事务对象。
    /// </summary>
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

    /// <summary>
    /// 在一个事务中执行指定操作：自动 Begin/Commit；发生异常时自动 Rollback。
    /// </summary>
    /// <param name="action">事务内操作。</param>
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

    /// <summary>
    /// 释放事务与连接资源。
    /// </summary>
    public void Dispose()
    {
        _transaction?.Dispose();
        _dbConnection.Dispose();
    }
}
