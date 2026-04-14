using System.Data;

namespace AutoTest.Application;

/// <summary>
/// 工作单元接口：用于在同一事务中提交多次仓储写操作。
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// 在事务中执行动作，并在成功时提交，失败时回滚。
    /// </summary>
    public Task ExecuteAsync(Func<IDbTransaction, Task> action);

    /// <summary>
    /// 开启事务。
    /// </summary>
    public Task BeginAsync();

    /// <summary>
    /// 当前事务对象。
    /// </summary>
    public IDbTransaction Transaction { get; }

    /// <summary>
    /// 提交事务。
    /// </summary>
    public Task CommitAsync();

    /// <summary>
    /// 回滚事务。
    /// </summary>
    Task RollbackAsync();
}
