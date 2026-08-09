using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class ProductReviewServiceTest
{
    private static readonly DateTime NowUtc =
        new(2026, 8, 9, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetPageAsync_ShouldReturnPublicIdentityVerifiedPurchaseAndSummary()
    {
        var fixture = new Fixture();
        fixture.ReviewRepository
            .Setup(item => item.QueryPageAsync(9, 7001, 1, 20))
            .ReturnsAsync((
                (IReadOnlyList<ProductReview>)
                [
                    new ProductReview
                    {
                        Id = 8001,
                        TenantId = 9,
                        ProductId = 7001,
                        UserId = 1001,
                        EligibleOrderId = 7101,
                        AuthorName = "旧昵称",
                        Rating = 5,
                        Comment = "很喜欢",
                        Version = 2,
                        CreateTime = NowUtc
                    }
                ],
                1));
        fixture.ReviewRepository
            .Setup(item => item.QuerySummaryAsync(9, 7001))
            .ReturnsAsync(new ProductReviewRatingSummary(1, 5m, 1, 0, 0, 0, 0));
        fixture.UserRepository
            .Setup(item => item.QueryAsync(It.IsAny<Expression<Func<User, bool>>>() ))
            .ReturnsAsync([
                new User
                {
                    Id = 1001,
                    PublicId = "usr_018f6b6f7c7d70008f8f8f8f8f8f8f8f",
                    PublicIndex = 2048,
                    UserName = "Alice",
                    IsEnable = true
                }
            ]);

        var result = await fixture.Service.GetPageAsync(7001, 1, 20, 9);

        Assert.Equal(5m, result.VoSummary.VoAverageRating);
        Assert.Equal(1, result.VoSummary.VoReviewCount);
        var review = Assert.Single(result.VoItems);
        Assert.Equal("Alice", review.VoAuthorDisplayName);
        Assert.Equal("Alice#2048", review.VoAuthorDisplayHandle);
        Assert.True(review.VoVerifiedPurchase);
        Assert.Equal(2, review.VoVersion);
    }

    [Fact]
    public async Task GetMineAsync_ShouldExposeTombstoneVersionAndBlockModeratedReview()
    {
        var fixture = new Fixture();
        fixture.ReviewRepository
            .Setup(item => item.QueryByUserAndProductIncludingDeletedAsync(9, 1001, 7001))
            .ReturnsAsync(new ProductReview
            {
                Id = 8001,
                TenantId = 9,
                ProductId = 7001,
                UserId = 1001,
                EligibleOrderId = 7101,
                AuthorName = "alice",
                Rating = 3,
                Version = 4,
                IsDeleted = true,
                ModerationTargetActionId = 9001
            });
        fixture.ReviewRepository
            .Setup(item => item.QueryLatestCompletedOrderIdAsync(9, 1001, 7001))
            .ReturnsAsync(7101L);

        var result = await fixture.Service.GetMineAsync(7001, 1001, 9);

        Assert.False(result.VoCanReview);
        Assert.Equal(4, result.VoExpectedVersion);
        Assert.Null(result.VoReview);
        Assert.Equal("该评价已被治理限制", result.VoUnavailableReason);
    }

    [Fact]
    public async Task UpsertAsync_ShouldMapPurchaseRequiredToStructuredForbidden()
    {
        var fixture = new Fixture();
        fixture.ReviewRepository
            .Setup(item => item.UpsertAsync(It.IsAny<ProductReviewWriteCommand>()))
            .ThrowsAsync(new ProductReviewPurchaseRequiredException());

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            fixture.Service.UpsertAsync(
                7001,
                new UpsertProductReviewDto { Rating = 5, ExpectedVersion = 0 },
                1001,
                "alice",
                9));

        Assert.Equal(StatusCodes.Status403Forbidden, exception.StatusCode);
        Assert.Equal("ProductReview.PurchaseRequired", exception.ErrorCode);
        Assert.Equal("error.product_review.purchase_required", exception.MessageKey);
    }

    [Fact]
    public async Task UpsertAsync_ShouldMapStaleVersionToStructuredConflict()
    {
        var fixture = new Fixture();
        fixture.ReviewRepository
            .Setup(item => item.UpsertAsync(It.Is<ProductReviewWriteCommand>(command =>
                command.ExpectedVersion == 2 &&
                command.Comment == "更新后的评价")))
            .ThrowsAsync(new ProductReviewVersionConflictException());

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            fixture.Service.UpsertAsync(
                7001,
                new UpsertProductReviewDto
                {
                    Rating = 4,
                    Comment = " 更新后的评价 ",
                    ExpectedVersion = 2
                },
                1001,
                "alice",
                9));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        Assert.Equal("ProductReview.VersionConflict", exception.ErrorCode);
        Assert.Equal("error.product_review.version_conflict", exception.MessageKey);
    }

    private sealed class Fixture
    {
        public Mock<IProductReviewRepository> ReviewRepository { get; } =
            new(MockBehavior.Strict);

        public Mock<IProductService> ProductService { get; } =
            new(MockBehavior.Strict);

        public Mock<IBaseRepository<User>> UserRepository { get; } =
            new(MockBehavior.Strict);

        public ProductReviewService Service { get; }

        public Fixture()
        {
            ProductService
                .Setup(item => item.GetProductDetailAsync(7001))
                .ReturnsAsync(new ProductVo
                {
                    VoId = 7001,
                    VoName = "青玉头像框",
                    VoIsEnabled = true,
                    VoIsOnSale = true
                });
            Service = new ProductReviewService(
                ReviewRepository.Object,
                ProductService.Object,
                UserRepository.Object,
                new FixedTimeProvider(new DateTimeOffset(NowUtc)));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nowUtc;
    }
}
