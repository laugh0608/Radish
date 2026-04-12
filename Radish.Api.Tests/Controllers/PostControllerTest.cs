using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Moq;
using Radish.Api.Controllers;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.IService.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(PostController))]
public class PostControllerTest
{
    [Fact]
    public async Task GetList_Should_Fill_Metadata_In_Batch_Without_Querying_Post_Detail_Per_Item()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);
        var attachmentServiceMock = new Mock<IBaseService<Attachment, AttachmentVo>>(MockBehavior.Strict);
        var commentServiceMock = new Mock<IBaseService<Comment, CommentVo>>(MockBehavior.Strict);

        var posts = new List<PostVo>
        {
            new()
            {
                VoId = 9527,
                VoTitle = "论坛投票帖子",
                VoAuthorId = 0,
                VoCreateTime = DateTime.UtcNow
            }
        };

        postServiceMock
            .Setup(service => service.GetForumPostPageAsync(
                null,
                1,
                20,
                "newest",
                null,
                null,
                null,
                null))
            .ReturnsAsync((posts, 1));
        postServiceMock
            .Setup(service => service.FillPostListMetadataAsync(It.Is<List<PostVo>>(items => ReferenceEquals(items, posts))))
            .Callback<List<PostVo>>(items =>
            {
                items[0].VoCategoryName = "综合讨论";
                items[0].VoTags = "投票, 社区";
                items[0].VoHasPoll = true;
                items[0].VoPollTotalVoteCount = 18;
                items[0].VoPollIsClosed = false;
            })
            .Returns(Task.CompletedTask);
        commentServiceMock
            .Setup(service => service.QueryAsync(It.IsAny<Expression<Func<Comment, bool>>>()))
            .ReturnsAsync(new List<CommentVo>());

        var controller = CreateController(
            postServiceMock.Object,
            moderationServiceMock.Object,
            attachmentServiceMock.Object,
            commentServiceMock.Object);

        var result = await controller.GetList();

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);

        var pageModel = Assert.IsType<PageModel<PostVo>>(result.ResponseData);
        var post = Assert.Single(pageModel.Data);
        Assert.Equal("综合讨论", post.VoCategoryName);
        Assert.Equal("投票, 社区", post.VoTags);
        Assert.True(post.VoHasPoll);
        Assert.Equal(18, post.VoPollTotalVoteCount);
        Assert.False(post.VoPollIsClosed);
        Assert.Null(post.VoPoll);

        postServiceMock.Verify(service => service.GetForumPostPageAsync(
            null,
            1,
            20,
            "newest",
            null,
            null,
            null,
            null), Times.Once);
        postServiceMock.Verify(service => service.FillPostListMetadataAsync(It.IsAny<List<PostVo>>()), Times.Once);
        postServiceMock.Verify(service => service.GetPostDetailAsync(It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<string>()), Times.Never);
        attachmentServiceMock.Verify(service => service.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task GetById_Should_Return_Full_Poll_Detail_When_PostExists()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);
        var attachmentServiceMock = new Mock<IBaseService<Attachment, AttachmentVo>>(MockBehavior.Strict);
        var commentServiceMock = new Mock<IBaseService<Comment, CommentVo>>(MockBehavior.Strict);

        postServiceMock
            .Setup(service => service.IncrementViewCountAsync(9527))
            .Returns(Task.CompletedTask);
        postServiceMock
            .Setup(service => service.GetPostDetailAsync(9527, 10001, "default"))
            .ReturnsAsync(new PostVo
            {
                VoId = 9527,
                VoTitle = "详情投票帖",
                VoHasPoll = true,
                VoPollTotalVoteCount = 23,
                VoPollIsClosed = false,
                VoPoll = new PostPollVo
                {
                    VoPollId = 2001,
                    VoPostId = 9527,
                    VoQuestion = "今天喝什么？",
                    VoTotalVoteCount = 23,
                    VoOptions =
                    [
                        new PostPollOptionVo
                        {
                            VoOptionId = 3001,
                            VoOptionText = "奶茶",
                            VoVoteCount = 12,
                            VoVotePercent = 52.17m
                        }
                    ]
                }
            });
        commentServiceMock
            .Setup(service => service.QueryAsync(It.IsAny<Expression<Func<Comment, bool>>>()))
            .ReturnsAsync(new List<CommentVo>());

        var controller = CreateController(
            postServiceMock.Object,
            moderationServiceMock.Object,
            attachmentServiceMock.Object,
            commentServiceMock.Object);

        var result = await controller.GetById(9527);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);

        var post = Assert.IsType<PostVo>(result.ResponseData);
        Assert.True(post.VoHasPoll);
        Assert.NotNull(post.VoPoll);
        Assert.Equal("今天喝什么？", post.VoPoll!.VoQuestion);
        Assert.Single(post.VoPoll.VoOptions);
        postServiceMock.Verify(service => service.GetPostDetailAsync(9527, 10001, "default"), Times.Once);
    }

    [Fact]
    public async Task GetById_Should_Pass_AnswerSort_When_RequestContains_AnswerSort()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);
        var attachmentServiceMock = new Mock<IBaseService<Attachment, AttachmentVo>>(MockBehavior.Strict);
        var commentServiceMock = new Mock<IBaseService<Comment, CommentVo>>(MockBehavior.Strict);

        postServiceMock
            .Setup(service => service.IncrementViewCountAsync(9529))
            .Returns(Task.CompletedTask);
        postServiceMock
            .Setup(service => service.GetPostDetailAsync(9529, 10001, "latest"))
            .ReturnsAsync(new PostVo
            {
                VoId = 9529,
                VoTitle = "问答详情排序",
                VoIsQuestion = true,
                VoQuestion = new PostQuestionVo
                {
                    VoPostId = 9529,
                    VoAnswerCount = 2
                }
            });
        commentServiceMock
            .Setup(service => service.QueryAsync(It.IsAny<Expression<Func<Comment, bool>>>()))
            .ReturnsAsync(new List<CommentVo>());

        var controller = CreateController(
            postServiceMock.Object,
            moderationServiceMock.Object,
            attachmentServiceMock.Object,
            commentServiceMock.Object);

        var result = await controller.GetById(9529, "latest");

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        postServiceMock.Verify(service => service.GetPostDetailAsync(9529, 10001, "latest"), Times.Once);
    }

    [Fact]
    public async Task GetList_Should_Pass_TagSlug_To_ForumQuery()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);
        var attachmentServiceMock = new Mock<IBaseService<Attachment, AttachmentVo>>(MockBehavior.Strict);
        var commentServiceMock = new Mock<IBaseService<Comment, CommentVo>>(MockBehavior.Strict);

        var taggedPosts = new List<PostVo>
        {
            new()
            {
                VoId = 9601,
                VoTitle = "标签公开帖",
                VoAuthorId = 0,
                VoCreateTime = DateTime.UtcNow
            }
        };

        postServiceMock
            .Setup(service => service.GetForumPostPageAsync(
                null,
                1,
                20,
                "newest",
                null,
                null,
                null,
                "community-news"))
            .ReturnsAsync((taggedPosts, 1));
        postServiceMock
            .Setup(service => service.FillPostListMetadataAsync(It.Is<List<PostVo>>(items => ReferenceEquals(items, taggedPosts))))
            .Returns(Task.CompletedTask);
        commentServiceMock
            .Setup(service => service.QueryAsync(It.IsAny<Expression<Func<Comment, bool>>>()))
            .ReturnsAsync(new List<CommentVo>());

        var controller = CreateController(
            postServiceMock.Object,
            moderationServiceMock.Object,
            attachmentServiceMock.Object,
            commentServiceMock.Object);

        var result = await controller.GetList(tagSlug: "community-news");

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        postServiceMock.Verify(service => service.GetForumPostPageAsync(
            null,
            1,
            20,
            "newest",
            null,
            null,
            null,
            "community-news"), Times.Once);
    }

    [Fact]
    public async Task GetList_Should_Use_Question_Query_When_PostTypeIsQuestion()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);
        var attachmentServiceMock = new Mock<IBaseService<Attachment, AttachmentVo>>(MockBehavior.Strict);
        var commentServiceMock = new Mock<IBaseService<Comment, CommentVo>>(MockBehavior.Strict);

        var questionPosts = new List<PostVo>
        {
            new()
            {
                VoId = 9528,
                VoTitle = "待解决问答帖",
                VoIsQuestion = true,
                VoIsSolved = false,
                VoAnswerCount = 3,
                VoAuthorId = 0,
                VoCreateTime = DateTime.UtcNow
            }
        };

        postServiceMock
            .Setup(service => service.GetQuestionPostPageAsync(
                null,
                1,
                20,
                "pending",
                null,
                null,
                null,
                false,
                null))
            .ReturnsAsync((questionPosts, 1));
        postServiceMock
            .Setup(service => service.FillPostListMetadataAsync(It.Is<List<PostVo>>(items => ReferenceEquals(items, questionPosts))))
            .Returns(Task.CompletedTask);
        commentServiceMock
            .Setup(service => service.QueryAsync(It.IsAny<Expression<Func<Comment, bool>>>()))
            .ReturnsAsync(new List<CommentVo>());

        var controller = CreateController(
            postServiceMock.Object,
            moderationServiceMock.Object,
            attachmentServiceMock.Object,
            commentServiceMock.Object);

        var result = await controller.GetList(postType: "question", questionStatus: "pending", sortBy: "pending");

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);

        var pageModel = Assert.IsType<PageModel<PostVo>>(result.ResponseData);
        var post = Assert.Single(pageModel.Data);
        Assert.True(post.VoIsQuestion);
        Assert.False(post.VoIsSolved);
        Assert.Equal(3, post.VoAnswerCount);

        postServiceMock.Verify(service => service.GetQuestionPostPageAsync(
            null,
            1,
            20,
            "pending",
            null,
            null,
            null,
            false,
            null), Times.Once);
        postServiceMock.Verify(service => service.QueryPageAsync(
            It.IsAny<Expression<Func<Post, bool>>>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<Expression<Func<Post, object>>>(),
            It.IsAny<OrderByType>()), Times.Never);
    }

    [Fact]
    public async Task GetList_Should_Use_Poll_Query_When_PostTypeIsPoll()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);
        var attachmentServiceMock = new Mock<IBaseService<Attachment, AttachmentVo>>(MockBehavior.Strict);
        var commentServiceMock = new Mock<IBaseService<Comment, CommentVo>>(MockBehavior.Strict);

        var pollPosts = new List<PostVo>
        {
            new()
            {
                VoId = 9530,
                VoTitle = "投票视图帖子",
                VoHasPoll = true,
                VoPollTotalVoteCount = 6,
                VoAuthorId = 0,
                VoCreateTime = DateTime.UtcNow
            }
        };

        postServiceMock
            .Setup(service => service.GetPollPostPageAsync(
                null,
                1,
                20,
                "hottest",
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync((pollPosts, 1));
        postServiceMock
            .Setup(service => service.FillPostListMetadataAsync(It.Is<List<PostVo>>(items => ReferenceEquals(items, pollPosts))))
            .Returns(Task.CompletedTask);
        commentServiceMock
            .Setup(service => service.QueryAsync(It.IsAny<Expression<Func<Comment, bool>>>()))
            .ReturnsAsync(new List<CommentVo>());

        var controller = CreateController(
            postServiceMock.Object,
            moderationServiceMock.Object,
            attachmentServiceMock.Object,
            commentServiceMock.Object);

        var result = await controller.GetList(postType: "poll", sortBy: "hottest");

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);

        var pageModel = Assert.IsType<PageModel<PostVo>>(result.ResponseData);
        var post = Assert.Single(pageModel.Data);
        Assert.True(post.VoHasPoll);
        Assert.Equal(6, post.VoPollTotalVoteCount);

        postServiceMock.Verify(service => service.GetPollPostPageAsync(
            null,
            1,
            20,
            "hottest",
            null,
            null,
            null,
            null,
            null), Times.Once);
        postServiceMock.Verify(service => service.GetQuestionPostPageAsync(
            It.IsAny<long?>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<bool?>(),
            It.IsAny<string?>()), Times.Never);
        postServiceMock.Verify(service => service.QueryPageAsync(
            It.IsAny<Expression<Func<Post, bool>>>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<Expression<Func<Post, object>>>(),
            It.IsAny<OrderByType>()), Times.Never);
    }

    [Fact]
    public async Task GetList_Should_Pass_PollStatus_Filter_When_PostTypeIsPoll()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);
        var attachmentServiceMock = new Mock<IBaseService<Attachment, AttachmentVo>>(MockBehavior.Strict);
        var commentServiceMock = new Mock<IBaseService<Comment, CommentVo>>(MockBehavior.Strict);

        var pollPosts = new List<PostVo>
        {
            new()
            {
                VoId = 9531,
                VoTitle = "已截止投票帖",
                VoHasPoll = true,
                VoPollIsClosed = true,
                VoAuthorId = 0,
                VoCreateTime = DateTime.UtcNow
            }
        };

        postServiceMock
            .Setup(service => service.GetPollPostPageAsync(
                null,
                1,
                20,
                "newest",
                null,
                null,
                null,
                true,
                null))
            .ReturnsAsync((pollPosts, 1));
        postServiceMock
            .Setup(service => service.FillPostListMetadataAsync(It.Is<List<PostVo>>(items => ReferenceEquals(items, pollPosts))))
            .Returns(Task.CompletedTask);
        commentServiceMock
            .Setup(service => service.QueryAsync(It.IsAny<Expression<Func<Comment, bool>>>()))
            .ReturnsAsync(new List<CommentVo>());

        var controller = CreateController(
            postServiceMock.Object,
            moderationServiceMock.Object,
            attachmentServiceMock.Object,
            commentServiceMock.Object);

        var result = await controller.GetList(postType: "poll", pollStatus: "closed");

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);

        var pageModel = Assert.IsType<PageModel<PostVo>>(result.ResponseData);
        var post = Assert.Single(pageModel.Data);
        Assert.True(post.VoHasPoll);
        Assert.True(post.VoPollIsClosed);

        postServiceMock.Verify(service => service.GetPollPostPageAsync(
            null,
            1,
            20,
            "newest",
            null,
            null,
            null,
            true,
            null), Times.Once);
    }

    [Fact]
    public async Task GetList_Should_Pass_Votes_Sort_When_PostTypeIsPoll()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);
        var attachmentServiceMock = new Mock<IBaseService<Attachment, AttachmentVo>>(MockBehavior.Strict);
        var commentServiceMock = new Mock<IBaseService<Comment, CommentVo>>(MockBehavior.Strict);

        var pollPosts = new List<PostVo>
        {
            new()
            {
                VoId = 9532,
                VoTitle = "高票投票帖",
                VoHasPoll = true,
                VoPollTotalVoteCount = 88,
                VoAuthorId = 0,
                VoCreateTime = DateTime.UtcNow
            }
        };

        postServiceMock
            .Setup(service => service.GetPollPostPageAsync(
                null,
                1,
                20,
                "votes",
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync((pollPosts, 1));
        postServiceMock
            .Setup(service => service.FillPostListMetadataAsync(It.Is<List<PostVo>>(items => ReferenceEquals(items, pollPosts))))
            .Returns(Task.CompletedTask);
        commentServiceMock
            .Setup(service => service.QueryAsync(It.IsAny<Expression<Func<Comment, bool>>>()))
            .ReturnsAsync(new List<CommentVo>());

        var controller = CreateController(
            postServiceMock.Object,
            moderationServiceMock.Object,
            attachmentServiceMock.Object,
            commentServiceMock.Object);

        var result = await controller.GetList(postType: "poll", sortBy: "votes");

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);

        var pageModel = Assert.IsType<PageModel<PostVo>>(result.ResponseData);
        var post = Assert.Single(pageModel.Data);
        Assert.True(post.VoHasPoll);
        Assert.Equal(88, post.VoPollTotalVoteCount);

        postServiceMock.Verify(service => service.GetPollPostPageAsync(
            null,
            1,
            20,
            "votes",
            null,
            null,
            null,
            null,
            null), Times.Once);
    }

    [Fact]
    public async Task GetList_Should_Use_Lottery_Query_When_PostTypeIsLottery()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);
        var attachmentServiceMock = new Mock<IBaseService<Attachment, AttachmentVo>>(MockBehavior.Strict);
        var commentServiceMock = new Mock<IBaseService<Comment, CommentVo>>(MockBehavior.Strict);

        var lotteryPosts = new List<PostVo>
        {
            new()
            {
                VoId = 9534,
                VoTitle = "抽奖公开帖",
                VoHasLottery = true,
                VoLotteryParticipantCount = 32,
                VoAuthorId = 0,
                VoCreateTime = DateTime.UtcNow
            }
        };

        postServiceMock
            .Setup(service => service.GetLotteryPostPageAsync(
                null,
                1,
                20,
                "hottest",
                null,
                null,
                null,
                null))
            .ReturnsAsync((lotteryPosts, 1));
        postServiceMock
            .Setup(service => service.FillPostListMetadataAsync(It.Is<List<PostVo>>(items => ReferenceEquals(items, lotteryPosts))))
            .Returns(Task.CompletedTask);
        commentServiceMock
            .Setup(service => service.QueryAsync(It.IsAny<Expression<Func<Comment, bool>>>()))
            .ReturnsAsync(new List<CommentVo>());

        var controller = CreateController(
            postServiceMock.Object,
            moderationServiceMock.Object,
            attachmentServiceMock.Object,
            commentServiceMock.Object);

        var result = await controller.GetList(postType: "lottery", sortBy: "hottest");

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);

        var pageModel = Assert.IsType<PageModel<PostVo>>(result.ResponseData);
        var post = Assert.Single(pageModel.Data);
        Assert.True(post.VoHasLottery);
        Assert.Equal(32, post.VoLotteryParticipantCount);

        postServiceMock.Verify(service => service.GetLotteryPostPageAsync(
            null,
            1,
            20,
            "hottest",
            null,
            null,
            null,
            null), Times.Once);
    }

    [Fact]
    public async Task GetList_Should_Pass_Deadline_Sort_When_PostTypeIsPoll()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);
        var attachmentServiceMock = new Mock<IBaseService<Attachment, AttachmentVo>>(MockBehavior.Strict);
        var commentServiceMock = new Mock<IBaseService<Comment, CommentVo>>(MockBehavior.Strict);

        var pollPosts = new List<PostVo>
        {
            new()
            {
                VoId = 9533,
                VoTitle = "即将截止投票帖",
                VoHasPoll = true,
                VoPollTotalVoteCount = 12,
                VoAuthorId = 0,
                VoCreateTime = DateTime.UtcNow
            }
        };

        postServiceMock
            .Setup(service => service.GetPollPostPageAsync(
                null,
                1,
                20,
                "deadline",
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync((pollPosts, 1));
        postServiceMock
            .Setup(service => service.FillPostListMetadataAsync(It.Is<List<PostVo>>(items => ReferenceEquals(items, pollPosts))))
            .Returns(Task.CompletedTask);
        commentServiceMock
            .Setup(service => service.QueryAsync(It.IsAny<Expression<Func<Comment, bool>>>()))
            .ReturnsAsync(new List<CommentVo>());

        var controller = CreateController(
            postServiceMock.Object,
            moderationServiceMock.Object,
            attachmentServiceMock.Object,
            commentServiceMock.Object);

        var result = await controller.GetList(postType: "poll", sortBy: "deadline");

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);

        var pageModel = Assert.IsType<PageModel<PostVo>>(result.ResponseData);
        var post = Assert.Single(pageModel.Data);
        Assert.True(post.VoHasPoll);
        Assert.Equal(12, post.VoPollTotalVoteCount);

        postServiceMock.Verify(service => service.GetPollPostPageAsync(
            null,
            1,
            20,
            "deadline",
            null,
            null,
            null,
            null,
            null), Times.Once);
    }

    [Fact]
    public async Task GetEditHistory_Should_Return_PagedHistories_When_PostExists()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);
        var attachmentServiceMock = new Mock<IBaseService<Attachment, AttachmentVo>>(MockBehavior.Strict);
        var commentServiceMock = new Mock<IBaseService<Comment, CommentVo>>(MockBehavior.Strict);

        postServiceMock
            .Setup(service => service.QueryFirstAsync(It.IsAny<Expression<Func<Post, bool>>>()))
            .ReturnsAsync(new PostVo
            {
                VoId = 9527,
                VoTitle = "问答帖"
            });
        postServiceMock
            .Setup(service => service.GetPostEditHistoryPageAsync(9527, 1, 10))
            .ReturnsAsync((
                new List<PostEditHistoryVo>
                {
                    new()
                    {
                        VoId = 1,
                        VoPostId = 9527,
                        VoEditSequence = 1,
                        VoOldTitle = "旧标题",
                        VoNewTitle = "新标题",
                        VoOldContent = "旧内容",
                        VoNewContent = "新内容",
                        VoEditorId = 10001,
                        VoEditorName = "Tester",
                        VoEditedAt = DateTime.UtcNow,
                        VoCreateTime = DateTime.UtcNow
                    }
                },
                1));

        var controller = CreateController(
            postServiceMock.Object,
            moderationServiceMock.Object,
            attachmentServiceMock.Object,
            commentServiceMock.Object);

        var result = await controller.GetEditHistory(9527, 1, 10);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);

        var page = Assert.IsType<VoPagedResult<PostEditHistoryVo>>(result.ResponseData);
        Assert.Single(page.VoItems);
        Assert.Equal(1, page.VoTotal);
        Assert.Equal(1, page.VoPageIndex);
        Assert.Equal(10, page.VoPageSize);
        Assert.Equal("旧标题", page.VoItems[0].VoOldTitle);
        Assert.Equal("新内容", page.VoItems[0].VoNewContent);

        postServiceMock.Verify(service => service.GetPostEditHistoryPageAsync(9527, 1, 10), Times.Once);
    }

    [Fact]
    public async Task GetEditHistory_Should_Return_NotFound_When_PostDoesNotExist()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);
        var attachmentServiceMock = new Mock<IBaseService<Attachment, AttachmentVo>>(MockBehavior.Strict);
        var commentServiceMock = new Mock<IBaseService<Comment, CommentVo>>(MockBehavior.Strict);

        postServiceMock
            .Setup(service => service.QueryFirstAsync(It.IsAny<Expression<Func<Post, bool>>>()))
            .ReturnsAsync((PostVo?)null);

        var controller = CreateController(
            postServiceMock.Object,
            moderationServiceMock.Object,
            attachmentServiceMock.Object,
            commentServiceMock.Object);

        var result = await controller.GetEditHistory(9527, 1, 10);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("帖子不存在", result.MessageInfo);

        postServiceMock.Verify(service => service.GetPostEditHistoryPageAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Publish_Should_Return_BadRequest_When_ServiceRejects_InvalidPoll()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);
        var attachmentServiceMock = new Mock<IBaseService<Attachment, AttachmentVo>>(MockBehavior.Strict);
        var commentServiceMock = new Mock<IBaseService<Comment, CommentVo>>(MockBehavior.Strict);

        moderationServiceMock
            .Setup(service => service.GetPublishPermissionAsync(10001))
            .ReturnsAsync(new ContentModerationPermissionVo
            {
                VoUserId = 10001,
                VoCanPublish = true
            });
        postServiceMock
            .Setup(service => service.PublishPostAsync(
                It.IsAny<Post>(),
                It.Is<CreatePollDto?>(poll => poll != null && poll.Question == "重复选项"),
                It.IsAny<CreateLotteryDto?>(),
                false,
                It.Is<List<string>>(tags => tags.Count == 1 && tags[0] == "投票"),
                false))
            .ThrowsAsync(new ArgumentException("投票选项不能重复", "poll"));

        var controller = CreateController(
            postServiceMock.Object,
            moderationServiceMock.Object,
            attachmentServiceMock.Object,
            commentServiceMock.Object);

        var result = await controller.Publish(new PublishPostDto
        {
            Title = "发帖带投票",
            Content = "正文",
            CategoryId = 1,
            TagNames = ["投票"],
            Poll = new CreatePollDto
            {
                Question = "重复选项",
                Options =
                [
                    new PollOptionDto { OptionText = "奶茶" },
                    new PollOptionDto { OptionText = "奶茶" }
                ]
            }
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("投票选项不能重复", result.MessageInfo);
    }

    [Fact]
    public async Task SetTop_Should_Return_Updated_Post_When_CurrentUser_Is_Admin()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);
        var attachmentServiceMock = new Mock<IBaseService<Attachment, AttachmentVo>>(MockBehavior.Strict);
        var commentServiceMock = new Mock<IBaseService<Comment, CommentVo>>(MockBehavior.Strict);

        postServiceMock
            .Setup(service => service.SetTopAsync(9527, true, 10001, "Tester"))
            .ReturnsAsync(new PostVo
            {
                VoId = 9527,
                VoTitle = "管理员置顶帖",
                VoIsTop = true
            });

        var controller = CreateController(
            postServiceMock.Object,
            moderationServiceMock.Object,
            attachmentServiceMock.Object,
            commentServiceMock.Object,
            new CurrentUser
            {
                UserId = 10001,
                UserName = "Tester",
                TenantId = 0,
                Roles = [UserRoles.Admin]
            });

        var result = await controller.SetTop(new SetPostTopDto
        {
            PostId = 9527,
            IsTop = true
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("置顶成功", result.MessageInfo);

        var post = Assert.IsType<PostVo>(result.ResponseData);
        Assert.Equal(9527, post.VoId);
        Assert.True(post.VoIsTop);

        postServiceMock.Verify(service => service.SetTopAsync(9527, true, 10001, "Tester"), Times.Once);
    }

    [Fact]
    public async Task SetTop_Should_Return_Forbidden_When_CurrentUser_Is_Not_Admin()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);
        var attachmentServiceMock = new Mock<IBaseService<Attachment, AttachmentVo>>(MockBehavior.Strict);
        var commentServiceMock = new Mock<IBaseService<Comment, CommentVo>>(MockBehavior.Strict);

        var controller = CreateController(
            postServiceMock.Object,
            moderationServiceMock.Object,
            attachmentServiceMock.Object,
            commentServiceMock.Object,
            new CurrentUser
            {
                UserId = 10002,
                UserName = "Member",
                TenantId = 0,
                Roles = Array.Empty<string>()
            });

        var result = await controller.SetTop(new SetPostTopDto
        {
            PostId = 9527,
            IsTop = true
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("无权置顶帖子", result.MessageInfo);

        postServiceMock.Verify(service => service.SetTopAsync(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    private static PostController CreateController(
        IPostService postService,
        IContentModerationService moderationService,
        IBaseService<Attachment, AttachmentVo> attachmentService,
        IBaseService<Comment, CommentVo> commentService,
        CurrentUser? currentUser = null)
    {
        Mock.Get(postService)
            .Setup(service => service.FillPostAvatarAndInteractorsAsync(It.IsAny<List<PostVo>>()))
            .Returns(Task.CompletedTask);

        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(accessor => accessor.Current).Returns(currentUser ?? new CurrentUser
        {
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0
        });
        var browseHistoryServiceMock = new Mock<IUserBrowseHistoryService>();
        browseHistoryServiceMock
            .Setup(service => service.RecordAsync(It.IsAny<RecordBrowseHistoryDto>()))
            .Returns(Task.CompletedTask);

        var attachmentServiceAdapter = attachmentService as IAttachmentService
            ?? new Mock<IAttachmentService>(MockBehavior.Strict).Object;

        return new PostController(
            postService,
            moderationService,
            attachmentServiceAdapter,
            commentService,
            browseHistoryServiceMock.Object,
            currentUserAccessorMock.Object);
    }
}
