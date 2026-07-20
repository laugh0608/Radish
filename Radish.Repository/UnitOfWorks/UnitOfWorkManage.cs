using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Radish.Common;
using SqlSugar;

namespace Radish.Repository.UnitOfWorks;

/// <summary>事务接口实现</summary>
public class UnitOfWorkManage : IUnitOfWorkManage
{
    private readonly ILogger<UnitOfWorkManage> _logger;
    private readonly ISqlSugarClient _sqlSugarClient;

    private int _tranCount { get; set; }
    private long _savepointSequence;
    public int TranCount => _tranCount;
    public readonly ConcurrentStack<string> TranStack = new();

    public UnitOfWorkManage(ISqlSugarClient sqlSugarClient, ILogger<UnitOfWorkManage> logger)
    {
        _sqlSugarClient = sqlSugarClient;
        _logger = logger;
        _tranCount = 0;
    }

    /// <summary>获取 Db，保证唯一性</summary>
    /// <returns></returns>
    public SqlSugarScope GetDbClient()
    {
        // 必须要 as，后边会用到切换数据库操作
        return _sqlSugarClient as SqlSugarScope
            ?? throw new InvalidOperationException("当前 ISqlSugarClient 不是 SqlSugarScope，无法执行多租户/事务操作。");
    }


    public UnitOfWorks CreateUnitOfWork()
    {
        UnitOfWorks uow = new UnitOfWorks();
        uow.Logger = _logger;
        uow.Db = _sqlSugarClient;
        uow.Tenant = (ITenant)_sqlSugarClient;
        uow.IsTran = true;

        uow.Db.Open();
        uow.Tenant.BeginTran();
        _logger.LogDebug("UnitOfWork Begin");
        return uow;
    }

    public void BeginTran()
    {
        lock (this)
        {
            _tranCount++;
            GetDbClient().BeginTran();
        }
    }

    public void BeginTran(MethodInfo method)
    {
        lock (this)
        {
            GetDbClient().BeginTran();
            TranStack.Push(method.GetFullName());
            _tranCount = TranStack.Count;
        }
    }

    public void CommitTran()
    {
        lock (this)
        {
            _tranCount--;
            if (_tranCount == 0)
            {
                try
                {
                    GetDbClient().CommitTran();
                }
                catch (Exception commitException)
                {
                    _logger.LogError(commitException, "UnitOfWork commit failed; rolling back the transaction.");
                    RollbackAfterCommitFailure(commitException);
                    throw;
                }
            }
        }
    }

    public void CommitTran(MethodInfo method)
    {
        lock (this)
        {
            string? result = null;
            while (!TranStack.IsEmpty && !TranStack.TryPeek(out result))
            {
                Thread.Sleep(1);
            }


            if (result == method.GetFullName())
            {
                try
                {
                    GetDbClient().CommitTran();

                    _logger.LogDebug($"Commit Transaction");
                    Console.WriteLine($"Commit Transaction");
                }
                catch (Exception commitException)
                {
                    _logger.LogError(
                        commitException,
                        "Transaction commit failed for {Method}; rolling back the transaction.",
                        method.GetFullName());
                    RollbackAfterCommitFailure(commitException);
                    throw;
                }
                finally
                {
                    while (!TranStack.TryPop(out _))
                    {
                        Thread.Sleep(1);
                    }

                    _tranCount = TranStack.Count;
                }
            }
        }
    }

    public void RollbackTran()
    {
        lock (this)
        {
            _tranCount--;
            GetDbClient().RollbackTran();
        }
    }

    public void RollbackTran(MethodInfo method)
    {
        lock (this)
        {
            string? result = null;
            while (!TranStack.IsEmpty && !TranStack.TryPeek(out result))
            {
                Thread.Sleep(1);
            }

            if (result == method.GetFullName())
            {
                GetDbClient().RollbackTran();
                _logger.LogDebug($"Rollback Transaction");
                Console.WriteLine($"Rollback Transaction");
                while (!TranStack.TryPop(out _))
                {
                    Thread.Sleep(1);
                }

                _tranCount = TranStack.Count;
            }
        }
    }

    public async Task<TResult> ExecuteInSavepointAsync<TResult>(Func<Task<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (TranCount <= 0)
        {
            return await operation();
        }

        var db = GetDbClient();
        var savepointName = $"radish_sp_{Interlocked.Increment(ref _savepointSequence)}";
        var commands = CreateSavepointCommands(db.CurrentConnectionConfig.DbType, savepointName);
        await db.Ado.ExecuteCommandAsync(commands.Create);

        try
        {
            var result = await operation();
            if (commands.Release != null)
            {
                await db.Ado.ExecuteCommandAsync(commands.Release);
            }

            return result;
        }
        catch (Exception operationException)
        {
            try
            {
                await db.Ado.ExecuteCommandAsync(commands.Rollback);
                if (commands.Release != null)
                {
                    await db.Ado.ExecuteCommandAsync(commands.Release);
                }
            }
            catch (Exception rollbackException)
            {
                _logger.LogError(
                    rollbackException,
                    "Rollback to savepoint {SavepointName} failed after operation failure.",
                    savepointName);
                throw new AggregateException(
                    $"Operation and rollback to savepoint {savepointName} both failed.",
                    operationException,
                    rollbackException);
            }

            throw;
        }
    }

    private void RollbackAfterCommitFailure(Exception commitException)
    {
        try
        {
            GetDbClient().RollbackTran();
        }
        catch (Exception rollbackException)
        {
            _logger.LogError(
                rollbackException,
                "Transaction rollback also failed after commit failure.");
            throw new AggregateException(
                "Transaction commit and rollback both failed.",
                commitException,
                rollbackException);
        }
    }

    private static SavepointCommands CreateSavepointCommands(DbType dbType, string savepointName)
    {
        return dbType switch
        {
            DbType.PostgreSQL or DbType.Sqlite or DbType.MySql => new SavepointCommands(
                $"SAVEPOINT {savepointName}",
                $"ROLLBACK TO SAVEPOINT {savepointName}",
                $"RELEASE SAVEPOINT {savepointName}"),
            DbType.SqlServer => new SavepointCommands(
                $"SAVE TRANSACTION {savepointName}",
                $"ROLLBACK TRANSACTION {savepointName}",
                null),
            _ => throw new NotSupportedException(
                $"数据库类型 {dbType} 尚未定义事务保存点语法，不能安全恢复约束冲突。")
        };
    }

    private sealed record SavepointCommands(string Create, string Rollback, string? Release);
}
