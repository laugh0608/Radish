using Radish.Common;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>将 Main 内容赞赏事件可靠、幂等地投影到 Log BalanceChangeLog。</summary>
public sealed class ContentRewardAuditProjectionRepository
    : BaseRepository<BalanceChangeLog>, IContentRewardAuditProjectionRepository
{
    public ContentRewardAuditProjectionRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public async Task ProjectAsync(ContentRewardAuditProjectionCommand command)
    {
        ValidateCommand(command);
        await ExecuteDbOperationAsync(async () =>
        {
            await ProjectCoreAsync(command);
            return true;
        });
    }

    private async Task ProjectCoreAsync(ContentRewardAuditProjectionCommand command)
    {
        DbProtectedClient.Ado.BeginTran();
        try
        {
            var entries = new[] { command.SenderEntry, command.RecipientEntry };
            var keys = entries.Select(item => item.SourceEventKey).ToList();
            var existing = await DbProtectedClient.Queryable<BalanceChangeLog>()
                .SplitTable()
                .Where(item =>
                    item.TenantId == command.TenantId &&
                    item.SourceEventKey != null &&
                    keys.Contains(item.SourceEventKey))
                .ToListAsync();
            foreach (var stored in existing)
            {
                var expected = entries.Single(item => item.SourceEventKey == stored.SourceEventKey);
                EnsureSameEntry(stored, command.CoinTransactionId, expected);
            }

            var existingKeys = existing
                .Where(item => item.SourceEventKey != null)
                .Select(item => item.SourceEventKey!)
                .ToHashSet(StringComparer.Ordinal);
            var missing = entries
                .Where(item => !existingKeys.Contains(item.SourceEventKey))
                .Select(item => new BalanceChangeLog
                {
                    Id = SnowFlakeSingle.Instance.NextId(),
                    TenantId = command.TenantId,
                    UserId = item.UserId,
                    TransactionId = command.CoinTransactionId,
                    ChangeAmount = item.ChangeAmount,
                    BalanceBefore = item.BalanceBefore,
                    BalanceAfter = item.BalanceAfter,
                    ChangeType = item.ChangeType,
                    SourceEventKey = item.SourceEventKey,
                    CreateTime = command.OccurredAtUtc,
                    CreateBy = command.OperatorName,
                    CreateId = command.OperatorId
                })
                .ToList();
            if (missing.Count > 0)
            {
                var affected = await DbProtectedClient.Insertable(missing)
                    .SplitTable()
                    .ExecuteCommandAsync();
                if (affected != missing.Count)
                {
                    throw new InvalidOperationException("内容赞赏审计投影未写入预期行数。");
                }
            }

            DbProtectedClient.Ado.CommitTran();
        }
        catch
        {
            DbProtectedClient.Ado.RollbackTran();
            throw;
        }
    }

    private static void EnsureSameEntry(
        BalanceChangeLog stored,
        long transactionId,
        ContentRewardAuditEntry expected)
    {
        if (stored.TransactionId != transactionId ||
            stored.UserId != expected.UserId ||
            stored.ChangeAmount != expected.ChangeAmount ||
            stored.BalanceBefore != expected.BalanceBefore ||
            stored.BalanceAfter != expected.BalanceAfter ||
            !string.Equals(stored.ChangeType, expected.ChangeType, StringComparison.Ordinal))
        {
            throw new ContentRewardAuditProjectionConflictException();
        }
    }

    private static void ValidateCommand(ContentRewardAuditProjectionCommand command)
    {
        if (command.ContentRewardId <= 0 ||
            command.CoinTransactionId <= 0 ||
            command.SenderEntry.UserId <= 0 ||
            command.RecipientEntry.UserId <= 0 ||
            command.SenderEntry.UserId == command.RecipientEntry.UserId ||
            command.SenderEntry.ChangeAmount != -1 ||
            command.RecipientEntry.ChangeAmount != 1 ||
            string.IsNullOrWhiteSpace(command.SenderEntry.SourceEventKey) ||
            string.IsNullOrWhiteSpace(command.RecipientEntry.SourceEventKey) ||
            command.SenderEntry.SourceEventKey == command.RecipientEntry.SourceEventKey)
        {
            throw new ArgumentException("内容赞赏审计投影命令无效。", nameof(command));
        }
    }
}
