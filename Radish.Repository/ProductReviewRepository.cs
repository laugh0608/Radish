using Radish.Common;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Repository;

/// <summary>商品评价、购买资格与 CAS 写入的 Main 原子边界。</summary>
public sealed class ProductReviewRepository
    : BaseRepository<ProductReview>, IProductReviewRepository
{
    public ProductReviewRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public async Task<(IReadOnlyList<ProductReview> Items, int Total)> QueryPageAsync(
        long tenantId,
        long productId,
        int pageIndex,
        int pageSize)
    {
        var normalizedTenantId = Math.Max(0, tenantId);
        var safePageIndex = Math.Max(1, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, 50);
        return await ExecuteDbOperationAsync(async () =>
        {
            var query = DbProtectedClient.Queryable<ProductReview>()
                .Where(item =>
                    item.TenantId == normalizedTenantId &&
                    item.ProductId == productId &&
                    !item.IsDeleted);
            var total = await query.CountAsync();
            var items = await query
                .OrderBy(item => item.CreateTime, OrderByType.Desc)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .Skip((safePageIndex - 1) * safePageSize)
                .Take(safePageSize)
                .ToListAsync();
            return ((IReadOnlyList<ProductReview>)items, total);
        });
    }

    public async Task<ProductReviewRatingSummary> QuerySummaryAsync(long tenantId, long productId)
    {
        var normalizedTenantId = Math.Max(0, tenantId);
        return await ExecuteDbOperationAsync(async () =>
        {
            var query = DbProtectedClient.Queryable<ProductReview>()
                .Where(item =>
                    item.TenantId == normalizedTenantId &&
                    item.ProductId == productId &&
                    !item.IsDeleted);
            var count = await query.CountAsync();
            if (count == 0)
            {
                return new ProductReviewRatingSummary(0, 0, 0, 0, 0, 0, 0);
            }

            var average = await query.AvgAsync(item => (decimal)item.Rating);
            var distribution = await query
                .GroupBy(item => item.Rating)
                .Select(item => new RatingCountRow
                {
                    Rating = item.Rating,
                    Count = SqlFunc.AggregateCount(item.Id)
                })
                .ToListAsync();
            var counts = distribution.ToDictionary(item => item.Rating, item => item.Count);
            return new ProductReviewRatingSummary(
                count,
                decimal.Round(average, 1, MidpointRounding.AwayFromZero),
                counts.GetValueOrDefault(5),
                counts.GetValueOrDefault(4),
                counts.GetValueOrDefault(3),
                counts.GetValueOrDefault(2),
                counts.GetValueOrDefault(1));
        });
    }

    public async Task<ProductReview?> QueryByUserAndProductIncludingDeletedAsync(
        long tenantId,
        long userId,
        long productId)
    {
        return await ExecuteDbOperationAsync(() => DbProtectedClient.Queryable<ProductReview>()
            .Where(item =>
                item.TenantId == Math.Max(0, tenantId) &&
                item.UserId == userId &&
                item.ProductId == productId)
            .FirstAsync());
    }

    public async Task<long?> QueryLatestCompletedOrderIdAsync(long tenantId, long userId, long productId)
    {
        var order = await ExecuteDbOperationAsync(() => DbProtectedClient.Queryable<Order>()
            .Where(item =>
                item.TenantId == Math.Max(0, tenantId) &&
                item.UserId == userId &&
                item.ProductId == productId &&
                item.Status == OrderStatus.Completed)
            .OrderBy(item => item.CompletedTime, OrderByType.Desc)
            .OrderBy(item => item.Id, OrderByType.Desc)
            .Select(item => new Order { Id = item.Id })
            .FirstAsync());
        return order?.Id;
    }

    public async Task<ProductReviewWriteResult> UpsertAsync(ProductReviewWriteCommand command)
    {
        ValidateWriteCommand(command);
        try
        {
            return await ExecuteDbOperationAsync(() => UpsertCoreAsync(command));
        }
        catch (Exception exception) when (RepositorySqlHelper.IsUniqueConstraintException(exception))
        {
            throw new ProductReviewVersionConflictException();
        }
    }

    public async Task<ProductReview> DeleteAsync(ProductReviewDeleteCommand command)
    {
        if (command.ReviewId <= 0 || command.UserId <= 0 || command.ExpectedVersion < 1)
        {
            throw new ArgumentException("商品评价删除参数无效");
        }

        return await ExecuteDbOperationAsync(() => DeleteCoreAsync(command));
    }

    private async Task<ProductReviewWriteResult> UpsertCoreAsync(ProductReviewWriteCommand command)
    {
        DbProtectedClient.Ado.BeginTran();
        try
        {
            var product = await DbProtectedClient.Queryable<Product>()
                .Where(item =>
                    item.Id == command.ProductId &&
                    (item.TenantId == command.TenantId || item.TenantId == 0) &&
                    !item.IsDeleted &&
                    item.IsEnabled)
                .FirstAsync();
            if (product == null)
            {
                throw new ProductReviewNotFoundException();
            }

            var eligibleOrder = await DbProtectedClient.Queryable<Order>()
                .Where(item =>
                    item.TenantId == command.TenantId &&
                    item.UserId == command.UserId &&
                    item.ProductId == command.ProductId &&
                    item.Status == OrderStatus.Completed)
                .OrderBy(item => item.CompletedTime, OrderByType.Desc)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .FirstAsync();
            if (eligibleOrder == null)
            {
                throw new ProductReviewPurchaseRequiredException();
            }

            var review = await DbProtectedClient.Queryable<ProductReview>()
                .Where(item =>
                    item.TenantId == command.TenantId &&
                    item.ProductId == command.ProductId &&
                    item.UserId == command.UserId)
                .FirstAsync();
            if (review == null)
            {
                if (command.ExpectedVersion != 0)
                {
                    throw new ProductReviewVersionConflictException();
                }

                review = new ProductReview
                {
                    Id = SnowFlakeSingle.Instance.NextId(),
                    TenantId = command.TenantId,
                    ProductId = command.ProductId,
                    UserId = command.UserId,
                    EligibleOrderId = eligibleOrder.Id,
                    AuthorName = command.AuthorName,
                    Rating = command.Rating,
                    Comment = command.Comment,
                    Version = 1,
                    IsDeleted = false,
                    CreateTime = command.NowUtc,
                    CreateBy = NormalizeAuditName(command.AuthorName),
                    CreateId = command.UserId
                };
                var inserted = await DbProtectedClient.Insertable(review).ExecuteCommandAsync();
                if (inserted != 1)
                {
                    throw new ProductReviewVersionConflictException();
                }

                DbProtectedClient.Ado.CommitTran();
                return new ProductReviewWriteResult(review, true, false);
            }

            if (review.ModerationTargetActionId.HasValue && review.IsDeleted)
            {
                throw new ProductReviewModeratedException();
            }
            if (review.Version != command.ExpectedVersion)
            {
                throw new ProductReviewVersionConflictException();
            }

            var wasDeleted = review.IsDeleted;
            var nextVersion = review.Version + 1;
            var affected = await DbProtectedClient.Updateable<ProductReview>()
                .SetColumns(item => new ProductReview
                {
                    EligibleOrderId = eligibleOrder.Id,
                    AuthorName = command.AuthorName,
                    Rating = command.Rating,
                    Comment = command.Comment,
                    Version = nextVersion,
                    IsDeleted = false,
                    DeletedAt = null,
                    DeletedBy = null,
                    ModifyTime = command.NowUtc,
                    ModifyBy = NormalizeAuditName(command.AuthorName),
                    ModifyId = command.UserId
                })
                .Where(item =>
                    item.Id == review.Id &&
                    item.TenantId == command.TenantId &&
                    item.UserId == command.UserId &&
                    item.Version == command.ExpectedVersion &&
                    item.ModerationTargetActionId == null)
                .ExecuteCommandAsync();
            if (affected != 1)
            {
                throw new ProductReviewVersionConflictException();
            }

            review.EligibleOrderId = eligibleOrder.Id;
            review.AuthorName = command.AuthorName;
            review.Rating = command.Rating;
            review.Comment = command.Comment;
            review.Version = nextVersion;
            review.IsDeleted = false;
            review.DeletedAt = null;
            review.DeletedBy = null;
            review.ModifyTime = command.NowUtc;
            review.ModifyBy = NormalizeAuditName(command.AuthorName);
            review.ModifyId = command.UserId;
            DbProtectedClient.Ado.CommitTran();
            return new ProductReviewWriteResult(review, false, wasDeleted);
        }
        catch
        {
            DbProtectedClient.Ado.RollbackTran();
            throw;
        }
    }

    private async Task<ProductReview> DeleteCoreAsync(ProductReviewDeleteCommand command)
    {
        DbProtectedClient.Ado.BeginTran();
        try
        {
            var review = await DbProtectedClient.Queryable<ProductReview>()
                .Where(item =>
                    item.Id == command.ReviewId &&
                    item.TenantId == Math.Max(0, command.TenantId) &&
                    item.UserId == command.UserId)
                .FirstAsync();
            if (review == null)
            {
                throw new ProductReviewNotFoundException();
            }
            if (review.ModerationTargetActionId.HasValue && review.IsDeleted)
            {
                throw new ProductReviewModeratedException();
            }
            if (review.IsDeleted || review.Version != command.ExpectedVersion)
            {
                throw new ProductReviewVersionConflictException();
            }

            var nextVersion = review.Version + 1;
            var affected = await DbProtectedClient.Updateable<ProductReview>()
                .SetColumns(item => new ProductReview
                {
                    IsDeleted = true,
                    DeletedAt = command.NowUtc,
                    DeletedBy = command.OperatorName,
                    Version = nextVersion,
                    ModifyTime = command.NowUtc,
                    ModifyBy = command.OperatorName,
                    ModifyId = command.UserId
                })
                .Where(item =>
                    item.Id == command.ReviewId &&
                    item.TenantId == Math.Max(0, command.TenantId) &&
                    item.UserId == command.UserId &&
                    item.Version == command.ExpectedVersion &&
                    !item.IsDeleted &&
                    item.ModerationTargetActionId == null)
                .ExecuteCommandAsync();
            if (affected != 1)
            {
                throw new ProductReviewVersionConflictException();
            }

            review.IsDeleted = true;
            review.DeletedAt = command.NowUtc;
            review.DeletedBy = command.OperatorName;
            review.Version = nextVersion;
            review.ModifyTime = command.NowUtc;
            review.ModifyBy = command.OperatorName;
            review.ModifyId = command.UserId;
            DbProtectedClient.Ado.CommitTran();
            return review;
        }
        catch
        {
            DbProtectedClient.Ado.RollbackTran();
            throw;
        }
    }

    private static void ValidateWriteCommand(ProductReviewWriteCommand command)
    {
        if (command.ProductId <= 0 || command.UserId <= 0 || command.ExpectedVersion < 0 ||
            command.Rating is < 1 or > 5 || string.IsNullOrWhiteSpace(command.AuthorName))
        {
            throw new ArgumentException("商品评价写入参数无效");
        }
    }

    private static string NormalizeAuditName(string value)
    {
        var normalized = value.Trim();
        return normalized.Length <= 50 ? normalized : normalized[..50];
    }

    private sealed class RatingCountRow
    {
        public int Rating { get; set; }
        public int Count { get; set; }
    }
}
