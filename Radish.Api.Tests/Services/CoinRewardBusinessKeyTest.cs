using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class CoinRewardBusinessKeyTest
{
    [Fact]
    public async Task GrantGodCommentRewardAsync_ShouldUseCommentLevelRewardBusinessKey()
    {
        const long commentId = 7001;
        const long authorId = 9001;
        const int likeCount = 3;
        CoinGrantOnceResult grantResult = CoinGrantOnceResult.NewGrant("TXN_GOD_COMMENT");

        var coinService = new Mock<ICoinService>(MockBehavior.Strict);
        coinService
            .Setup(service => service.GrantCoinOnceAsync(
                authorId,
                23,
                "HIGHLIGHT_REWARD",
                "coin:highlight-base:god-comment:author:9001:comment:7001",
                "GOD_COMMENT",
                commentId,
                It.IsAny<string>()))
            .ReturnsAsync(grantResult);

        var rewardService = new CoinRewardService(
            coinService.Object,
            new Mock<IBaseRepository<CoinTransaction>>(MockBehavior.Strict).Object);

        var result = await rewardService.GrantGodCommentRewardAsync(commentId, authorId, likeCount);

        result.IsSuccess.ShouldBeTrue();
        result.TransactionNo.ShouldBe("TXN_GOD_COMMENT");
        result.Amount.ShouldBe(23);
        coinService.VerifyAll();
    }

    [Fact]
    public async Task GrantLikeBonusRewardAsync_ShouldUseLikeCountSnapshotKey()
    {
        const long highlightId = 8001;
        const long authorId = 9001;
        const int likeIncrement = 2;
        const int likeCountAfter = 7;

        var coinService = new Mock<ICoinService>(MockBehavior.Strict);
        coinService
            .Setup(service => service.GrantCoinOnceAsync(
                authorId,
                10,
                "HIGHLIGHT_REWARD",
                "coin:highlight-like-bonus:god-comment:highlight:8001:to-like:7",
                "GodComment_LIKE_BONUS",
                highlightId,
                It.IsAny<string>()))
            .ReturnsAsync(CoinGrantOnceResult.NewGrant("TXN_LIKE_BONUS"));

        var rewardService = new CoinRewardService(
            coinService.Object,
            new Mock<IBaseRepository<CoinTransaction>>(MockBehavior.Strict).Object);

        var result = await rewardService.GrantLikeBonusRewardAsync(
            highlightId,
            authorId,
            likeIncrement,
            "GodComment",
            likeCountAfter);

        result.IsSuccess.ShouldBeTrue();
        result.TransactionNo.ShouldBe("TXN_LIKE_BONUS");
        result.Amount.ShouldBe(10);
        coinService.VerifyAll();
    }

    [Fact]
    public async Task GrantCommentReplyRewardAsync_ShouldReturnAlreadyGranted_WhenBusinessKeyExists()
    {
        const long parentCommentId = 7001;
        const long parentAuthorId = 9001;
        const long replyCommentId = 7002;

        var coinService = new Mock<ICoinService>(MockBehavior.Strict);
        coinService
            .Setup(service => service.GrantCoinOnceAsync(
                parentAuthorId,
                1,
                "COMMENT_REWARD",
                It.Is<string>(key =>
                    key.StartsWith("coin:comment-reply:author:9001:comment:7001:day:", StringComparison.Ordinal)),
                "COMMENT_REPLY",
                parentCommentId,
                It.IsAny<string>()))
            .ReturnsAsync(CoinGrantOnceResult.Existing("TXN_EXISTING_REPLY"));

        var rewardService = new CoinRewardService(
            coinService.Object,
            new Mock<IBaseRepository<CoinTransaction>>(MockBehavior.Strict).Object);

        var result = await rewardService.GrantCommentReplyRewardAsync(
            parentCommentId,
            parentAuthorId,
            replyCommentId);

        result.IsSuccess.ShouldBeFalse();
        result.FailureReason.ShouldBe("今日已发放过评论被回复奖励");
        coinService.VerifyAll();
    }

    [Fact]
    public async Task CheckDailyLikeRewardLimitAsync_ShouldUseExistingTransactionRepository()
    {
        var coinService = new Mock<ICoinService>(MockBehavior.Strict);
        var transactionRepository = new Mock<IBaseRepository<CoinTransaction>>(MockBehavior.Strict);
        transactionRepository
            .Setup(repository => repository.QuerySumAsync(
                It.IsAny<Expression<Func<CoinTransaction, long>>>(),
                It.IsAny<Expression<Func<CoinTransaction, bool>>?>()))
            .ReturnsAsync(51);

        var rewardService = new CoinRewardService(coinService.Object, transactionRepository.Object);

        var limitReached = await rewardService.CheckDailyLikeRewardLimitAsync(9001);

        limitReached.ShouldBeTrue();
    }
}
