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
            .Setup(service => service.QueryPageAsync(
                It.IsAny<Expression<Func<Post, bool>>>(),
                1,
                20,
                It.IsAny<Expression<Func<Post, object>>>(),
                OrderByType.Desc))
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
                false))
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
            false), Times.Once);
        postServiceMock.Verify(service => service.QueryPageAsync(
            It.IsAny<Expression<Func<Post, bool>>>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<Expression<Func<Post, object>>>(),
            It.IsAny<OrderByType>()), Times.Never);
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

    private static PostController CreateController(
        IPostService postService,
        IContentModerationService moderationService,
        IBaseService<Attachment, AttachmentVo> attachmentService,
        IBaseService<Comment, CommentVo> commentService)
    {
        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(accessor => accessor.Current).Returns(new CurrentUser
        {
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0
        });

        return new PostController(
            postService,
            moderationService,
            attachmentService,
            commentService,
            currentUserAccessorMock.Object);
    }
}
