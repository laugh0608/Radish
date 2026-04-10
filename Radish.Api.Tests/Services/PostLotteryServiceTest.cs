using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public class PostLotteryServiceTest
{
    [Fact]
    public async Task DrawAsync_Should_DrawDistinctParentCommentAuthors_AndExcludePostAuthor()
    {
        var postService = new Mock<IPostService>(MockBehavior.Strict);
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var lotteryRepository = new Mock<IBaseRepository<PostLottery>>(MockBehavior.Strict);
        var winnerRepository = new Mock<IBaseRepository<PostLotteryWinner>>(MockBehavior.Strict);
        var commentRepository = new Mock<IBaseRepository<Comment>>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var logger = new Mock<ILogger<PostLotteryService>>();

        var post = new Post(new PostInitializationOptions("抽奖帖", "正文"))
        {
            Id = 1001,
            AuthorId = 9527,
            AuthorName = "Author",
            TenantId = 9,
            IsPublished = true,
            CreateTime = DateTime.UtcNow.AddHours(-2),
            PublishTime = DateTime.UtcNow.AddHours(-2)
        };
        var lottery = new PostLottery
        {
            Id = 2001,
            PostId = 1001,
            TenantId = 9,
            PrizeName = "周边礼包",
            WinnerCount = 2,
            DrawTime = DateTime.UtcNow.AddMinutes(10),
            IsDrawn = false
        };
        var comments = new List<Comment>
        {
            new("参与一下")
            {
                Id = 3001,
                PostId = 1001,
                AuthorId = 1101,
                AuthorName = "Alice",
                ParentId = null,
                IsEnabled = true,
                IsDeleted = false,
                CreateTime = DateTime.UtcNow.AddMinutes(-10)
            },
            new("重复参与")
            {
                Id = 3002,
                PostId = 1001,
                AuthorId = 1101,
                AuthorName = "Alice",
                ParentId = null,
                IsEnabled = true,
                IsDeleted = false,
                CreateTime = DateTime.UtcNow.AddMinutes(-8)
            },
            new("我也来")
            {
                Id = 3003,
                PostId = 1001,
                AuthorId = 1102,
                AuthorName = "Bob",
                ParentId = null,
                IsEnabled = true,
                IsDeleted = false,
                CreateTime = DateTime.UtcNow.AddMinutes(-6)
            },
            new("作者本人不参与")
            {
                Id = 3004,
                PostId = 1001,
                AuthorId = 9527,
                AuthorName = "Author",
                ParentId = null,
                IsEnabled = true,
                IsDeleted = false,
                CreateTime = DateTime.UtcNow.AddMinutes(-4)
            }
        };

        postRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(post);
        lotteryRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<PostLottery, bool>>?>()))
            .ReturnsAsync(lottery);
        commentRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Comment, bool>>?>()))
            .ReturnsAsync((Expression<Func<Comment, bool>>? expression) =>
            {
                if (expression == null)
                {
                    return comments;
                }

                var predicate = expression.Compile();
                return comments.Where(predicate).ToList();
            });
        winnerRepository
            .Setup(repository => repository.AddRangeAsync(It.Is<List<PostLotteryWinner>>(winners =>
                winners.Count == 2 &&
                winners.All(winner => winner.LotteryId == 2001 && winner.PostId == 1001) &&
                winners.Select(winner => winner.UserId).Distinct().Count() == 2 &&
                winners.All(winner => winner.UserId != 9527))))
            .ReturnsAsync(2);
        lotteryRepository
            .Setup(repository => repository.UpdateAsync(It.Is<PostLottery>(entity =>
                entity.Id == 2001 &&
                entity.IsDrawn &&
                entity.ParticipantCount == 2 &&
                entity.ModifyBy == "Author" &&
                entity.ModifyId == 9527 &&
                entity.DrawnAt.HasValue)))
            .ReturnsAsync(true);
        postService
            .Setup(service => service.GetPostDetailAsync(1001, 9527, "default"))
            .ReturnsAsync(new PostVo
            {
                VoId = 1001,
                VoHasLottery = true,
                VoLottery = new PostLotteryVo
                {
                    VoLotteryId = 2001,
                    VoPostId = 1001,
                    VoPrizeName = "周边礼包",
                    VoParticipantCount = 2,
                    VoIsDrawn = true,
                    VoWinners =
                    [
                        new PostLotteryWinnerVo { VoId = 1, VoLotteryId = 2001, VoUserId = 1101, VoUserName = "Alice", VoDrawnAt = DateTime.UtcNow },
                        new PostLotteryWinnerVo { VoId = 2, VoLotteryId = 2001, VoUserId = 1102, VoUserName = "Bob", VoDrawnAt = DateTime.UtcNow }
                    ]
                }
            });
        notificationService
            .Setup(service => service.CreateNotificationAsync(It.Is<Model.DtoModels.CreateNotificationDto>(dto =>
                dto.Type == NotificationType.LotteryWon &&
                dto.BusinessId == 1001 &&
                dto.ReceiverUserIds.Count == 2 &&
                dto.ReceiverUserIds.Contains(1101) &&
                dto.ReceiverUserIds.Contains(1102))))
            .ReturnsAsync(1);

        var service = new PostLotteryService(
            postService.Object,
            postRepository.Object,
            lotteryRepository.Object,
            winnerRepository.Object,
            commentRepository.Object,
            notificationService.Object,
            logger.Object);

        var result = await service.DrawAsync(1001, 9527, "Author");

        Assert.True(result.VoIsDrawn);
        Assert.Equal(2, result.VoParticipantCount);
        Assert.Equal(2, result.VoWinners.Count);

        winnerRepository.VerifyAll();
        lotteryRepository.VerifyAll();
        postService.VerifyAll();
        notificationService.VerifyAll();
    }

    [Fact]
    public async Task DrawAsync_Should_Reject_When_PostIsNotOldEnoughForManualDraw()
    {
        var postService = new Mock<IPostService>(MockBehavior.Strict);
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var lotteryRepository = new Mock<IBaseRepository<PostLottery>>(MockBehavior.Strict);
        var winnerRepository = new Mock<IBaseRepository<PostLotteryWinner>>(MockBehavior.Strict);
        var commentRepository = new Mock<IBaseRepository<Comment>>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var logger = new Mock<ILogger<PostLotteryService>>();

        postRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(new Post(new PostInitializationOptions("抽奖帖", "正文"))
            {
                Id = 1001,
                AuthorId = 9527,
                AuthorName = "Author",
                IsPublished = true,
                CreateTime = DateTime.UtcNow.AddMinutes(-20),
                PublishTime = DateTime.UtcNow.AddMinutes(-20)
            });
        lotteryRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<PostLottery, bool>>?>()))
            .ReturnsAsync(new PostLottery
            {
                Id = 2001,
                PostId = 1001,
                PrizeName = "周边礼包",
                WinnerCount = 1,
                DrawTime = DateTime.UtcNow.AddMinutes(10),
                IsDrawn = false
            });

        var service = new PostLotteryService(
            postService.Object,
            postRepository.Object,
            lotteryRepository.Object,
            winnerRepository.Object,
            commentRepository.Object,
            notificationService.Object,
            logger.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DrawAsync(1001, 9527, "Author"));

        Assert.Equal("发帖满 1 小时后才可提前开奖", exception.Message);
        winnerRepository.Verify(repository => repository.AddRangeAsync(It.IsAny<List<PostLotteryWinner>>()), Times.Never);
    }

    [Fact]
    public async Task DrawAsync_Should_Reject_When_AutoDrawDeadlineAlreadyReached()
    {
        var postService = new Mock<IPostService>(MockBehavior.Strict);
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var lotteryRepository = new Mock<IBaseRepository<PostLottery>>(MockBehavior.Strict);
        var winnerRepository = new Mock<IBaseRepository<PostLotteryWinner>>(MockBehavior.Strict);
        var commentRepository = new Mock<IBaseRepository<Comment>>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var logger = new Mock<ILogger<PostLotteryService>>();

        postRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(new Post(new PostInitializationOptions("抽奖帖", "正文"))
            {
                Id = 1001,
                AuthorId = 9527,
                AuthorName = "Author",
                IsPublished = true,
                CreateTime = DateTime.UtcNow.AddHours(-2),
                PublishTime = DateTime.UtcNow.AddHours(-2)
            });
        lotteryRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<PostLottery, bool>>?>()))
            .ReturnsAsync(new PostLottery
            {
                Id = 2001,
                PostId = 1001,
                PrizeName = "周边礼包",
                WinnerCount = 1,
                DrawTime = DateTime.UtcNow.AddMinutes(-1),
                IsDrawn = false
            });

        var service = new PostLotteryService(
            postService.Object,
            postRepository.Object,
            lotteryRepository.Object,
            winnerRepository.Object,
            commentRepository.Object,
            notificationService.Object,
            logger.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DrawAsync(1001, 9527, "Author"));

        Assert.Equal("已到自动开奖时间，请等待系统开奖", exception.Message);
        winnerRepository.Verify(repository => repository.AddRangeAsync(It.IsAny<List<PostLotteryWinner>>()), Times.Never);
    }

    [Fact]
    public async Task AutoDrawByPostIdAsync_Should_CloseLottery_When_NoParticipants()
    {
        var postService = new Mock<IPostService>(MockBehavior.Strict);
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var lotteryRepository = new Mock<IBaseRepository<PostLottery>>(MockBehavior.Strict);
        var winnerRepository = new Mock<IBaseRepository<PostLotteryWinner>>(MockBehavior.Strict);
        var commentRepository = new Mock<IBaseRepository<Comment>>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var logger = new Mock<ILogger<PostLotteryService>>();

        var post = new Post(new PostInitializationOptions("自动开奖帖", "正文"))
        {
            Id = 2001,
            AuthorId = 9527,
            AuthorName = "Author",
            TenantId = 9,
            IsPublished = true,
            CreateTime = DateTime.UtcNow.AddHours(-2),
            PublishTime = DateTime.UtcNow.AddHours(-2)
        };
        var drawTime = DateTime.UtcNow.AddMinutes(-3);
        var lottery = new PostLottery
        {
            Id = 3001,
            PostId = 2001,
            TenantId = 9,
            PrizeName = "空池自动开奖",
            WinnerCount = 1,
            DrawTime = drawTime,
            IsDrawn = false
        };

        postRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(post);
        lotteryRepository
            .SetupSequence(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<PostLottery, bool>>?>()))
            .ReturnsAsync(lottery)
            .ReturnsAsync(lottery);
        commentRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Comment, bool>>?>()))
            .ReturnsAsync(new List<Comment>());
        lotteryRepository
            .Setup(repository => repository.UpdateAsync(It.Is<PostLottery>(entity =>
                entity.Id == 3001 &&
                entity.IsDrawn &&
                entity.ParticipantCount == 0 &&
                entity.DrawnAt == drawTime &&
                entity.ModifyBy == "LotteryAutoDrawJob" &&
                entity.ModifyId == null)))
            .ReturnsAsync(true);
        postService
            .Setup(service => service.GetPostDetailAsync(2001, null, "default"))
            .ReturnsAsync(new PostVo
            {
                VoId = 2001,
                VoHasLottery = true,
                VoLottery = new PostLotteryVo
                {
                    VoLotteryId = 3001,
                    VoPostId = 2001,
                    VoPrizeName = "空池自动开奖",
                    VoParticipantCount = 0,
                    VoIsDrawn = true,
                    VoDrawnAt = drawTime,
                    VoWinners = []
                }
            });

        var service = new PostLotteryService(
            postService.Object,
            postRepository.Object,
            lotteryRepository.Object,
            winnerRepository.Object,
            commentRepository.Object,
            notificationService.Object,
            logger.Object);

        var result = await service.AutoDrawByPostIdAsync(2001);

        Assert.True(result.VoIsDrawn);
        Assert.Equal(0, result.VoParticipantCount);
        Assert.Empty(result.VoWinners);

        winnerRepository.Verify(repository => repository.AddRangeAsync(It.IsAny<List<PostLotteryWinner>>()), Times.Never);
        notificationService.Verify(service => service.CreateNotificationAsync(It.IsAny<Model.DtoModels.CreateNotificationDto>()), Times.Never);
        lotteryRepository.VerifyAll();
        postService.VerifyAll();
    }
}
