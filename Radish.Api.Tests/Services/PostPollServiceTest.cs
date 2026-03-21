using System;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public class PostPollServiceTest
{
    [Fact]
    public async Task VoteAsync_Should_AddVote_AndReturnLatestPoll()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var postPollRepository = new Mock<IBaseRepository<PostPoll>>(MockBehavior.Strict);
        var postPollOptionRepository = new Mock<IBaseRepository<PostPollOption>>(MockBehavior.Strict);
        var postPollVoteRepository = new Mock<IBaseRepository<PostPollVote>>(MockBehavior.Strict);
        var postService = new Mock<IPostService>(MockBehavior.Strict);

        var post = new Post(new PostInitializationOptions("标题", "正文"))
        {
            Id = 1001,
            TenantId = 9
        };
        var poll = new PostPoll
        {
            Id = 2001,
            PostId = 1001,
            TenantId = 9,
            Question = "中午吃什么？",
            TotalVoteCount = 3
        };
        var option = new PostPollOption
        {
            Id = 3001,
            PollId = 2001,
            VoteCount = 2,
            OptionText = "盖饭"
        };

        postRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(post);
        postPollRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PostPoll, bool>>?>()))
            .ReturnsAsync(poll);
        postPollOptionRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PostPollOption, bool>>?>()))
            .ReturnsAsync(option);
        postPollVoteRepository
            .Setup(r => r.QueryExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PostPollVote, bool>>>()))
            .ReturnsAsync(false);
        postPollVoteRepository
            .Setup(r => r.AddAsync(It.Is<PostPollVote>(vote =>
                vote.PollId == 2001 &&
                vote.PostId == 1001 &&
                vote.OptionId == 3001 &&
                vote.UserId == 9527 &&
                vote.UserName == "Tester" &&
                vote.TenantId == 9)))
            .ReturnsAsync(4001);
        postPollOptionRepository
            .Setup(r => r.UpdateAsync(It.Is<PostPollOption>(entity =>
                entity.Id == 3001 &&
                entity.VoteCount == 3 &&
                entity.ModifyBy == "Tester" &&
                entity.ModifyId == 9527)))
            .ReturnsAsync(true);
        postPollRepository
            .Setup(r => r.UpdateAsync(It.Is<PostPoll>(entity =>
                entity.Id == 2001 &&
                entity.TotalVoteCount == 4 &&
                entity.ModifyBy == "Tester" &&
                entity.ModifyId == 9527)))
            .ReturnsAsync(true);
        postService
            .Setup(s => s.GetPostDetailAsync(1001, 9527, "default"))
            .ReturnsAsync(new PostVo
            {
                VoId = 1001,
                VoHasPoll = true,
                VoPoll = new PostPollVo
                {
                    VoPollId = 2001,
                    VoPostId = 1001,
                    VoHasVoted = true,
                    VoSelectedOptionId = 3001,
                    VoTotalVoteCount = 4
                }
            });

        var service = CreateService(postService, postRepository, postPollRepository, postPollOptionRepository, postPollVoteRepository);

        var result = await service.VoteAsync(9527, "Tester", new VotePollDto
        {
            PostId = 1001,
            OptionId = 3001
        });

        Assert.True(result.VoHasVoted);
        Assert.Equal(3001, result.VoSelectedOptionId);
        Assert.Equal(4, result.VoTotalVoteCount);

        postPollVoteRepository.VerifyAll();
        postPollOptionRepository.VerifyAll();
        postPollRepository.VerifyAll();
        postService.VerifyAll();
    }

    [Fact]
    public async Task VoteAsync_Should_RejectDuplicateVote()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var postPollRepository = new Mock<IBaseRepository<PostPoll>>(MockBehavior.Strict);
        var postPollOptionRepository = new Mock<IBaseRepository<PostPollOption>>(MockBehavior.Strict);
        var postPollVoteRepository = new Mock<IBaseRepository<PostPollVote>>(MockBehavior.Strict);
        var postService = new Mock<IPostService>(MockBehavior.Strict);

        postRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(new Post(new PostInitializationOptions("标题", "正文")) { Id = 1001 });
        postPollRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PostPoll, bool>>?>()))
            .ReturnsAsync(new PostPoll { Id = 2001, PostId = 1001, Question = "问题" });
        postPollOptionRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PostPollOption, bool>>?>()))
            .ReturnsAsync(new PostPollOption { Id = 3001, PollId = 2001, OptionText = "选项" });
        postPollVoteRepository
            .Setup(r => r.QueryExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PostPollVote, bool>>>()))
            .ReturnsAsync(true);

        var service = CreateService(postService, postRepository, postPollRepository, postPollOptionRepository, postPollVoteRepository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.VoteAsync(9527, "Tester", new VotePollDto
        {
            PostId = 1001,
            OptionId = 3001
        }));

        Assert.Equal("你已经投过票", exception.Message);
        postPollVoteRepository.Verify(r => r.AddAsync(It.IsAny<PostPollVote>()), Times.Never);
    }

    [Fact]
    public async Task VoteAsync_Should_RejectClosedPoll()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var postPollRepository = new Mock<IBaseRepository<PostPoll>>(MockBehavior.Strict);
        var postPollOptionRepository = new Mock<IBaseRepository<PostPollOption>>(MockBehavior.Strict);
        var postPollVoteRepository = new Mock<IBaseRepository<PostPollVote>>(MockBehavior.Strict);
        var postService = new Mock<IPostService>(MockBehavior.Strict);

        postRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(new Post(new PostInitializationOptions("标题", "正文")) { Id = 1001 });
        postPollRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PostPoll, bool>>?>()))
            .ReturnsAsync(new PostPoll
            {
                Id = 2001,
                PostId = 1001,
                Question = "问题",
                EndTime = DateTime.UtcNow.AddMinutes(-1)
            });

        var service = CreateService(postService, postRepository, postPollRepository, postPollOptionRepository, postPollVoteRepository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.VoteAsync(9527, "Tester", new VotePollDto
        {
            PostId = 1001,
            OptionId = 3001
        }));

        Assert.Equal("投票已截止", exception.Message);
        postPollOptionRepository.Verify(r => r.QueryFirstAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PostPollOption, bool>>?>()), Times.Never);
    }

    [Fact]
    public async Task VoteAsync_Should_RejectMissingOption()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var postPollRepository = new Mock<IBaseRepository<PostPoll>>(MockBehavior.Strict);
        var postPollOptionRepository = new Mock<IBaseRepository<PostPollOption>>(MockBehavior.Strict);
        var postPollVoteRepository = new Mock<IBaseRepository<PostPollVote>>(MockBehavior.Strict);
        var postService = new Mock<IPostService>(MockBehavior.Strict);

        postRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(new Post(new PostInitializationOptions("标题", "正文")) { Id = 1001 });
        postPollRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PostPoll, bool>>?>()))
            .ReturnsAsync(new PostPoll
            {
                Id = 2001,
                PostId = 1001,
                Question = "问题",
                EndTime = DateTime.UtcNow.AddHours(1)
            });
        postPollOptionRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PostPollOption, bool>>?>()))
            .ReturnsAsync((PostPollOption?)null);

        var service = CreateService(postService, postRepository, postPollRepository, postPollOptionRepository, postPollVoteRepository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.VoteAsync(9527, "Tester", new VotePollDto
        {
            PostId = 1001,
            OptionId = 9999
        }));

        Assert.Equal("投票选项不存在", exception.Message);
        postPollVoteRepository.Verify(r => r.QueryExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PostPollVote, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task CloseAsync_Should_ClosePoll_AndReturnLatestPoll()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var postPollRepository = new Mock<IBaseRepository<PostPoll>>(MockBehavior.Strict);
        var postPollOptionRepository = new Mock<IBaseRepository<PostPollOption>>(MockBehavior.Strict);
        var postPollVoteRepository = new Mock<IBaseRepository<PostPollVote>>(MockBehavior.Strict);
        var postService = new Mock<IPostService>(MockBehavior.Strict);

        var post = new Post(new PostInitializationOptions("标题", "正文"))
        {
            Id = 1002,
            AuthorId = 9527
        };
        var poll = new PostPoll
        {
            Id = 2002,
            PostId = 1002,
            Question = "今晚吃什么？",
            IsClosed = false
        };

        postRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(post);
        postPollRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PostPoll, bool>>?>()))
            .ReturnsAsync(poll);
        postPollRepository
            .Setup(r => r.UpdateAsync(It.Is<PostPoll>(entity =>
                entity.Id == 2002 &&
                entity.IsClosed &&
                entity.ModifyBy == "Tester" &&
                entity.ModifyId == 9527)))
            .ReturnsAsync(true);
        postService
            .Setup(s => s.GetPostDetailAsync(1002, 9527, "default"))
            .ReturnsAsync(new PostVo
            {
                VoId = 1002,
                VoHasPoll = true,
                VoPollIsClosed = true,
                VoPoll = new PostPollVo
                {
                    VoPollId = 2002,
                    VoPostId = 1002,
                    VoIsClosed = true,
                    VoTotalVoteCount = 7
                }
            });

        var service = CreateService(postService, postRepository, postPollRepository, postPollOptionRepository, postPollVoteRepository);

        var result = await service.CloseAsync(1002, 9527, "Tester");

        Assert.True(result.VoIsClosed);
        Assert.Equal(7, result.VoTotalVoteCount);
        postPollRepository.VerifyAll();
        postService.VerifyAll();
        postPollVoteRepository.Verify(r => r.AddAsync(It.IsAny<PostPollVote>()), Times.Never);
    }

    [Fact]
    public async Task CloseAsync_Should_RejectNonAuthor()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var postPollRepository = new Mock<IBaseRepository<PostPoll>>(MockBehavior.Strict);
        var postPollOptionRepository = new Mock<IBaseRepository<PostPollOption>>(MockBehavior.Strict);
        var postPollVoteRepository = new Mock<IBaseRepository<PostPollVote>>(MockBehavior.Strict);
        var postService = new Mock<IPostService>(MockBehavior.Strict);

        postRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(new Post(new PostInitializationOptions("标题", "正文"))
            {
                Id = 1003,
                AuthorId = 20001
            });
        postPollRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PostPoll, bool>>?>()))
            .ReturnsAsync(new PostPoll
            {
                Id = 2003,
                PostId = 1003,
                Question = "问题",
                IsClosed = false
            });

        var service = CreateService(postService, postRepository, postPollRepository, postPollOptionRepository, postPollVoteRepository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloseAsync(1003, 9527, "Tester"));

        Assert.Equal("只有发帖者可以结束投票", exception.Message);
        postPollRepository.Verify(r => r.UpdateAsync(It.IsAny<PostPoll>()), Times.Never);
    }

    [Fact]
    public async Task CloseAsync_Should_RejectAlreadyClosedPoll()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var postPollRepository = new Mock<IBaseRepository<PostPoll>>(MockBehavior.Strict);
        var postPollOptionRepository = new Mock<IBaseRepository<PostPollOption>>(MockBehavior.Strict);
        var postPollVoteRepository = new Mock<IBaseRepository<PostPollVote>>(MockBehavior.Strict);
        var postService = new Mock<IPostService>(MockBehavior.Strict);

        postRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(new Post(new PostInitializationOptions("标题", "正文"))
            {
                Id = 1004,
                AuthorId = 9527
            });
        postPollRepository
            .Setup(r => r.QueryFirstAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PostPoll, bool>>?>()))
            .ReturnsAsync(new PostPoll
            {
                Id = 2004,
                PostId = 1004,
                Question = "问题",
                IsClosed = true
            });

        var service = CreateService(postService, postRepository, postPollRepository, postPollOptionRepository, postPollVoteRepository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloseAsync(1004, 9527, "Tester"));

        Assert.Equal("投票已截止", exception.Message);
        postPollRepository.Verify(r => r.UpdateAsync(It.IsAny<PostPoll>()), Times.Never);
    }

    private static PostPollService CreateService(
        Mock<IPostService> postService,
        Mock<IBaseRepository<Post>> postRepository,
        Mock<IBaseRepository<PostPoll>> postPollRepository,
        Mock<IBaseRepository<PostPollOption>> postPollOptionRepository,
        Mock<IBaseRepository<PostPollVote>> postPollVoteRepository)
    {
        return new PostPollService(
            postService.Object,
            postRepository.Object,
            postPollRepository.Object,
            postPollOptionRepository.Object,
            postPollVoteRepository.Object);
    }
}
