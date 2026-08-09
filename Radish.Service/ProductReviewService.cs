using Microsoft.AspNetCore.Http;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.Service;

/// <summary>商品公开评分、已购资格与本人 CAS 评价应用服务。</summary>
public sealed class ProductReviewService : IProductReviewService
{
    private const int MaxPageSize = 50;
    private readonly IProductReviewRepository _productReviewRepository;
    private readonly IProductService _productService;
    private readonly IBaseRepository<User> _userRepository;
    private readonly TimeProvider _timeProvider;

    public ProductReviewService(
        IProductReviewRepository productReviewRepository,
        IProductService productService,
        IBaseRepository<User> userRepository,
        TimeProvider timeProvider)
    {
        _productReviewRepository = productReviewRepository;
        _productService = productService;
        _userRepository = userRepository;
        _timeProvider = timeProvider;
    }

    public async Task<ProductReviewPageVo> GetPageAsync(
        long productId,
        int pageIndex,
        int pageSize,
        long tenantId)
    {
        await RequireProductAsync(productId);
        var safePageIndex = Math.Max(1, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var normalizedTenantId = Math.Max(0, tenantId);
        var (items, total) = await _productReviewRepository.QueryPageAsync(
            normalizedTenantId,
            productId,
            safePageIndex,
            safePageSize);
        var summary = await _productReviewRepository.QuerySummaryAsync(normalizedTenantId, productId);
        return new ProductReviewPageVo
        {
            VoSummary = MapSummary(summary),
            VoItems = await MapReviewsAsync(items),
            VoTotal = total,
            VoPageIndex = safePageIndex,
            VoPageSize = safePageSize
        };
    }

    public async Task<MyProductReviewVo> GetMineAsync(long productId, long userId, long tenantId)
    {
        if (userId <= 0)
        {
            throw AuthenticationRequired();
        }

        await RequireProductAsync(productId);
        var normalizedTenantId = Math.Max(0, tenantId);
        var review = await _productReviewRepository.QueryByUserAndProductIncludingDeletedAsync(
            normalizedTenantId,
            userId,
            productId);
        var eligibleOrderId = await _productReviewRepository.QueryLatestCompletedOrderIdAsync(
            normalizedTenantId,
            userId,
            productId);
        var moderated = review is { IsDeleted: true, ModerationTargetActionId: not null };
        var canReview = eligibleOrderId.HasValue && !moderated;
        return new MyProductReviewVo
        {
            VoCanReview = canReview,
            VoUnavailableReason = moderated
                ? "该评价已被治理限制"
                : eligibleOrderId.HasValue
                    ? null
                    : "仅已完成订单的购买者可以评价",
            VoExpectedVersion = review?.Version ?? 0,
            VoReview = review is { IsDeleted: false }
                ? await MapReviewAsync(review)
                : null
        };
    }

    public async Task<ProductReviewVo> UpsertAsync(
        long productId,
        UpsertProductReviewDto dto,
        long userId,
        string userName,
        long tenantId)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (userId <= 0)
        {
            throw AuthenticationRequired();
        }
        if (dto.Rating is < 1 or > 5 || dto.ExpectedVersion < 0)
        {
            throw new ArgumentException("商品评价参数无效");
        }

        await RequireProductAsync(productId);
        try
        {
            var result = await _productReviewRepository.UpsertAsync(new ProductReviewWriteCommand(
                Math.Max(0, tenantId),
                productId,
                userId,
                NormalizeAuthorName(userName, userId),
                dto.Rating,
                NormalizeComment(dto.Comment),
                dto.ExpectedVersion,
                _timeProvider.GetUtcNow().UtcDateTime));
            return await MapReviewAsync(result.Review);
        }
        catch (ProductReviewNotFoundException)
        {
            throw ProductNotFound();
        }
        catch (ProductReviewPurchaseRequiredException)
        {
            throw PurchaseRequired();
        }
        catch (ProductReviewVersionConflictException)
        {
            throw VersionConflict();
        }
        catch (ProductReviewModeratedException)
        {
            throw Moderated();
        }
    }

    public async Task<ProductReviewVo> DeleteAsync(
        long reviewId,
        int expectedVersion,
        long userId,
        string userName,
        long tenantId)
    {
        if (userId <= 0)
        {
            throw AuthenticationRequired();
        }
        if (reviewId <= 0 || expectedVersion < 1)
        {
            throw new ArgumentException("商品评价删除参数无效");
        }

        try
        {
            var review = await _productReviewRepository.DeleteAsync(new ProductReviewDeleteCommand(
                Math.Max(0, tenantId),
                reviewId,
                userId,
                expectedVersion,
                NormalizeAuditName(userName, userId),
                _timeProvider.GetUtcNow().UtcDateTime));
            return await MapReviewAsync(review);
        }
        catch (ProductReviewNotFoundException)
        {
            throw ReviewNotFound();
        }
        catch (ProductReviewVersionConflictException)
        {
            throw VersionConflict();
        }
        catch (ProductReviewModeratedException)
        {
            throw Moderated();
        }
    }

    private async Task RequireProductAsync(long productId)
    {
        if (productId <= 0 || await _productService.GetProductDetailAsync(productId) == null)
        {
            throw ProductNotFound();
        }
    }

    private async Task<List<ProductReviewVo>> MapReviewsAsync(IReadOnlyCollection<ProductReview> reviews)
    {
        if (reviews.Count == 0)
        {
            return [];
        }

        var userIds = reviews.Select(item => item.UserId).Distinct().ToList();
        var users = await _userRepository.QueryAsync(item =>
            userIds.Contains(item.Id) && !item.IsDeleted && item.IsEnable);
        var usersById = users.GroupBy(item => item.Id).ToDictionary(group => group.Key, group => group.First());
        return reviews.Select(review => MapReview(review, usersById.GetValueOrDefault(review.UserId))).ToList();
    }

    private async Task<ProductReviewVo> MapReviewAsync(ProductReview review)
    {
        var user = await _userRepository.QueryFirstAsync(item =>
            item.Id == review.UserId && !item.IsDeleted && item.IsEnable);
        return MapReview(review, user);
    }

    private static ProductReviewVo MapReview(ProductReview review, User? user)
    {
        var displayName = user == null
            ? User.NormalizeDisplayName(review.AuthorName, review.UserId)
            : User.NormalizeDisplayName(user.UserName, user.Id);
        return new ProductReviewVo
        {
            VoId = review.Id,
            VoProductId = review.ProductId,
            VoUserId = review.UserId,
            VoAuthorPublicId = user?.PublicId,
            VoAuthorPublicIndex = user?.PublicIndex,
            VoAuthorDisplayName = displayName,
            VoAuthorDisplayHandle = User.BuildDisplayHandle(displayName, user?.PublicIndex, review.UserId),
            VoRating = review.Rating,
            VoComment = review.Comment,
            VoVerifiedPurchase = true,
            VoVersion = review.Version,
            VoCreateTime = review.CreateTime,
            VoModifyTime = review.ModifyTime
        };
    }

    private static ProductReviewSummaryVo MapSummary(ProductReviewRatingSummary summary) => new()
    {
        VoAverageRating = summary.AverageRating,
        VoReviewCount = summary.ReviewCount,
        VoFiveStarCount = summary.FiveStarCount,
        VoFourStarCount = summary.FourStarCount,
        VoThreeStarCount = summary.ThreeStarCount,
        VoTwoStarCount = summary.TwoStarCount,
        VoOneStarCount = summary.OneStarCount
    };

    private static string NormalizeAuthorName(string? userName, long userId)
    {
        var normalized = User.NormalizeDisplayName(userName, userId);
        return normalized.Length <= 200 ? normalized : normalized[..200];
    }

    private static string NormalizeAuditName(string? userName, long userId)
    {
        var normalized = NormalizeAuthorName(userName, userId);
        return normalized.Length <= 50 ? normalized : normalized[..50];
    }

    private static string? NormalizeComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        var normalized = comment.Trim();
        if (normalized.Length > 500)
        {
            throw new ArgumentException("评价内容不能超过500个字符");
        }
        return normalized;
    }

    private static BusinessException AuthenticationRequired() => new(
        "请先登录后再评价",
        StatusCodes.Status401Unauthorized,
        "ProductReview.AuthenticationRequired",
        "error.product_review.authentication_required");

    private static BusinessException ProductNotFound() => new(
        "商品不存在或当前不可评价",
        StatusCodes.Status404NotFound,
        "ProductReview.ProductNotFound",
        "error.product_review.product_not_found");

    private static BusinessException ReviewNotFound() => new(
        "商品评价不存在",
        StatusCodes.Status404NotFound,
        "ProductReview.NotFound",
        "error.product_review.not_found");

    private static BusinessException PurchaseRequired() => new(
        "仅已完成订单的购买者可以评价",
        StatusCodes.Status403Forbidden,
        "ProductReview.PurchaseRequired",
        "error.product_review.purchase_required");

    private static BusinessException VersionConflict() => new(
        "评价已发生变化，请刷新后重试",
        StatusCodes.Status409Conflict,
        "ProductReview.VersionConflict",
        "error.product_review.version_conflict");

    private static BusinessException Moderated() => new(
        "评价已被治理限制，当前不可修改",
        StatusCodes.Status409Conflict,
        "ProductReview.Moderated",
        "error.product_review.moderated");
}
