using System.Reflection;
using SqlSugar;

namespace Radish.Repository.UnitOfWorks;

/// <summary>事务接口</summary>
public interface IUnitOfWorkManage
{
    SqlSugarScope GetDbClient();
    int TranCount { get; }
    UnitOfWorks CreateUnitOfWork();

    /// <summary>开始事务</summary>
    void BeginTran();

    /// <summary>开始事务</summary>
    void BeginTran(MethodInfo method);

    /// <summary>提交事务</summary>
    void CommitTran();

    /// <summary>提交事务</summary>
    void CommitTran(MethodInfo method);

    /// <summary>回滚事务</summary>
    void RollbackTran();

    /// <summary>回滚事务</summary>
    void RollbackTran(MethodInfo method);

    /// <summary>
    /// 在当前事务内通过数据库保存点执行操作；操作失败时先回滚到保存点，再原样抛出异常。
    /// </summary>
    /// <remarks>
    /// 当前没有事务时直接执行操作。该边界用于唯一约束竞争等“允许恢复后继续查询”的场景，
    /// 避免 PostgreSQL 将整个外层事务标记为 aborted。
    /// </remarks>
    Task<TResult> ExecuteInSavepointAsync<TResult>(Func<Task<TResult>> operation);
}
