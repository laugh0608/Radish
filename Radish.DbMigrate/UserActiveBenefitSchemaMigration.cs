using Microsoft.Extensions.DependencyInjection;
using Radish.Common;
using Radish.Model;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立持续权益唯一选择、撤销信息和通用权益操作流水。</summary>
internal sealed class UserActiveBenefitSchemaMigration : ISchemaMigration
{
    private const string BenefitTableName = "ShopUserBenefit";
    private const string ActiveTableName = "ShopUserActiveBenefit";
    private const string OperationTableName = "ShopEntitlementOperation";

    public static UserActiveBenefitSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260713_003_user_active_benefit";

    public string Scope => "Main";

    public string Description => "建立权益唯一选择、UTC 失效、撤销信息与通用业务流水";

    public string ChecksumSource =>
        "20260713_003_user_active_benefit|Main|" +
        "ShopUserActiveBenefit-v1|user-benefit-revocation-v1|shop-entitlement-operation-v2|" +
        "latest-legal-active-backfill-v1|utc-status-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        db.CodeFirst.InitTables<UserBenefit>();
        db.CodeFirst.InitTables<UserActiveBenefit>();
        db.CodeFirst.InitTables<ShopEntitlementOperation>();

        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var activeBenefits = db.Queryable<UserBenefit>()
            .Where(benefit =>
                benefit.IsActive &&
                !benefit.IsDeleted &&
                benefit.RevokedAt == null)
            .ToList()
            .Where(benefit => !benefit.ExpiresAt.HasValue || benefit.ExpiresAt.Value > nowUtc)
            .Where(benefit => benefit.EffectiveAt <= nowUtc)
            .ToList();
        var selectedBenefits = activeBenefits
            .GroupBy(benefit => new { benefit.TenantId, benefit.UserId, benefit.BenefitType })
            .Select(group => group
                .OrderByDescending(benefit => benefit.ActivatedAt ?? benefit.ModifyTime ?? benefit.CreateTime)
                .ThenByDescending(benefit => benefit.Id)
                .First())
            .ToList();

        db.Deleteable<UserActiveBenefit>().ExecuteCommand();
        db.Updateable<UserBenefit>()
            .SetColumns(benefit => new UserBenefit { IsActive = false })
            .Where(benefit => benefit.IsActive)
            .ExecuteCommand();

        if (selectedBenefits.Count == 0)
        {
            return;
        }

        var selections = selectedBenefits.Select(benefit => new UserActiveBenefit
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = benefit.TenantId,
            UserId = benefit.UserId,
            BenefitType = benefit.BenefitType,
            BenefitId = benefit.Id,
            SelectedAt = benefit.ActivatedAt ?? benefit.ModifyTime ?? benefit.CreateTime,
            CreateTime = nowUtc,
            CreateBy = "DbMigrate",
            CreateId = 0
        }).ToList();
        db.Insertable(selections).ExecuteCommand();
        var selectedIds = selectedBenefits.Select(benefit => benefit.Id).ToList();
        db.Updateable<UserBenefit>()
            .SetColumns(benefit => new UserBenefit { IsActive = true })
            .Where(benefit => selectedIds.Contains(benefit.Id))
            .ExecuteCommand();
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        if (!db.DbMaintenance.IsAnyTable(BenefitTableName, false))
        {
            return [];
        }

        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var hasRevokedAtColumn = DatabaseIdentifierResolver.ResolveColumn(
            db,
            BenefitTableName,
            nameof(UserBenefit.RevokedAt)) != null;
        var rows = QueryDiagnosticRows(db, hasRevokedAtColumn);
        var warnings = new List<string>();
        var conflictGroups = rows
            .Where(row =>
                row.IsActive &&
                !row.IsDeleted &&
                row.RevokedAt == null &&
                row.EffectiveAt <= nowUtc &&
                (!row.ExpiresAt.HasValue || row.ExpiresAt.Value > nowUtc))
            .GroupBy(row => new { row.TenantId, row.UserId, row.BenefitType })
            .Count(group => group.Count() > 1);
        if (conflictGroups > 0)
        {
            warnings.Add($"发现 {conflictGroups} 组同用户同类型多激活权益；apply 将按最近激活时间保留唯一选择。");
        }

        var invalidActiveCount = rows.Count(row =>
            row.IsActive &&
            (row.IsDeleted || row.RevokedAt != null || row.EffectiveAt > nowUtc ||
             (row.ExpiresAt.HasValue && row.ExpiresAt.Value <= nowUtc)));
        if (invalidActiveCount > 0)
        {
            warnings.Add($"发现 {invalidActiveCount} 条已删除、已撤销、尚未生效或实时已过期但仍标记激活的权益；apply 不会迁移为当前选择。");
        }

        return warnings;
    }

    private static List<BenefitDiagnosticRow> QueryDiagnosticRows(
        ISqlSugarClient db,
        bool includeRevokedAt)
    {
        if (includeRevokedAt)
        {
            return db.Queryable<UserBenefit>()
                .Select(benefit => new BenefitDiagnosticRow
                {
                    Id = benefit.Id,
                    TenantId = benefit.TenantId,
                    UserId = benefit.UserId,
                    BenefitType = benefit.BenefitType,
                    IsActive = benefit.IsActive,
                    IsDeleted = benefit.IsDeleted,
                    RevokedAt = benefit.RevokedAt,
                    EffectiveAt = benefit.EffectiveAt,
                    ExpiresAt = benefit.ExpiresAt,
                    ActivatedAt = benefit.ActivatedAt,
                    ModifyTime = benefit.ModifyTime,
                    CreateTime = benefit.CreateTime
                })
                .ToList();
        }

        return db.Queryable<UserBenefit>()
            .Select(benefit => new BenefitDiagnosticRow
            {
                Id = benefit.Id,
                TenantId = benefit.TenantId,
                UserId = benefit.UserId,
                BenefitType = benefit.BenefitType,
                IsActive = benefit.IsActive,
                IsDeleted = benefit.IsDeleted,
                EffectiveAt = benefit.EffectiveAt,
                ExpiresAt = benefit.ExpiresAt,
                ActivatedAt = benefit.ActivatedAt,
                ModifyTime = benefit.ModifyTime,
                CreateTime = benefit.CreateTime
            })
            .ToList();
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        var issues = new List<string>();
        foreach (var tableName in new[] { BenefitTableName, ActiveTableName, OperationTableName })
        {
            if (!db.DbMaintenance.IsAnyTable(tableName, false))
            {
                issues.Add($"{tableName} 表不存在。");
            }
        }

        if (issues.Count > 0)
        {
            return issues;
        }

        foreach (var columnName in new[] { "RevokedAt", "RevokedById", "RevokedByName", "RevocationReason" })
        {
            if (DatabaseIdentifierResolver.ResolveColumn(db, BenefitTableName, columnName) == null)
            {
                issues.Add($"{BenefitTableName}.{columnName} 列不存在。");
            }
        }

        foreach (var columnName in new[] { "BenefitId", "RelatedBenefitId", "BenefitType", "BenefitValue", "Reason" })
        {
            if (DatabaseIdentifierResolver.ResolveColumn(db, OperationTableName, columnName) == null)
            {
                issues.Add($"{OperationTableName}.{columnName} 列不存在。");
            }
        }

        foreach (var indexName in new[]
                 {
                     "idx_active_benefit_tenant_user_type",
                     "idx_active_benefit_tenant_benefit",
                     "idx_shop_entitlement_operation_benefit_time"
                 })
        {
            if (!db.DbMaintenance.IsAnyIndex(indexName))
            {
                issues.Add($"缺少索引 {indexName}。");
            }
        }

        if (issues.Count > 0)
        {
            return issues;
        }

        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var benefits = db.Queryable<UserBenefit>().ToList().ToDictionary(benefit => benefit.Id);
        var selections = db.Queryable<UserActiveBenefit>().ToList();
        foreach (var selection in selections)
        {
            if (!benefits.TryGetValue(selection.BenefitId, out var benefit))
            {
                issues.Add($"当前权益指针 {selection.Id} 引用了不存在的权益 {selection.BenefitId}。");
                continue;
            }

            if (benefit.TenantId != selection.TenantId ||
                benefit.UserId != selection.UserId ||
                benefit.BenefitType != selection.BenefitType)
            {
                issues.Add($"当前权益指针 {selection.Id} 与拥有记录的租户、用户或类型不一致。");
            }

            if (benefit.IsDeleted || benefit.RevokedAt.HasValue || benefit.EffectiveAt > nowUtc ||
                (benefit.ExpiresAt.HasValue && benefit.ExpiresAt.Value <= nowUtc))
            {
                issues.Add(
                    $"当前权益指针 {selection.Id} 指向实时无效权益 {benefit.Id}" +
                    $"（IsDeleted={benefit.IsDeleted}, RevokedAt={benefit.RevokedAt:O}, ExpiresAt={benefit.ExpiresAt:O}）。");
            }
        }

        var selectedIds = selections.Select(selection => selection.BenefitId).ToHashSet();
        var mirrorMismatchCount = benefits.Values.Count(benefit => benefit.IsActive != selectedIds.Contains(benefit.Id));
        if (mirrorMismatchCount > 0)
        {
            issues.Add($"存在 {mirrorMismatchCount} 条 UserBenefit.IsActive 兼容镜像与唯一指针不一致。");
        }

        return issues;
    }

    private sealed class BenefitDiagnosticRow
    {
        public long Id { get; set; }

        public long TenantId { get; set; }

        public long UserId { get; set; }

        public BenefitType BenefitType { get; set; }

        public bool IsActive { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime? RevokedAt { get; set; }

        public DateTime EffectiveAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public DateTime? ActivatedAt { get; set; }

        public DateTime? ModifyTime { get; set; }

        public DateTime CreateTime { get; set; }
    }
}
