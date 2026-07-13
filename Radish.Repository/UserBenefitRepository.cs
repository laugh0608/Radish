using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Radish.Common;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Repository;

/// <summary>持续权益选择与失效的专属仓储。</summary>
public sealed class UserBenefitRepository : BaseRepository<UserBenefit>, IUserBenefitRepository
{
    public UserBenefitRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public Task<List<UserActiveBenefit>> GetActiveSelectionsAsync(long userId)
    {
        return CreateTenantQueryableFor<UserActiveBenefit>()
            .Where(selection => selection.UserId == userId)
            .ToListAsync();
    }

    public async Task<UserActiveBenefit?> GetActiveSelectionAsync(long userId, BenefitType benefitType)
    {
        return await CreateTenantQueryableFor<UserActiveBenefit>()
            .Where(selection => selection.UserId == userId && selection.BenefitType == benefitType)
            .FirstAsync();
    }

    public async Task<List<long>> GetDueBenefitIdsAsync(DateTime nowUtc, int take)
    {
        var effectiveTake = Math.Clamp(take, 1, 500);
        // 系统过期任务必须覆盖所有租户，不能继承请求上下文的租户过滤。
        var candidates = await DbProtectedClient.Queryable<UserBenefit>()
            .Where(benefit =>
                !benefit.IsDeleted &&
                benefit.RevokedAt == null &&
                !benefit.IsExpired &&
                benefit.ExpiresAt != null)
            .OrderBy(benefit => benefit.ExpiresAt)
            .Take(effectiveTake)
            .ToListAsync();
        return candidates
            .Where(benefit => benefit.ExpiresAt!.Value <= nowUtc)
            .Select(benefit => benefit.Id)
            .ToList();
    }

    public Task<UserBenefitPersistenceResult> ActivateAsync(
        long userId,
        long benefitId,
        long operatorId,
        string operatorName,
        DateTime nowUtc)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var benefit = await QueryOwnedBenefitAsync(userId, benefitId)
                ?? throw new InvalidOperationException("权益不存在");
            EnsureBenefitCanBeActivated(benefit, nowUtc);

            var current = await QuerySelectionAsync(benefit.TenantId, userId, benefit.BenefitType);
            if (current?.BenefitId == benefitId)
            {
                return new UserBenefitPersistenceResult(benefit, false, benefitId, benefitId);
            }

            var previousBenefitId = current?.BenefitId;
            await UpsertSelectionAsync(benefit, operatorId, operatorName, nowUtc);

            await DbProtectedClient.Updateable<UserBenefit>()
                .SetColumns(item => new UserBenefit
                {
                    IsActive = false,
                    ModifyTime = nowUtc,
                    ModifyBy = operatorName,
                    ModifyId = operatorId
                })
                .Where(item =>
                    item.TenantId == benefit.TenantId &&
                    item.UserId == userId &&
                    item.BenefitType == benefit.BenefitType &&
                    item.Id != benefitId &&
                    item.IsActive)
                .ExecuteCommandAsync();

            var activated = await DbProtectedClient.Updateable<UserBenefit>()
                .SetColumns(item => new UserBenefit
                {
                    IsActive = true,
                    ActivatedAt = nowUtc,
                    ModifyTime = nowUtc,
                    ModifyBy = operatorName,
                    ModifyId = operatorId
                })
                .Where(item =>
                    item.Id == benefitId &&
                    item.TenantId == benefit.TenantId &&
                    item.UserId == userId &&
                    !item.IsDeleted &&
                    item.RevokedAt == null &&
                    item.ModifyTime == benefit.ModifyTime)
                .ExecuteCommandAsync();
            if (activated != 1)
            {
                throw new InvalidOperationException("权益状态已变化，请刷新后重试");
            }

            benefit.IsActive = true;
            benefit.ActivatedAt = nowUtc;
            await InsertBenefitOperationAsync(
                benefit,
                ShopEntitlementOperationTypes.Activate,
                previousBenefitId,
                null,
                operatorId,
                operatorName,
                nowUtc,
                $"benefit-activate:{SnowFlakeSingle.Instance.NextId()}");
            return new UserBenefitPersistenceResult(benefit, true, benefitId, previousBenefitId);
        });
    }

    public Task<UserBenefitPersistenceResult> DeactivateAsync(
        long userId,
        long benefitId,
        long operatorId,
        string operatorName,
        DateTime nowUtc)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var benefit = await QueryOwnedBenefitAsync(userId, benefitId)
                ?? throw new InvalidOperationException("权益不存在");
            var deleted = await DbProtectedClient.Deleteable<UserActiveBenefit>()
                .Where(selection =>
                    selection.TenantId == benefit.TenantId &&
                    selection.UserId == userId &&
                    selection.BenefitType == benefit.BenefitType &&
                    selection.BenefitId == benefitId)
                .ExecuteCommandAsync();
            if (deleted <= 0)
            {
                var current = await QuerySelectionAsync(benefit.TenantId, userId, benefit.BenefitType);
                return new UserBenefitPersistenceResult(benefit, false, current?.BenefitId, benefitId);
            }

            await ClearLegacyActiveFlagAsync(benefit, operatorId, operatorName, nowUtc);
            benefit.IsActive = false;
            await InsertBenefitOperationAsync(
                benefit,
                ShopEntitlementOperationTypes.Deactivate,
                null,
                null,
                operatorId,
                operatorName,
                nowUtc,
                $"benefit-deactivate:{SnowFlakeSingle.Instance.NextId()}");
            return new UserBenefitPersistenceResult(benefit, true, null, benefitId);
        });
    }

    public Task<UserBenefitPersistenceResult> RevokeAsync(
        long benefitId,
        string reason,
        long operatorId,
        string operatorName,
        DateTime nowUtc)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var benefit = await CreateTenantQueryableFor<UserBenefit>()
                .Where(item => item.Id == benefitId && !item.IsDeleted)
                .FirstAsync()
                ?? throw new InvalidOperationException("权益不存在");
            if (benefit.RevokedAt.HasValue)
            {
                return new UserBenefitPersistenceResult(benefit, false, null, benefit.Id);
            }

            var updated = await DbProtectedClient.Updateable<UserBenefit>()
                .SetColumns(item => new UserBenefit
                {
                    RevokedAt = nowUtc,
                    RevokedById = operatorId,
                    RevokedByName = operatorName,
                    RevocationReason = reason,
                    IsActive = false,
                    ModifyTime = nowUtc,
                    ModifyBy = operatorName,
                    ModifyId = operatorId
                })
                .Where(item =>
                    item.Id == benefitId &&
                    item.TenantId == benefit.TenantId &&
                    !item.IsDeleted &&
                    item.RevokedAt == null)
                .ExecuteCommandAsync();
            if (updated != 1)
            {
                throw new InvalidOperationException("权益状态已变化，请刷新后重试");
            }

            await DeleteSelectionAsync(benefit);
            benefit.RevokedAt = nowUtc;
            benefit.RevokedById = operatorId;
            benefit.RevokedByName = operatorName;
            benefit.RevocationReason = reason;
            benefit.IsActive = false;
            await InsertBenefitOperationAsync(
                benefit,
                ShopEntitlementOperationTypes.Revoke,
                null,
                reason,
                operatorId,
                operatorName,
                nowUtc,
                $"benefit-revoke:{benefit.Id}");
            return new UserBenefitPersistenceResult(benefit, true, null, benefit.Id);
        });
    }

    public Task<UserBenefitPersistenceResult?> ExpireAsync(long benefitId, DateTime nowUtc)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            // 该入口仅供系统过期任务调用，需要按全局主键处理任意租户权益。
            var benefit = await DbProtectedClient.Queryable<UserBenefit>()
                .Where(item => item.Id == benefitId && !item.IsDeleted)
                .FirstAsync();
            if (benefit == null ||
                benefit.RevokedAt.HasValue ||
                benefit.IsExpired ||
                benefit.ExpiresAt == null ||
                benefit.ExpiresAt > nowUtc)
            {
                return null;
            }

            var updated = await DbProtectedClient.Updateable<UserBenefit>()
                .SetColumns(item => new UserBenefit
                {
                    IsExpired = true,
                    IsActive = false,
                    ModifyTime = nowUtc,
                    ModifyBy = "System",
                    ModifyId = 0
                })
                .Where(item =>
                    item.Id == benefitId &&
                    item.TenantId == benefit.TenantId &&
                    !item.IsDeleted &&
                    item.RevokedAt == null &&
                    !item.IsExpired &&
                    item.ModifyTime == benefit.ModifyTime)
                .ExecuteCommandAsync();
            if (updated != 1)
            {
                return null;
            }

            await DeleteSelectionAsync(benefit);
            benefit.IsExpired = true;
            benefit.IsActive = false;
            await InsertBenefitOperationAsync(
                benefit,
                ShopEntitlementOperationTypes.Expire,
                null,
                "权益已到达 UTC 到期时间",
                0,
                "System",
                nowUtc,
                $"benefit-expire:{benefit.Id}");
            return new UserBenefitPersistenceResult(benefit, true, null, benefit.Id);
        });
    }

    private async Task<UserBenefit?> QueryOwnedBenefitAsync(long userId, long benefitId)
    {
        return await CreateTenantQueryableFor<UserBenefit>()
            .Where(benefit => benefit.Id == benefitId && benefit.UserId == userId && !benefit.IsDeleted)
            .FirstAsync();
    }

    private async Task<UserActiveBenefit?> QuerySelectionAsync(long tenantId, long userId, BenefitType benefitType)
    {
        return await DbProtectedClient.Queryable<UserActiveBenefit>()
            .Where(selection =>
                selection.TenantId == tenantId &&
                selection.UserId == userId &&
                selection.BenefitType == benefitType)
            .FirstAsync();
    }

    private async Task UpsertSelectionAsync(
        UserBenefit benefit,
        long operatorId,
        string operatorName,
        DateTime nowUtc)
    {
        var entityInfo = DbProtectedClient.EntityMaintenance.GetEntityInfo<UserActiveBenefit>();
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
            var physicalColumnName = RepositorySqlHelper.ResolvePhysicalColumnName(
                physicalColumnNames,
                physicalTableName,
                configuredColumnName);
            return RepositorySqlHelper.QuoteIdentifier(physicalColumnName);
        }

        var sql = $"INSERT INTO {table} (" +
                  $"{Column(nameof(UserActiveBenefit.Id))}, {Column(nameof(UserActiveBenefit.TenantId))}, " +
                  $"{Column(nameof(UserActiveBenefit.UserId))}, {Column(nameof(UserActiveBenefit.BenefitType))}, " +
                  $"{Column(nameof(UserActiveBenefit.BenefitId))}, {Column(nameof(UserActiveBenefit.SelectedAt))}, " +
                  $"{Column(nameof(UserActiveBenefit.CreateTime))}, {Column(nameof(UserActiveBenefit.CreateBy))}, " +
                  $"{Column(nameof(UserActiveBenefit.CreateId))}) VALUES " +
                  "(@Id, @TenantId, @UserId, @BenefitType, @BenefitId, @SelectedAt, @CreateTime, @CreateBy, @CreateId) " +
                  $"ON CONFLICT ({Column(nameof(UserActiveBenefit.TenantId))}, {Column(nameof(UserActiveBenefit.UserId))}, " +
                  $"{Column(nameof(UserActiveBenefit.BenefitType))}) DO UPDATE SET " +
                  $"{Column(nameof(UserActiveBenefit.BenefitId))} = excluded.{Column(nameof(UserActiveBenefit.BenefitId))}, " +
                  $"{Column(nameof(UserActiveBenefit.SelectedAt))} = excluded.{Column(nameof(UserActiveBenefit.SelectedAt))}, " +
                  $"{Column(nameof(UserActiveBenefit.ModifyTime))} = excluded.{Column(nameof(UserActiveBenefit.SelectedAt))}, " +
                  $"{Column(nameof(UserActiveBenefit.ModifyBy))} = excluded.{Column(nameof(UserActiveBenefit.CreateBy))}, " +
                  $"{Column(nameof(UserActiveBenefit.ModifyId))} = excluded.{Column(nameof(UserActiveBenefit.CreateId))}";

        await DbProtectedClient.Ado.ExecuteCommandAsync(
            sql,
            new SugarParameter("@Id", SnowFlakeSingle.Instance.NextId()),
            new SugarParameter("@TenantId", benefit.TenantId),
            new SugarParameter("@UserId", benefit.UserId),
            new SugarParameter("@BenefitType", (int)benefit.BenefitType),
            new SugarParameter("@BenefitId", benefit.Id),
            new SugarParameter("@SelectedAt", nowUtc),
            new SugarParameter("@CreateTime", nowUtc),
            new SugarParameter("@CreateBy", operatorName),
            new SugarParameter("@CreateId", operatorId));
    }

    private Task<int> DeleteSelectionAsync(UserBenefit benefit)
    {
        return DbProtectedClient.Deleteable<UserActiveBenefit>()
            .Where(selection =>
                selection.TenantId == benefit.TenantId &&
                selection.UserId == benefit.UserId &&
                selection.BenefitType == benefit.BenefitType &&
                selection.BenefitId == benefit.Id)
            .ExecuteCommandAsync();
    }

    private Task<int> ClearLegacyActiveFlagAsync(
        UserBenefit benefit,
        long operatorId,
        string operatorName,
        DateTime nowUtc)
    {
        return DbProtectedClient.Updateable<UserBenefit>()
            .SetColumns(item => new UserBenefit
            {
                IsActive = false,
                ModifyTime = nowUtc,
                ModifyBy = operatorName,
                ModifyId = operatorId
            })
            .Where(item => item.Id == benefit.Id && item.TenantId == benefit.TenantId)
            .ExecuteCommandAsync();
    }

    private async Task InsertBenefitOperationAsync(
        UserBenefit benefit,
        string operationType,
        long? relatedBenefitId,
        string? reason,
        long operatorId,
        string operatorName,
        DateTime nowUtc,
        string idempotencyKey)
    {
        var requestHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes($"{operationType}|{benefit.Id}|{relatedBenefitId}|{reason}")))
            .ToLowerInvariant();
        var operationId = SnowFlakeSingle.Instance.NextId();
        await DbProtectedClient.Insertable(new ShopEntitlementOperation
        {
            Id = operationId,
            TenantId = benefit.TenantId,
            UserId = benefit.UserId,
            BenefitId = benefit.Id,
            RelatedBenefitId = relatedBenefitId,
            OperationType = operationType,
            BenefitType = benefit.BenefitType,
            BenefitValue = benefit.BenefitValue,
            Reason = reason,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            EffectType = operationType switch
            {
                ShopEntitlementOperationTypes.Expire => ShopEntitlementEffectTypes.BenefitExpiration,
                ShopEntitlementOperationTypes.Revoke => ShopEntitlementEffectTypes.BenefitRevocation,
                _ => ShopEntitlementEffectTypes.BenefitSelection
            },
            EffectValue = benefit.BenefitValue,
            EffectResourceType = ShopEntitlementResourceTypes.UserBenefit,
            EffectResourceId = benefit.Id,
            ResultPayload = JsonSerializer.Serialize(new
            {
                operationId,
                benefitId = benefit.Id,
                relatedBenefitId,
                operationType
            }),
            CreateTime = nowUtc,
            CreateBy = operatorName,
            CreateId = operatorId
        }).ExecuteCommandAsync();
    }

    private static void EnsureBenefitCanBeActivated(UserBenefit benefit, DateTime nowUtc)
    {
        if (benefit.RevokedAt.HasValue)
        {
            throw new InvalidOperationException("权益已撤销");
        }

        if (benefit.ExpiresAt.HasValue && benefit.ExpiresAt.Value <= nowUtc)
        {
            throw new InvalidOperationException("权益已过期");
        }

        if (benefit.EffectiveAt > nowUtc)
        {
            throw new InvalidOperationException("权益尚未生效");
        }
    }
}
