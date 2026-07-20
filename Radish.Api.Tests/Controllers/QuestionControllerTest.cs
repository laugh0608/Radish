using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Moq;
using Radish.Api.Controllers;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(QuestionController))]
public class QuestionControllerTest
{
    [Fact]
    public async Task Answer_Should_Return_UpdatedQuestionDetail_When_RequestIsValid()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);

        moderationServiceMock
            .Setup(service => service.GetPublishPermissionAsync(10001))
            .ReturnsAsync(new ContentModerationPermissionVo
            {
                VoUserId = 10001,
                VoCanPublish = true
            });
        postServiceMock
            .Setup(service => service.AddAnswerAsync(9527, "给出排查步骤", 10001, "Tester", 0))
            .ReturnsAsync(new PostQuestionVo
            {
                VoPostId = 9527,
                VoIsSolved = false,
                VoAnswerCount = 1,
                VoAnswers =
                [
                    new PostAnswerVo
                    {
                        VoAnswerId = 3001,
                        VoPostId = 9527,
                        VoAuthorId = 10001,
                        VoAuthorName = "Tester",
                        VoContent = "给出排查步骤",
                        VoIsAccepted = false
                    }
                ]
            });

        var controller = CreateController(postServiceMock.Object, moderationServiceMock.Object);

        var result = await controller.Answer(new CreateAnswerDto
        {
            PostId = 9527,
            Content = "给出排查步骤"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);

        var question = Assert.IsType<PostQuestionVo>(result.ResponseData);
        Assert.Equal(9527, question.VoPostId);
        Assert.Single(question.VoAnswers);
        Assert.Equal("给出排查步骤", question.VoAnswers[0].VoContent);
    }

    [Fact]
    public async Task Answer_Should_Return_BadRequest_When_PostIsNotQuestion()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);

        moderationServiceMock
            .Setup(service => service.GetPublishPermissionAsync(10001))
            .ReturnsAsync(new ContentModerationPermissionVo
            {
                VoUserId = 10001,
                VoCanPublish = true
            });
        postServiceMock
            .Setup(service => service.AddAnswerAsync(9528, "普通帖不能回答", 10001, "Tester", 0))
            .ThrowsAsync(new BusinessException("当前帖子不是问答帖", 400, "Forum.NotQuestionPost"));

        var controller = CreateController(postServiceMock.Object, moderationServiceMock.Object);

        var result = await controller.Answer(new CreateAnswerDto
        {
            PostId = 9528,
            Content = "普通帖不能回答"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("当前帖子不是问答帖", result.MessageInfo);
    }

    [Fact]
    public async Task Answer_Should_Unwrap_AggregateException_To_NotFound_When_PostDoesNotExist()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);

        moderationServiceMock
            .Setup(service => service.GetPublishPermissionAsync(10001))
            .ReturnsAsync(new ContentModerationPermissionVo
            {
                VoUserId = 10001,
                VoCanPublish = true
            });
        postServiceMock
            .Setup(service => service.AddAnswerAsync(9999, "帖子已经不存在", 10001, "Tester", 0))
            .ThrowsAsync(new AggregateException(new BusinessException(
                "问答帖不存在",
                404,
                "Forum.QuestionNotFound",
                "error.forum.question_not_found")));

        var controller = CreateController(postServiceMock.Object, moderationServiceMock.Object);

        var result = await controller.Answer(new CreateAnswerDto
        {
            PostId = 9999,
            Content = "帖子已经不存在"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("问答帖不存在", result.MessageInfo);
        Assert.Equal("Forum.QuestionNotFound", result.Code);
        Assert.Equal("error.forum.question_not_found", result.MessageKey);
        Assert.Null(result.MessageArguments);
    }

    [Theory]
    [InlineData(
        409,
        ForumPublishErrorCodes.SubmissionProcessing,
        "error.forum.publish_submission_processing",
        null)]
    [InlineData(
        429,
        ForumPublishErrorCodes.RateLimited,
        "error.forum.publish_rate_limited",
        17L)]
    public async Task Answer_Should_Preserve_BusinessErrorContract(
        int statusCode,
        string errorCode,
        string messageKey,
        long? retryAfterSeconds)
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);
        var forumContentWriteServiceMock = new Mock<IForumContentWriteService>(MockBehavior.Strict);

        moderationServiceMock
            .Setup(service => service.GetPublishPermissionAsync(10001))
            .ReturnsAsync(new ContentModerationPermissionVo
            {
                VoUserId = 10001,
                VoCanPublish = true
            });

        var messageArguments = retryAfterSeconds.HasValue
            ? new object[] { retryAfterSeconds.Value }
            : [];
        forumContentWriteServiceMock
            .Setup(service => service.AddAnswerAsync(
                9527,
                "给出排查步骤",
                10001,
                "Tester",
                0,
                "answer-submission-1"))
            .ThrowsAsync(new BusinessException(
                "操作暂时无法完成",
                statusCode,
                errorCode,
                messageKey,
                messageArguments));

        var controller = CreateController(
            postServiceMock.Object,
            moderationServiceMock.Object,
            forumContentWriteServiceMock.Object);

        var result = await controller.Answer(new CreateAnswerDto
        {
            PostId = 9527,
            Content = "给出排查步骤",
            ClientSubmissionId = "answer-submission-1"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(statusCode, result.StatusCode);
        Assert.Equal(errorCode, result.Code);
        Assert.Equal(messageKey, result.MessageKey);
        Assert.Equal(messageArguments.Length == 0 ? null : messageArguments, result.MessageArguments);
    }

    [Fact]
    public async Task Answer_Should_Return_Forbidden_When_PublishPermissionIsDenied()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);

        moderationServiceMock
            .Setup(service => service.GetPublishPermissionAsync(10001))
            .ReturnsAsync(new ContentModerationPermissionVo
            {
                VoUserId = 10001,
                VoCanPublish = false,
                VoDenyReason = "当前状态无法发布内容"
            });

        var controller = CreateController(postServiceMock.Object, moderationServiceMock.Object);

        var result = await controller.Answer(new CreateAnswerDto
        {
            PostId = 9527,
            Content = "这条回答不会被提交"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Contains("当前状态无法发布内容", result.MessageInfo);

        postServiceMock.Verify(
            service => service.AddAnswerAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task Accept_Should_Return_UpdatedQuestionDetail_When_RequestIsValid()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);

        postServiceMock
            .Setup(service => service.AcceptAnswerAsync(9527, 3001, 10001, "Tester"))
            .ReturnsAsync(new PostQuestionVo
            {
                VoPostId = 9527,
                VoIsSolved = true,
                VoAcceptedAnswerId = 3001,
                VoAnswerCount = 2,
                VoAnswers =
                [
                    new PostAnswerVo
                    {
                        VoAnswerId = 3001,
                        VoPostId = 9527,
                        VoAuthorId = 20001,
                        VoAuthorName = "Alice",
                        VoContent = "最终方案",
                        VoIsAccepted = true
                    }
                ]
            });

        var controller = CreateController(postServiceMock.Object, moderationServiceMock.Object);

        var result = await controller.Accept(new AcceptAnswerDto
        {
            PostId = 9527,
            AnswerId = 3001
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);

        var question = Assert.IsType<PostQuestionVo>(result.ResponseData);
        Assert.True(question.VoIsSolved);
        Assert.Equal(3001, question.VoAcceptedAnswerId);
        Assert.True(question.VoAnswers[0].VoIsAccepted);
    }

    [Fact]
    public async Task Accept_Should_Return_Forbidden_When_CurrentUserIsNotOwner()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);

        postServiceMock
            .Setup(service => service.AcceptAnswerAsync(9527, 3001, 10001, "Tester"))
            .ThrowsAsync(new BusinessException(
                "只有提问者可以采纳答案",
                403,
                "Forum.AnswerAcceptForbidden",
                "error.forum.answer_accept_forbidden"));

        var controller = CreateController(postServiceMock.Object, moderationServiceMock.Object);

        var result = await controller.Accept(new AcceptAnswerDto
        {
            PostId = 9527,
            AnswerId = 3001
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Contains("只有提问者可以采纳答案", result.MessageInfo);
        Assert.Equal("Forum.AnswerAcceptForbidden", result.Code);
        Assert.Equal("error.forum.answer_accept_forbidden", result.MessageKey);
    }

    [Fact]
    public async Task Accept_Should_Unwrap_AggregateException_To_BadRequest_When_AcceptingOwnAnswer()
    {
        var postServiceMock = new Mock<IPostService>(MockBehavior.Strict);
        var moderationServiceMock = new Mock<IContentModerationService>(MockBehavior.Strict);

        postServiceMock
            .Setup(service => service.AcceptAnswerAsync(9527, 3001, 10001, "Tester"))
            .ThrowsAsync(new AggregateException(new BusinessException(
                "不能采纳自己的回答",
                400,
                "Forum.CannotAcceptOwnAnswer",
                "error.forum.cannot_accept_own_answer")));

        var controller = CreateController(postServiceMock.Object, moderationServiceMock.Object);

        var result = await controller.Accept(new AcceptAnswerDto
        {
            PostId = 9527,
            AnswerId = 3001
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("不能采纳自己的回答", result.MessageInfo);
        Assert.Equal("Forum.CannotAcceptOwnAnswer", result.Code);
        Assert.Equal("error.forum.cannot_accept_own_answer", result.MessageKey);
    }

    private static QuestionController CreateController(
        IPostService postService,
        IContentModerationService moderationService,
        IForumContentWriteService? forumContentWriteService = null)
    {
        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(accessor => accessor.Current).Returns(new CurrentUser
        {
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0
        });
        var forumContentWriteServiceMock = new Mock<IForumContentWriteService>(MockBehavior.Strict);
        forumContentWriteServiceMock
            .Setup(service => service.AddAnswerAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<string?>()))
            .Returns(async (
                long postId,
                string content,
                long authorId,
                string authorName,
                long tenantId,
                string? _) =>
                ContentWriteResult<PostQuestionVo>.CreatedResult(await postService.AddAnswerAsync(
                    postId,
                    content,
                    authorId,
                    authorName,
                    tenantId)));

        return new QuestionController(
            postService,
            moderationService,
            currentUserAccessorMock.Object,
            forumContentWriteService ?? forumContentWriteServiceMock.Object);
    }
}
