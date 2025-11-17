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
}