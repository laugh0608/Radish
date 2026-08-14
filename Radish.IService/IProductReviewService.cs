using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

public interface IProductReviewService
{
    Task<ProductReviewPageVo> GetPageAsync(long productId, int pageIndex, int pageSize, long tenantId);

    Task<MyProductReviewVo> GetMineAsync(long productId, long userId, long tenantId);

    Task<ProductReviewVo> UpsertAsync(
        long productId,
        UpsertProductReviewDto dto,
        long userId,
        string userName,
        long tenantId);

    Task<ProductReviewVo> DeleteAsync(
        long reviewId,
        int expectedVersion,
        long userId,
        string userName,
        long tenantId);
}
