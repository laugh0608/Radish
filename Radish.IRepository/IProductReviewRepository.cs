using Radish.Model;

namespace Radish.IRepository;

public sealed record ProductReviewRatingSummary(
    int ReviewCount,
    decimal AverageRating,
    int FiveStarCount,
    int FourStarCount,
    int ThreeStarCount,
    int TwoStarCount,
    int OneStarCount);

public sealed record ProductReviewWriteCommand(
    long TenantId,
    long ProductId,
    long UserId,
    string AuthorName,
    int Rating,
    string? Comment,
    int ExpectedVersion,
    DateTime NowUtc);

public sealed record ProductReviewDeleteCommand(
    long TenantId,
    long ReviewId,
    long UserId,
    int ExpectedVersion,
    string OperatorName,
    DateTime NowUtc);

public sealed record ProductReviewWriteResult(ProductReview Review, bool Created, bool Restored);

public sealed class ProductReviewNotFoundException : Exception
{
}

public sealed class ProductReviewPurchaseRequiredException : Exception
{
}

public sealed class ProductReviewVersionConflictException : Exception
{
}

public sealed class ProductReviewModeratedException : Exception
{
}

public interface IProductReviewRepository
{
    Task<(IReadOnlyList<ProductReview> Items, int Total)> QueryPageAsync(
        long tenantId,
        long productId,
        int pageIndex,
        int pageSize);

    Task<ProductReviewRatingSummary> QuerySummaryAsync(long tenantId, long productId);

    Task<ProductReview?> QueryByUserAndProductIncludingDeletedAsync(long tenantId, long userId, long productId);

    Task<long?> QueryLatestCompletedOrderIdAsync(long tenantId, long userId, long productId);

    Task<ProductReviewWriteResult> UpsertAsync(ProductReviewWriteCommand command);

    Task<ProductReview> DeleteAsync(ProductReviewDeleteCommand command);
}
