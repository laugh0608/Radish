using Radish.Model;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立已购商品评价、CAS 版本与治理目标边界。</summary>
internal sealed class ProductReviewSchemaMigration : ISchemaMigration
{
    private const string TableName = "ShopProductReview";
    private static readonly string[] RequiredIndexes =
    [
        "idx_product_review_user_product",
        "idx_product_review_product_page",
        "idx_product_review_product_rating"
    ];

    public static ProductReviewSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260809_020_product_review";
    public string Scope => "Main";
    public string Description => "建立已完成订单资格、五星评价、唯一关系、CAS 与治理目标";
    public string ChecksumSource =>
        "20260809_020_product_review|Main|ShopProductReview-v1|" +
        "completed-order-eligibility-v1|tenant-product-user-unique-v1|" +
        "rating-1-5-v1|cas-soft-delete-restore-v1|moderation-target-v1|report-product-snapshot-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = Diagnose(db, services);
        if (issues.Count > 0)
        {
            throw new InvalidOperationException(
                "商品评价迁移前置诊断未通过：" + string.Join("；", issues));
        }

        db.CodeFirst.InitTables<ProductReview>();
        if (db.DbMaintenance.IsAnyTable(nameof(ContentReport), false))
        {
            db.CodeFirst.InitTables<ContentReport>();
            db.Updateable<ContentReport>()
                .SetColumns(report => new ContentReport
                {
                    TargetSnapshotProductId = report.TargetContentId
                })
                .Where(report =>
                    report.ReportTargetType == (int)ContentReportTargetTypeEnum.Product &&
                    report.TargetSnapshotProductId == null)
                .ExecuteCommand();
        }
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(TableName, false))
        {
            return [];
        }

        var requiredBaselineColumns = new[]
        {
            nameof(ProductReview.Id),
            nameof(ProductReview.TenantId),
            nameof(ProductReview.ProductId),
            nameof(ProductReview.UserId),
            nameof(ProductReview.Rating),
            nameof(ProductReview.Version),
            nameof(ProductReview.IsDeleted)
        };
        if (requiredBaselineColumns.Any(column =>
                DatabaseIdentifierResolver.ResolveColumn(db, TableName, column) == null))
        {
            return [];
        }

        return VerifyRows(db);
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(TableName, false))
        {
            return [$"缺少表 {TableName}。"];
        }

        var issues = new List<string>();
        foreach (var column in new[]
                 {
                     nameof(ProductReview.Id),
                     nameof(ProductReview.TenantId),
                     nameof(ProductReview.ProductId),
                     nameof(ProductReview.UserId),
                     nameof(ProductReview.EligibleOrderId),
                     nameof(ProductReview.AuthorName),
                     nameof(ProductReview.Rating),
                     nameof(ProductReview.Comment),
                     nameof(ProductReview.Version),
                     nameof(ProductReview.ModerationTargetActionId),
                     nameof(ProductReview.IsDeleted),
                     nameof(ProductReview.DeletedAt),
                     nameof(ProductReview.DeletedBy),
                     nameof(ProductReview.CreateTime),
                     nameof(ProductReview.CreateBy),
                     nameof(ProductReview.CreateId),
                     nameof(ProductReview.ModifyTime),
                     nameof(ProductReview.ModifyBy),
                     nameof(ProductReview.ModifyId)
                 })
        {
            if (DatabaseIdentifierResolver.ResolveColumn(db, TableName, column) == null)
            {
                issues.Add($"缺少列 {TableName}.{column}。");
            }
        }
        if (issues.Count > 0)
        {
            return issues;
        }

        foreach (var indexName in RequiredIndexes)
        {
            if (!IndexExists(db, indexName))
            {
                issues.Add($"缺少索引 {indexName}。");
            }
        }
        if (db.DbMaintenance.IsAnyTable(nameof(ContentReport), false) &&
            DatabaseIdentifierResolver.ResolveColumn(
                db,
                nameof(ContentReport),
                nameof(ContentReport.TargetSnapshotProductId)) == null)
        {
            issues.Add($"缺少列 {nameof(ContentReport)}.{nameof(ContentReport.TargetSnapshotProductId)}。");
        }
        issues.AddRange(VerifyRows(db));
        return issues.Distinct(StringComparer.Ordinal).ToList();
    }

    private static IReadOnlyList<string> VerifyRows(ISqlSugarClient db)
    {
        var reviews = db.Queryable<ProductReview>().ToList();
        if (reviews.Count == 0)
        {
            return [];
        }

        var issues = new List<string>();
        var invalidRows = reviews.Count(review =>
            review.Id <= 0 || review.ProductId <= 0 || review.UserId <= 0 ||
            review.EligibleOrderId <= 0 || review.Rating is < 1 or > 5 ||
            review.Version < 1 || string.IsNullOrWhiteSpace(review.AuthorName) ||
            review.Comment is { Length: > 500 });
        if (invalidRows > 0)
        {
            issues.Add($"存在 {invalidRows} 条字段或五星范围无效的商品评价。");
        }

        var duplicates = reviews
            .GroupBy(review => new { review.TenantId, review.ProductId, review.UserId })
            .Count(group => group.Count() > 1);
        if (duplicates > 0)
        {
            issues.Add($"存在 {duplicates} 组同租户、商品、用户的重复评价。");
        }

        if (db.DbMaintenance.IsAnyTable("ShopOrder", false))
        {
            var orderIds = reviews.Select(review => review.EligibleOrderId).Distinct().ToList();
            var ordersById = db.Queryable<Order>()
                .Where(order => orderIds.Contains(order.Id))
                .ToList()
                .GroupBy(order => order.Id)
                .ToDictionary(group => group.Key, group => group.First());
            var invalidOrders = reviews.Count(review =>
                !ordersById.TryGetValue(review.EligibleOrderId, out var order) ||
                order.TenantId != review.TenantId ||
                order.ProductId != review.ProductId ||
                order.UserId != review.UserId ||
                order.Status != OrderStatus.Completed);
            if (invalidOrders > 0)
            {
                issues.Add($"存在 {invalidOrders} 条缺少匹配 Completed 订单证据的商品评价。");
            }
        }

        if (db.DbMaintenance.IsAnyTable("ShopProduct", false))
        {
            var productIds = reviews.Select(review => review.ProductId).Distinct().ToList();
            var products = db.Queryable<Product>()
                .Where(product => productIds.Contains(product.Id))
                .Select(product => new Product
                {
                    Id = product.Id,
                    TenantId = product.TenantId,
                    IsDeleted = product.IsDeleted
                })
                .ToList();
            var invalidProducts = reviews.Count(review => !products.Any(product =>
                product.Id == review.ProductId &&
                (product.TenantId == review.TenantId || product.TenantId == 0)));
            if (invalidProducts > 0)
            {
                issues.Add($"存在 {invalidProducts} 条商品不存在或跨租户的评价。");
            }
        }

        return issues;
    }

    private static bool IndexExists(ISqlSugarClient db, string indexName)
    {
        if (db.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return db.DbMaintenance.IsAnyIndex(indexName);
        }

        var physicalTableName =
            DatabaseIdentifierResolver.ResolveColumn(db, TableName, nameof(ProductReview.Id))?.TableName
            ?? TableName;
        return db.DbMaintenance.GetIndexList(physicalTableName)
            .Any(index => string.Equals(index, indexName, StringComparison.OrdinalIgnoreCase));
    }
}
