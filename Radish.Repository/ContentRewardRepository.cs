using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Radish.Common;
using Radish.IRepository;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Repository;

/// <summary>内容赞赏、双方余额、TIP 流水、幂等终态与 Outbox 的 Main 原子事务边界。</summary>
public sealed class ContentRewardRepository : BaseRepository<ContentReward>, IContentRewardRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ContentRewardRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public Task<ContentRewardWriteResult> CreateAsync(ContentRewardWriteCommand command)
    {
        ValidateCommand(command);
        return ExecuteDbOperationAsync(() => CreateWithLockAsync(command));
    }

    public async Task<(IReadOnlyList<ContentReward> Items, int Total)> QueryTargetPageAsync(
        long tenantId,
        string targetType,
        long targetId,
        int pageIndex,
        int pageSize)
    {
        var safePageIndex = Math.Max(1, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        return await ExecuteDbOperationAsync(async () =>
        {
            var query = DbProtectedClient.Queryable<ContentReward>()
                .Where(item =>
                    item.TenantId == tenantId &&
                    item.TargetType == targetType &&
                    item.TargetId == targetId);
            var total = await query.CountAsync();
            var items = await query
                .OrderBy(item => item.CreateTime, OrderByType.Desc)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .Skip((safePageIndex - 1) * safePageSize)
                .Take(safePageSize)
                .ToListAsync();
            return ((IReadOnlyList<ContentReward>)items, total);
        });
    }

    public async Task<IReadOnlyList<ContentRewardTargetCount>> QueryTargetCountsAsync(
        long tenantId,
        IReadOnlyCollection<ContentRewardTargetKey> targets)
    {
        var normalized = NormalizeTargets(targets);
        if (normalized.Count == 0)
        {
            return [];
        }

        var postIds = normalized
            .Where(item => item.TargetType == ContentRewardTargetTypes.Post)
            .Select(item => item.TargetId)
            .ToList();
        var commentIds = normalized
            .Where(item => item.TargetType == ContentRewardTargetTypes.Comment)
            .Select(item => item.TargetId)
            .ToList();
        return await ExecuteDbOperationAsync(async () =>
        {
            var query = DbProtectedClient.Queryable<ContentReward>()
                .Where(item =>
                    item.TenantId == tenantId &&
                    ((item.TargetType == ContentRewardTargetTypes.Post && postIds.Contains(item.TargetId)) ||
                     (item.TargetType == ContentRewardTargetTypes.Comment && commentIds.Contains(item.TargetId))));
            var rows = await query
                .GroupBy(item => new { item.TargetType, item.TargetId })
                .Select(item => new
                {
                    item.TargetType,
                    item.TargetId,
                    TotalCount = SqlFunc.AggregateCount(item.Id)
                })
                .ToListAsync();
            return (IReadOnlyList<ContentRewardTargetCount>)rows
                .Select(item => new ContentRewardTargetCount(
                    item.TargetType,
                    item.TargetId,
                    item.TotalCount))
                .ToList();
        });
    }

    public async Task<IReadOnlySet<ContentRewardTargetKey>> QueryRewardedTargetsAsync(
        long tenantId,
        long senderUserId,
        IReadOnlyCollection<ContentRewardTargetKey> targets)
    {
        if (senderUserId <= 0)
        {
            return new HashSet<ContentRewardTargetKey>();
        }

        var normalized = NormalizeTargets(targets);
        if (normalized.Count == 0)
        {
            return new HashSet<ContentRewardTargetKey>();
        }

        var postIds = normalized
            .Where(item => item.TargetType == ContentRewardTargetTypes.Post)
            .Select(item => item.TargetId)
            .ToList();
        var commentIds = normalized
            .Where(item => item.TargetType == ContentRewardTargetTypes.Comment)
            .Select(item => item.TargetId)
            .ToList();
        var rows = await ExecuteDbOperationAsync(() => DbProtectedClient.Queryable<ContentReward>()
            .Where(item =>
                item.TenantId == tenantId &&
                item.SenderUserId == senderUserId &&
                ((item.TargetType == ContentRewardTargetTypes.Post && postIds.Contains(item.TargetId)) ||
                 (item.TargetType == ContentRewardTargetTypes.Comment && commentIds.Contains(item.TargetId))))
            .Select(item => new ContentReward
            {
                TargetType = item.TargetType,
                TargetId = item.TargetId
            })
            .ToListAsync());
        return rows
            .Select(item => new ContentRewardTargetKey(item.TargetType, item.TargetId))
            .ToHashSet();
    }

    private async Task<ContentRewardWriteResult> CreateWithLockAsync(ContentRewardWriteCommand command)
    {
        var existingReward = command.RecoverExpiredProcessing
            ? await QueryExistingRewardAsync(command)
            : null;
        var expectedRecipientUserId = existingReward?.RecipientUserId
                                      ?? (await ResolveTargetAsync(
                                          command.TenantId,
                                          command.TargetType,
                                          command.TargetId)).RecipientUserId;
        var sqliteLock = await UserInteractionTransactionLock.EnterSqliteAsync(
            DbProtectedClient,
            command.TenantId,
            command.SenderUserId,
            expectedRecipientUserId);
        try
        {
            return await CreateCoreAsync(command, expectedRecipientUserId);
        }
        finally
        {
            UserInteractionTransactionLock.ExitSqlite(sqliteLock);
        }
    }

    private async Task<ContentRewardWriteResult> CreateCoreAsync(
        ContentRewardWriteCommand command,
        long expectedRecipientUserId)
    {
        DbProtectedClient.Ado.BeginTran();
        try
        {
            await AcquirePostgreSqlLockAsync(
                $"content-reward-operation:{command.TenantId}:{command.SenderUserId}:{command.IdempotencyRecordId}");
            await AcquirePostgreSqlLockAsync(
                $"content-reward-sender:{command.TenantId}:{command.SenderUserId}");

            var idempotencyRecord = await ValidateIdempotencyAsync(command);
            var existingReward = await QueryExistingRewardAsync(command);
            if (command.RecoverExpiredProcessing && existingReward != null)
            {
                if (existingReward.RecipientUserId != expectedRecipientUserId)
                {
                    throw new ContentRewardConcurrentConflictException();
                }

                await UserInteractionTransactionLock.AcquirePostgreSqlAsync(
                    DbProtectedClient,
                    command.TenantId,
                    command.SenderUserId,
                    existingReward.RecipientUserId);
                var recovered = await RecoverCommittedResultAsync(
                    command,
                    idempotencyRecord,
                    existingReward);
                DbProtectedClient.Ado.CommitTran();
                return recovered;
            }

            var initialTarget = await ResolveTargetAsync(command.TenantId, command.TargetType, command.TargetId);
            if (initialTarget.RecipientUserId != expectedRecipientUserId)
            {
                throw new ContentRewardConcurrentConflictException();
            }
            await UserInteractionTransactionLock.AcquirePostgreSqlAsync(
                DbProtectedClient,
                command.TenantId,
                command.SenderUserId,
                initialTarget.RecipientUserId);

            var target = await ResolveTargetAsync(command.TenantId, command.TargetType, command.TargetId);
            if (target.RecipientUserId != initialTarget.RecipientUserId)
            {
                throw new ContentRewardConcurrentConflictException();
            }
            await ValidateUsersAndInteractionAsync(command, target.RecipientUserId);

            existingReward = await QueryExistingRewardAsync(command);
            if (existingReward != null)
            {
                throw new ContentRewardAlreadyExistsException();
            }

            await ValidateDailyLimitsAsync(command, target.RecipientUserId);
            await EnsureBalanceExistsAsync(command.TenantId, command.SenderUserId, command.SenderName, command.NowUtc);
            await EnsureBalanceExistsAsync(command.TenantId, target.RecipientUserId, command.SenderName, command.NowUtc);
            var balances = await DbProtectedClient.Queryable<UserBalance>()
                .Where(item =>
                    (item.UserId == command.SenderUserId || item.UserId == target.RecipientUserId) &&
                    !item.IsDeleted)
                .OrderBy(item => item.UserId)
                .ToListAsync();
            var senderBalance = balances.FirstOrDefault(item => item.UserId == command.SenderUserId);
            var recipientBalance = balances.FirstOrDefault(item => item.UserId == target.RecipientUserId);
            if (senderBalance == null || recipientBalance == null)
            {
                throw new ContentRewardAccountUnavailableException();
            }
            if (senderBalance.Balance < 1)
            {
                throw new ContentRewardInsufficientBalanceException();
            }

            var senderBefore = senderBalance.Balance;
            var recipientBefore = recipientBalance.Balance;
            var rewardId = SnowFlakeSingle.Instance.NextId();
            var transactionId = SnowFlakeSingle.Instance.NextId();
            var transactionNo = $"TXN_{transactionId}";
            var reward = new ContentReward
            {
                Id = rewardId,
                TenantId = command.TenantId,
                TargetType = command.TargetType,
                TargetId = command.TargetId,
                PostId = target.PostId,
                SenderUserId = command.SenderUserId,
                RecipientUserId = target.RecipientUserId,
                Amount = 1,
                ReasonCode = command.ReasonCode,
                CoinTransactionId = transactionId,
                CreateTime = command.NowUtc,
                CreateBy = command.SenderName,
                CreateId = command.SenderUserId
            };
            await EnsureSingleRowAsync(
                DbProtectedClient.Insertable(reward).ExecuteCommandAsync(),
                "创建内容赞赏");

            await EnsureSingleRowAsync(
                DbProtectedClient.Insertable(new CoinTransaction
                {
                    Id = transactionId,
                    TransactionNo = transactionNo,
                    FromUserId = command.SenderUserId,
                    ToUserId = target.RecipientUserId,
                    Amount = 1,
                    Fee = 0,
                    TransactionType = "TIP",
                    Status = "SUCCESS",
                    BusinessType = BusinessType.ContentReward,
                    BusinessId = rewardId,
                    RewardBusinessKey = null,
                    Remark = command.ReasonCode,
                    TenantId = command.TenantId,
                    CreateTime = command.NowUtc,
                    CreateBy = command.SenderName,
                    CreateId = command.SenderUserId
                }).ExecuteCommandAsync(),
                "创建内容赞赏交易");

            foreach (var balance in balances.OrderBy(item => item.UserId))
            {
                if (balance.UserId == command.SenderUserId)
                {
                    await UpdateBalanceAsync(
                        balance,
                        balance.Balance - 1,
                        balance.TotalEarned,
                        balance.TotalSpent + 1,
                        balance.TotalTransferredIn,
                        balance.TotalTransferredOut + 1,
                        command);
                }
                else
                {
                    await UpdateBalanceAsync(
                        balance,
                        balance.Balance + 1,
                        balance.TotalEarned + 1,
                        balance.TotalSpent,
                        balance.TotalTransferredIn + 1,
                        balance.TotalTransferredOut,
                        command);
                }
            }

            var totalCount = await DbProtectedClient.Queryable<ContentReward>()
                .Where(item =>
                    item.TenantId == command.TenantId &&
                    item.TargetType == command.TargetType &&
                    item.TargetId == command.TargetId)
                .CountAsync();
            var response = new ContentRewardMutationVo
            {
                VoRewardId = rewardId,
                VoTargetType = command.TargetType,
                VoTargetId = command.TargetId,
                VoReasonCode = command.ReasonCode,
                VoTotalCount = totalCount,
                VoViewerRewarded = true,
                VoSenderAvailableBalance = senderBefore - 1,
                VoTransactionNo = transactionNo
            };
            await CompleteIdempotencyAsync(command, rewardId, transactionNo, response);
            await AddAuditOutboxAsync(
                command,
                rewardId,
                transactionId,
                target.RecipientUserId,
                senderBefore,
                recipientBefore);
            await AddNotificationOutboxAsync(command, reward, target);

            DbProtectedClient.Ado.CommitTran();
            return new ContentRewardWriteResult(
                reward,
                transactionNo,
                senderBefore - 1,
                totalCount);
        }
        catch (Exception exception)
        {
            DbProtectedClient.Ado.RollbackTran();
            if (exception is ContentRewardTargetUnavailableException or
                ContentRewardSelfNotAllowedException or
                ContentRewardAlreadyExistsException or
                ContentRewardInsufficientBalanceException or
                ContentRewardDailyLimitExceededException or
                ContentRewardAccountUnavailableException or
                ContentRewardInteractionUnavailableException or
                ContentRewardIdempotencyStateException or
                ContentRewardConcurrentConflictException or
                ContentRewardRecoveryUnavailableException or
                ContentRewardRelationshipUnavailableException)
            {
                throw;
            }

            if (RepositorySqlHelper.IsUniqueConstraintException(exception))
            {
                throw new ContentRewardAlreadyExistsException();
            }
            if (IsDatabaseConcurrencyException(exception))
            {
                throw new ContentRewardConcurrentConflictException();
            }

            throw;
        }
    }

    private async Task<TargetSnapshot> ResolveTargetAsync(long tenantId, string targetType, long targetId)
    {
        if (targetType == ContentRewardTargetTypes.Post)
        {
            var post = await DbProtectedClient.Queryable<Post>()
                .Where(item =>
                    item.Id == targetId &&
                    item.TenantId == tenantId &&
                    item.IsPublished &&
                    item.IsEnabled &&
                    !item.IsDeleted)
                .FirstAsync();
            if (post == null)
            {
                throw new ContentRewardTargetUnavailableException();
            }

            return new TargetSnapshot(
                post.Id,
                post.PublicId,
                post.Title,
                post.AuthorId,
                null);
        }

        var comment = await DbProtectedClient.Queryable<Comment>()
            .Where(item =>
                item.Id == targetId &&
                item.TenantId == tenantId &&
                item.IsEnabled &&
                !item.IsDeleted)
            .FirstAsync();
        if (comment == null)
        {
            throw new ContentRewardTargetUnavailableException();
        }

        var parentPost = await DbProtectedClient.Queryable<Post>()
            .Where(item =>
                item.Id == comment.PostId &&
                item.TenantId == tenantId &&
                item.IsPublished &&
                item.IsEnabled &&
                !item.IsDeleted)
            .FirstAsync();
        if (parentPost == null)
        {
            throw new ContentRewardTargetUnavailableException();
        }

        return new TargetSnapshot(
            parentPost.Id,
            parentPost.PublicId,
            parentPost.Title,
            comment.AuthorId,
            comment.Id);
    }

    private async Task<ContentReward?> QueryExistingRewardAsync(ContentRewardWriteCommand command)
    {
        return await DbProtectedClient.Queryable<ContentReward>()
            .Where(item =>
                item.TenantId == command.TenantId &&
                item.SenderUserId == command.SenderUserId &&
                item.TargetType == command.TargetType &&
                item.TargetId == command.TargetId)
            .FirstAsync();
    }

    private async Task<OperationIdempotencyRecord> ValidateIdempotencyAsync(
        ContentRewardWriteCommand command)
    {
        var record = await DbProtectedClient.Queryable<OperationIdempotencyRecord>()
            .Where(item =>
                item.Id == command.IdempotencyRecordId &&
                item.TenantId == command.TenantId &&
                item.UserId == command.SenderUserId &&
                item.OperationType == OperationIdempotencyOperationTypes.ContentReward)
            .FirstAsync();
        if (record == null ||
            record.Status != OperationIdempotencyStatuses.Processing ||
            !string.Equals(record.RequestHash, command.IdempotencyRequestHash, StringComparison.Ordinal) ||
            (command.RecoverExpiredProcessing && record.ExpiresAt > command.NowUtc))
        {
            throw new ContentRewardIdempotencyStateException();
        }

        return record;
    }

    private async Task<ContentRewardWriteResult> RecoverCommittedResultAsync(
        ContentRewardWriteCommand command,
        OperationIdempotencyRecord idempotencyRecord,
        ContentReward reward)
    {
        var auditKey = $"task:content-reward:{reward.Id}:audit";
        var notificationKey = $"task:content-reward:{reward.Id}:notification";
        var outbox = await DbProtectedClient.Queryable<ReliableOutboxMessage>()
            .Where(item =>
                item.TenantId == command.TenantId &&
                (item.IdempotencyKey == auditKey || item.IdempotencyKey == notificationKey))
            .ToListAsync();
        var auditOutbox = outbox.SingleOrDefault(item =>
            item.TaskType == ReliableTaskTypes.ContentRewardAuditProjection &&
            item.IdempotencyKey == auditKey);
        var notificationOutbox = outbox.SingleOrDefault(item =>
            item.TaskType == ReliableTaskTypes.NotificationRequested &&
            item.IdempotencyKey == notificationKey);
        var transaction = await DbProtectedClient.Queryable<CoinTransaction>()
            .Where(item =>
                item.Id == reward.CoinTransactionId &&
                item.TenantId == command.TenantId)
            .FirstAsync();
        if (auditOutbox == null ||
            notificationOutbox == null ||
            transaction == null ||
            transaction.FromUserId != reward.SenderUserId ||
            transaction.ToUserId != reward.RecipientUserId ||
            transaction.Amount != 1 ||
            transaction.TransactionType != "TIP" ||
            transaction.Status != "SUCCESS" ||
            transaction.BusinessType != BusinessType.ContentReward ||
            transaction.BusinessId != reward.Id)
        {
            throw new ContentRewardRecoveryUnavailableException();
        }

        ContentRewardAuditProjectionTaskPayload? auditPayload;
        try
        {
            auditPayload = JsonSerializer.Deserialize<ContentRewardAuditProjectionTaskPayload>(
                auditOutbox.PayloadJson,
                JsonOptions);
        }
        catch (JsonException)
        {
            throw new ContentRewardRecoveryUnavailableException();
        }

        if (auditPayload == null ||
            auditPayload.TenantId != command.TenantId ||
            auditPayload.ContentRewardId != reward.Id ||
            auditPayload.IdempotencyRecordId != idempotencyRecord.Id ||
            auditPayload.CoinTransactionId != transaction.Id ||
            auditPayload.SenderEntry.UserId != command.SenderUserId ||
            auditPayload.SenderEntry.ChangeAmount != -1 ||
            auditPayload.SenderEntry.BalanceAfter != auditPayload.SenderEntry.BalanceBefore - 1)
        {
            if (auditPayload?.IdempotencyRecordId != idempotencyRecord.Id)
            {
                throw new ContentRewardAlreadyExistsException();
            }

            throw new ContentRewardRecoveryUnavailableException();
        }

        var totalCount = await DbProtectedClient.Queryable<ContentReward>()
            .Where(item =>
                item.TenantId == command.TenantId &&
                item.TargetType == command.TargetType &&
                item.TargetId == command.TargetId)
            .CountAsync();
        var response = new ContentRewardMutationVo
        {
            VoRewardId = reward.Id,
            VoTargetType = reward.TargetType,
            VoTargetId = reward.TargetId,
            VoReasonCode = reward.ReasonCode,
            VoTotalCount = totalCount,
            VoViewerRewarded = true,
            VoSenderAvailableBalance = auditPayload.SenderEntry.BalanceAfter,
            VoTransactionNo = transaction.TransactionNo
        };
        await CompleteIdempotencyAsync(
            command,
            reward.Id,
            transaction.TransactionNo,
            response);
        return new ContentRewardWriteResult(
            reward,
            transaction.TransactionNo,
            auditPayload.SenderEntry.BalanceAfter,
            totalCount);
    }

    private async Task ValidateUsersAndInteractionAsync(
        ContentRewardWriteCommand command,
        long recipientUserId)
    {
        if (command.SenderUserId == recipientUserId)
        {
            throw new ContentRewardSelfNotAllowedException();
        }

        var users = await DbProtectedClient.Queryable<User>()
            .Where(item =>
                item.TenantId == command.TenantId &&
                (item.Id == command.SenderUserId || item.Id == recipientUserId) &&
                item.IsEnable &&
                !item.IsDeleted &&
                item.StatusCode == (int)UserStatusCodeEnum.Normal)
            .Select(item => item.Id)
            .ToListAsync();
        if (users.Distinct().Count() != 2)
        {
            throw new ContentRewardAccountUnavailableException();
        }

        bool hasBarrier;
        try
        {
            hasBarrier = await DbProtectedClient.Queryable<UserBlock>()
                .Where(item =>
                    item.TenantId == command.TenantId &&
                    !item.IsDeleted &&
                    ((item.BlockerUserId == command.SenderUserId && item.BlockedUserId == recipientUserId) ||
                     (item.BlockerUserId == recipientUserId && item.BlockedUserId == command.SenderUserId)))
                .AnyAsync();
        }
        catch (Exception exception)
        {
            throw new ContentRewardRelationshipUnavailableException(exception);
        }
        if (hasBarrier)
        {
            throw new ContentRewardInteractionUnavailableException();
        }
    }

    private async Task ValidateDailyLimitsAsync(
        ContentRewardWriteCommand command,
        long recipientUserId)
    {
        var totalCount = await DbProtectedClient.Queryable<ContentReward>()
            .Where(item =>
                item.TenantId == command.TenantId &&
                item.SenderUserId == command.SenderUserId &&
                item.CreateTime >= command.BusinessDayStartUtc &&
                item.CreateTime < command.BusinessDayEndUtc)
            .CountAsync();
        if (totalCount >= command.DailyTotalLimit)
        {
            throw new ContentRewardDailyLimitExceededException();
        }

        var recipientCount = await DbProtectedClient.Queryable<ContentReward>()
            .Where(item =>
                item.TenantId == command.TenantId &&
                item.SenderUserId == command.SenderUserId &&
                item.RecipientUserId == recipientUserId &&
                item.CreateTime >= command.BusinessDayStartUtc &&
                item.CreateTime < command.BusinessDayEndUtc)
            .CountAsync();
        if (recipientCount >= command.DailyRecipientLimit)
        {
            throw new ContentRewardDailyLimitExceededException();
        }
    }

    private async Task EnsureBalanceExistsAsync(
        long tenantId,
        long userId,
        string operatorName,
        DateTime nowUtc)
    {
        var entityInfo = DbProtectedClient.EntityMaintenance.GetEntityInfo<UserBalance>();
        var physicalTableName = RepositorySqlHelper.ResolvePhysicalTableName(
            DbProtectedClient,
            entityInfo.DbTableName);
        var table = RepositorySqlHelper.QuoteIdentifier(physicalTableName);
        var physicalColumnNames = DbProtectedClient.DbMaintenance
            .GetColumnInfosByTableName(physicalTableName, false)
            .Select(column => column.DbColumnName)
            .ToList();
        string Column(string propertyName)
        {
            var configuredColumnName = entityInfo.Columns
                .First(column => string.Equals(column.PropertyName, propertyName, StringComparison.Ordinal))
                .DbColumnName;
            return RepositorySqlHelper.QuoteIdentifier(
                RepositorySqlHelper.ResolvePhysicalColumnName(
                    physicalColumnNames,
                    physicalTableName,
                    configuredColumnName));
        }

        var sql = $"INSERT INTO {table} (" +
                  $"{Column(nameof(UserBalance.Id))}, {Column(nameof(UserBalance.UserId))}, " +
                  $"{Column(nameof(UserBalance.Balance))}, {Column(nameof(UserBalance.FrozenBalance))}, " +
                  $"{Column(nameof(UserBalance.TotalEarned))}, {Column(nameof(UserBalance.TotalSpent))}, " +
                  $"{Column(nameof(UserBalance.TotalTransferredIn))}, {Column(nameof(UserBalance.TotalTransferredOut))}, " +
                  $"{Column(nameof(UserBalance.Version))}, {Column(nameof(UserBalance.TenantId))}, " +
                  $"{Column(nameof(UserBalance.CreateTime))}, {Column(nameof(UserBalance.CreateBy))}, " +
                  $"{Column(nameof(UserBalance.CreateId))}, {Column(nameof(UserBalance.IsDeleted))}) VALUES " +
                  "(@Id, @UserId, 0, 0, 0, 0, 0, 0, 0, @TenantId, @CreateTime, @CreateBy, @CreateId, @IsDeleted) " +
                  $"ON CONFLICT ({Column(nameof(UserBalance.UserId))}) DO NOTHING";
        await DbProtectedClient.Ado.ExecuteCommandAsync(
            sql,
            new SugarParameter("@Id", SnowFlakeSingle.Instance.NextId()),
            new SugarParameter("@UserId", userId),
            new SugarParameter("@TenantId", tenantId),
            new SugarParameter("@CreateTime", nowUtc),
            new SugarParameter("@CreateBy", operatorName),
            new SugarParameter("@CreateId", userId),
            new SugarParameter("@IsDeleted", false));
    }

    private async Task UpdateBalanceAsync(
        UserBalance balance,
        long newBalance,
        long totalEarned,
        long totalSpent,
        long totalTransferredIn,
        long totalTransferredOut,
        ContentRewardWriteCommand command)
    {
        var affected = await DbProtectedClient.Updateable<UserBalance>()
            .SetColumns(item => new UserBalance
            {
                Balance = newBalance,
                TotalEarned = totalEarned,
                TotalSpent = totalSpent,
                TotalTransferredIn = totalTransferredIn,
                TotalTransferredOut = totalTransferredOut,
                Version = balance.Version + 1,
                ModifyTime = command.NowUtc,
                ModifyBy = command.SenderName,
                ModifyId = command.SenderUserId
            })
            .Where(item =>
                item.Id == balance.Id &&
                item.Version == balance.Version &&
                !item.IsDeleted)
            .ExecuteCommandAsync();
        if (affected != 1)
        {
            throw new ContentRewardConcurrentConflictException();
        }
    }

    private async Task CompleteIdempotencyAsync(
        ContentRewardWriteCommand command,
        long rewardId,
        string transactionNo,
        ContentRewardMutationVo response)
    {
        var affected = await DbProtectedClient.Updateable<OperationIdempotencyRecord>()
            .SetColumns(item => new OperationIdempotencyRecord
            {
                Status = OperationIdempotencyStatuses.Succeeded,
                ResourceType = OperationIdempotencyResourceTypes.ContentReward,
                ResourceId = rewardId,
                ResourceNo = transactionNo,
                ResponsePayload = JsonSerializer.Serialize(response, JsonOptions),
                ErrorCode = null,
                ErrorMessage = null,
                CompleteTime = command.NowUtc,
                ExpiresAt = command.NowUtc.AddHours(24),
                ModifyTime = command.NowUtc,
                ModifyBy = "System",
                ModifyId = 0
            })
            .Where(item =>
                item.Id == command.IdempotencyRecordId &&
                item.TenantId == command.TenantId &&
                item.UserId == command.SenderUserId &&
                item.OperationType == OperationIdempotencyOperationTypes.ContentReward &&
                item.Status == OperationIdempotencyStatuses.Processing &&
                item.RequestHash == command.IdempotencyRequestHash)
            .ExecuteCommandAsync();
        if (affected != 1)
        {
            throw new ContentRewardIdempotencyStateException();
        }
    }

    private async Task AddAuditOutboxAsync(
        ContentRewardWriteCommand command,
        long rewardId,
        long transactionId,
        long recipientUserId,
        long senderBefore,
        long recipientBefore)
    {
        var payload = new ContentRewardAuditProjectionTaskPayload(
            command.TenantId,
            rewardId,
            command.IdempotencyRecordId,
            transactionId,
            command.NowUtc,
            command.SenderName,
            command.SenderUserId,
            new ContentRewardAuditProjectionEntryPayload(
                command.SenderUserId,
                -1,
                senderBefore,
                senderBefore - 1,
                "TRANSFER_OUT",
                $"content-reward:{transactionId}:{command.SenderUserId}:out"),
            new ContentRewardAuditProjectionEntryPayload(
                recipientUserId,
                1,
                recipientBefore,
                recipientBefore + 1,
                "TRANSFER_IN",
                $"content-reward:{transactionId}:{recipientUserId}:in"));
        await InsertOutboxAsync(
            command,
            ReliableTaskTypes.ContentRewardAuditProjection,
            $"task:content-reward:{rewardId}:audit",
            rewardId,
            payload);
    }

    private async Task AddNotificationOutboxAsync(
        ContentRewardWriteCommand command,
        ContentReward reward,
        TargetSnapshot target)
    {
        var notificationId = SnowFlakeSingle.Instance.NextId();
        var notification = new CreateNotificationDto
        {
            NotificationId = notificationId,
            BusinessKey = $"content-reward:{reward.Id}",
            Type = NotificationType.ContentRewardReceived,
            TemplateArguments = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["actorName"] = command.SenderName,
                ["reasonCode"] = command.ReasonCode,
                ["targetTitle"] = target.TargetTitle
            },
            TargetKind = NotificationTargetKind.ForumPost,
            Target = new NotificationTargetData
            {
                PostId = target.PostId,
                PostPublicId = target.PostPublicId,
                CommentId = target.CommentId
            },
            OccurredAtUtc = command.NowUtc,
            Title = "收到内容赞赏",
            Content = $"{command.SenderName} 送出了 1 胡萝卜",
            BusinessType = BusinessType.ContentReward,
            BusinessId = reward.Id,
            TriggerId = command.SenderUserId,
            TriggerName = command.SenderName,
            ReceiverUserIds = [reward.RecipientUserId],
            TenantId = command.TenantId
        };
        await InsertOutboxAsync(
            command,
            ReliableTaskTypes.NotificationRequested,
            $"task:content-reward:{reward.Id}:notification",
            reward.Id,
            new NotificationRequestedTaskPayload(notification));
    }

    private async Task InsertOutboxAsync<TPayload>(
        ContentRewardWriteCommand command,
        string taskType,
        string idempotencyKey,
        long rewardId,
        TPayload payload)
    {
        await EnsureSingleRowAsync(
            DbProtectedClient.Insertable(new ReliableOutboxMessage
            {
                Id = SnowFlakeSingle.Instance.NextId(),
                TenantId = command.TenantId,
                TaskType = taskType,
                SchemaVersion = 1,
                IdempotencyKey = idempotencyKey,
                AggregateType = BusinessType.ContentReward,
                AggregateId = rewardId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
                Status = ReliableOutboxStatuses.Pending,
                MaxAttempts = 6,
                OccurredAtUtc = command.NowUtc,
                AvailableAtUtc = command.NowUtc,
                CreateTime = command.NowUtc
            }).ExecuteCommandAsync(),
            $"创建 {taskType} Outbox");
    }

    private async Task AcquirePostgreSqlLockAsync(string identity)
    {
        if (DbProtectedClient.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(hash);
        await DbProtectedClient.Ado.ExecuteCommandAsync(
            "SELECT pg_advisory_xact_lock(@LockKey)",
            new SugarParameter("@LockKey", lockKey));
    }

    private static async Task EnsureSingleRowAsync(Task<int> operation, string action)
    {
        if (await operation != 1)
        {
            throw new InvalidOperationException($"{action}未影响预期的一行。");
        }
    }

    private static bool IsDatabaseConcurrencyException(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("40001", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("40P01", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("database is locked", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("database is busy", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static List<ContentRewardTargetKey> NormalizeTargets(
        IReadOnlyCollection<ContentRewardTargetKey> targets)
    {
        return targets
            .Where(item =>
                ContentRewardTargetTypes.All.Contains(item.TargetType) &&
                item.TargetId > 0)
            .Distinct()
            .ToList();
    }

    private static void ValidateCommand(ContentRewardWriteCommand command)
    {
        if (command.SenderUserId <= 0 ||
            command.TargetId <= 0 ||
            command.IdempotencyRecordId <= 0 ||
            !ContentRewardTargetTypes.All.Contains(command.TargetType) ||
            !ContentRewardReasonCodes.All.Contains(command.ReasonCode) ||
            command.DailyTotalLimit <= 0 ||
            command.DailyRecipientLimit <= 0 ||
            command.DailyRecipientLimit > command.DailyTotalLimit ||
            command.BusinessDayStartUtc >= command.BusinessDayEndUtc ||
            string.IsNullOrWhiteSpace(command.IdempotencyRequestHash))
        {
            throw new ArgumentException("内容赞赏写入命令无效。", nameof(command));
        }
    }

    private sealed record TargetSnapshot(
        long PostId,
        string? PostPublicId,
        string TargetTitle,
        long RecipientUserId,
        long? CommentId);
}
