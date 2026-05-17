using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Radish.Repository.UnitOfWorks;

public class UnitOfWorks : IDisposable
{
    public ILogger Logger { get; set; } = null!;
    public ISqlSugarClient Db { get; internal set; } = null!;

    public ITenant Tenant { get; internal set; } = null!;

    public bool IsTran { get; internal set; }

    public bool IsCommit { get; internal set; }

    public bool IsClose { get; internal set; }

    public void Dispose()
    {
        if (this.IsTran && !this.IsCommit)
        {
            Logger.LogDebug("UnitOfWork RollbackTran");
            this.Tenant.RollbackTran();
        }

        if (this.Db.Ado.Transaction != null || this.IsClose)
            return;
        this.Db.Close();
    }

    public bool Commit()
    {
        if (this.IsTran && !this.IsCommit)
        {
            Logger.LogDebug("UnitOfWork CommitTran");
            this.Tenant.CommitTran();
            this.IsCommit = true;
        }

        if (this.Db.Ado.Transaction == null && !this.IsClose)
        {
            this.Db.Close();
            this.IsClose = true;
        }

        return this.IsCommit;
    }
}
