using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.Constants;
using Xunit;

namespace Radish.Api.Tests.Services;

public class ForumContentWriteServiceTest
{
    [Fact]
    public async Task PublishPostAsync_Should_Not_Mark_Failed_When_Post_Created_But_Completion_First_Fails()
    {
        var contentSubmissionService = CreateContentSubmissionService();
        contentSubmissionService
            .SetupSequence(s => s.CompleteSuccessAsync(It.IsAny<ContentSubmissionCompletionRequest>()))
            .ThrowsAsync(new InvalidOperationException("temporary completion failure"))
            .Returns(Task.CompletedTask);

        var postService = new Mock<IPostService>(MockBehavior.Strict);
        postService
            .Setup(s => s.PublishPostAsync(
                It.IsAny<Post>(),
                It.IsAny<CreatePollDto?>(),
                It.IsAny<CreateLotteryDto?>(),
                It.IsAny<bool>(),
                It.IsAny<List<string>?>(),
                It.IsAny<bool>()))
            .ReturnsAsync(9001);

        var service = CreateService(contentSubmissionService, postService);

        var result = await service.PublishPostAsync(
            CreatePost(),
            poll: null,
            lottery: null,
            isQuestion: false,
            tagNames: ["Radish"],
            allowCreateTag: true,
            clientSubmissionId: "forum-post:abc");

        Assert.Equal(ContentWriteStatus.Created, result.Status);
        Assert.Equal(9001, result.Result);

        contentSubmissionService.Verify(
            s => s.CompleteSuccessAsync(It.Is<ContentSubmissionCompletionRequest>(request =>
                request.RecordId == 7001 &&
                request.ResultType == ContentSubmissionResultTypes.Post &&
                request.ResultId == 9001 &&
                request.ResultPublicId == "pst_test")),
            Times.Exactly(2));
        contentSubmissionService.Verify(
            s => s.CompleteFailureAsync(It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishPostAsync_Should_Mark_Failed_When_Post_Create_Fails()
    {
        var contentSubmissionService = CreateContentSubmissionService();
        contentSubmissionService
            .Setup(s => s.CompleteFailureAsync(7001, "InvalidOperationException", "publish failed"))
            .Returns(Task.CompletedTask);

        var postService = new Mock<IPostService>(MockBehavior.Strict);
        postService
            .Setup(s => s.PublishPostAsync(
                It.IsAny<Post>(),
                It.IsAny<CreatePollDto?>(),
                It.IsAny<CreateLotteryDto?>(),
                It.IsAny<bool>(),
                It.IsAny<List<string>?>(),
                It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException("publish failed"));

        var service = CreateService(contentSubmissionService, postService);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PublishPostAsync(
            CreatePost(),
            poll: null,
            lottery: null,
            isQuestion: false,
            tagNames: ["Radish"],
            allowCreateTag: true,
            clientSubmissionId: "forum-post:abc"));

        Assert.Equal("publish failed", exception.Message);
        contentSubmissionService.Verify(
            s => s.CompleteSuccessAsync(It.IsAny<ContentSubmissionCompletionRequest>()),
            Times.Never);
        contentSubmissionService.Verify(
            s => s.CompleteFailureAsync(7001, "InvalidOperationException", "publish failed"),
            Times.Once);
    }

    [Fact]
    public async Task UpdatePostAsync_Should_Return_NoChange_When_Request_Matches_Current_Post()
    {
        var contentSubmissionService = CreateContentSubmissionService();
        contentSubmissionService
            .Setup(s => s.CompleteSuccessAsync(It.IsAny<ContentSubmissionCompletionRequest>()))
            .Returns(Task.CompletedTask);

        var postService = new Mock<IPostService>(MockBehavior.Strict);
        postService
            .Setup(s => s.GetPostDetailAsync(2001, 42, "default"))
            .ReturnsAsync(new PostVo
            {
                VoId = 2001,
                VoPublicId = "pst_2001",
                VoTitle = "原帖子标题",
                VoContent = "原帖子内容",
                VoCategoryId = 101,
                VoTags = "Radish,治理"
            });

        var service = CreateService(contentSubmissionService, postService);

        var result = await service.UpdatePostAsync(
            tenantId: 9,
            postId: 2001,
            title: " 原帖子标题 ",
            content: "原帖子内容",
            categoryId: 101,
            tagNames: ["治理", "radish"],
            allowCreateTag: false,
            operatorId: 42,
            operatorName: "Owner",
            isAdmin: false,
            clientSubmissionId: "forum-post-edit:abc");

        Assert.Equal(ContentWriteStatus.NoChange, result.Status);
        Assert.Equal(2001, result.Result.PostId);
        contentSubmissionService.Verify(
            s => s.BeginAsync(It.Is<ContentSubmissionBeginRequest>(request =>
                request.TenantId == 9 &&
                request.UserId == 42 &&
                request.OperationType == ContentSubmissionOperationTypes.ForumPostEdit &&
                request.TargetType == "Post" &&
                request.TargetId == 2001 &&
                request.DuplicateWindowSeconds == 60)),
            Times.Once);
        contentSubmissionService.Verify(
            s => s.CompleteSuccessAsync(It.Is<ContentSubmissionCompletionRequest>(request =>
                request.RecordId == 7001 &&
                request.ResultType == ContentSubmissionResultTypes.Post &&
                request.ResultId == 2001 &&
                request.ResultPublicId == "pst_2001")),
            Times.Once);
        postService.Verify(
            s => s.UpdatePostAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<long?>(),
                It.IsAny<List<string>?>(),
                It.IsAny<bool>(),
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateCommentAsync_Should_Return_NoChange_When_Request_Matches_Current_Comment()
    {
        var contentSubmissionService = CreateContentSubmissionService();
        contentSubmissionService
            .Setup(s => s.CompleteSuccessAsync(It.IsAny<ContentSubmissionCompletionRequest>()))
            .Returns(Task.CompletedTask);

        var commentService = new Mock<ICommentService>(MockBehavior.Strict);
        commentService
            .Setup(s => s.QueryFirstAsync(It.IsAny<Expression<Func<Comment, bool>>?>()))
            .ReturnsAsync(new CommentVo
            {
                VoId = 3001,
                VoPostId = 2001,
                VoParentId = null,
                VoContent = "原评论内容"
            });

        var service = CreateService(
            contentSubmissionService,
            new Mock<IPostService>(MockBehavior.Strict),
            commentService);

        var result = await service.UpdateCommentAsync(
            tenantId: 9,
            commentId: 3001,
            content: " 原评论内容 ",
            operatorId: 42,
            operatorName: "Owner",
            isAdmin: false,
            clientSubmissionId: "forum-comment-edit:abc");

        Assert.Equal(ContentWriteStatus.NoChange, result.Status);
        Assert.Equal(3001, result.Result.CommentId);
        Assert.Equal(2001, result.Result.PostId);
        contentSubmissionService.Verify(
            s => s.BeginAsync(It.Is<ContentSubmissionBeginRequest>(request =>
                request.TenantId == 9 &&
                request.UserId == 42 &&
                request.OperationType == ContentSubmissionOperationTypes.ForumCommentEdit &&
                request.TargetType == "Comment" &&
                request.TargetId == 3001 &&
                request.DuplicateWindowSeconds == 60)),
            Times.Once);
        contentSubmissionService.Verify(
            s => s.CompleteSuccessAsync(It.Is<ContentSubmissionCompletionRequest>(request =>
                request.RecordId == 7001 &&
                request.ResultType == ContentSubmissionResultTypes.Comment &&
                request.ResultId == 3001)),
            Times.Once);
        commentService.Verify(
            s => s.UpdateCommentAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateCommentAsync_Should_Replay_Succeeded_Submission_Without_Updating()
    {
        var contentSubmissionService = CreateContentSubmissionService();
        contentSubmissionService
            .Setup(s => s.BeginAsync(It.IsAny<ContentSubmissionBeginRequest>()))
            .ReturnsAsync(new ContentSubmissionBeginResult
            {
                Status = ContentSubmissionBeginStatus.Succeeded,
                ResultType = ContentSubmissionResultTypes.Comment,
                ResultId = 3001,
                Message = "已提交过相同编辑"
            });

        var commentService = new Mock<ICommentService>(MockBehavior.Strict);
        commentService
            .Setup(s => s.QueryFirstAsync(It.IsAny<Expression<Func<Comment, bool>>?>()))
            .ReturnsAsync(new CommentVo
            {
                VoId = 3001,
                VoPostId = 2001,
                VoParentId = 1001,
                VoContent = "原评论内容"
            });

        var service = CreateService(
            contentSubmissionService,
            new Mock<IPostService>(MockBehavior.Strict),
            commentService);

        var result = await service.UpdateCommentAsync(
            tenantId: 9,
            commentId: 3001,
            content: "编辑后的评论内容",
            operatorId: 42,
            operatorName: "Owner",
            isAdmin: false,
            clientSubmissionId: "forum-comment-edit:abc");

        Assert.Equal(ContentWriteStatus.Replayed, result.Status);
        Assert.Equal(3001, result.Result.CommentId);
        Assert.Equal(2001, result.Result.PostId);
        Assert.Equal(1001, result.Result.ParentId);
        contentSubmissionService.Verify(
            s => s.CompleteSuccessAsync(It.IsAny<ContentSubmissionCompletionRequest>()),
            Times.Never);
        contentSubmissionService.Verify(
            s => s.CompleteFailureAsync(It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);
        commentService.Verify(
            s => s.UpdateCommentAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    private static Mock<IContentSubmissionService> CreateContentSubmissionService()
    {
        var contentSubmissionService = new Mock<IContentSubmissionService>(MockBehavior.Strict);
        contentSubmissionService
            .Setup(s => s.CreateRequestSnapshot(
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>()))
            .Returns(new ContentSubmissionRequestSnapshot
            {
                RequestDigest = "digest-a",
                RequestSummary = "summary-a",
                ContentFingerprint = "fingerprint-a"
            });
        contentSubmissionService
            .Setup(s => s.BeginAsync(It.IsAny<ContentSubmissionBeginRequest>()))
            .ReturnsAsync(new ContentSubmissionBeginResult
            {
                Status = ContentSubmissionBeginStatus.Started,
                RecordId = 7001
            });

        return contentSubmissionService;
    }

    private static ForumContentWriteService CreateService(
        Mock<IContentSubmissionService> contentSubmissionService,
        Mock<IPostService> postService,
        Mock<ICommentService>? commentService = null)
    {
        return new ForumContentWriteService(
            contentSubmissionService.Object,
            postService.Object,
            (commentService ?? new Mock<ICommentService>(MockBehavior.Strict)).Object);
    }

    private static Post CreatePost()
    {
        return new Post
        {
            TenantId = 0,
            AuthorId = 42,
            AuthorName = "萝卜",
            CategoryId = 1001,
            Title = "测试帖子",
            Content = "测试内容",
            ContentType = "Markdown",
            PublicId = "pst_test"
        };
    }
}
