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
public sealed class QuestionControllerTest
{
    [Fact]
    public async Task Answer_ShouldReturnAnswerMutation_WhenRequestIsValid()
    {
        var questionService = new Mock<IForumQuestionService>(MockBehavior.Strict);
        var moderationService = AllowPublishing();
        questionService
            .Setup(service => service.CreateAnswerAsync(
                0,
                "pst_question",
                "给出排查步骤",
                10001,
                "Tester",
                "answer-submission-1"))
            .ReturnsAsync(new PostAnswerMutationVo
            {
                VoPostPublicId = "pst_question",
                VoAnswerCount = 1,
                VoAnswer = new PostAnswerVo
                {
                    VoAnswerId = 3001,
                    VoPublicId = "ans_0123456789abcdef0123456789abcdef",
                    VoContent = "给出排查步骤",
                    VoContentRevision = 1
                }
            });

        var controller = CreateController(moderationService.Object, questionService.Object);
        var result = await controller.Answer(new CreateAnswerDto
        {
            PostIdentifier = "pst_question",
            Content = "给出排查步骤",
            ClientSubmissionId = "answer-submission-1"
        });

        Assert.True(result.IsSuccess);
        var mutation = Assert.IsType<PostAnswerMutationVo>(result.ResponseData);
        Assert.Equal("ans_0123456789abcdef0123456789abcdef", mutation.VoAnswer.VoPublicId);
        Assert.Equal(1, mutation.VoAnswer.VoContentRevision);
    }

    [Fact]
    public async Task Answer_ShouldPreserveBusinessErrorContract()
    {
        var questionService = new Mock<IForumQuestionService>(MockBehavior.Strict);
        var moderationService = AllowPublishing();
        questionService
            .Setup(service => service.CreateAnswerAsync(
                0,
                "pst_question",
                "给出排查步骤",
                10001,
                "Tester",
                "answer-submission-1"))
            .ThrowsAsync(new BusinessException(
                "请求正在处理中",
                409,
                ForumQuestionErrorCodes.Conflict,
                ForumQuestionErrorCodes.ResolveMessageKey(ForumQuestionErrorCodes.Conflict)));

        var controller = CreateController(moderationService.Object, questionService.Object);
        var result = await controller.Answer(new CreateAnswerDto
        {
            PostIdentifier = "pst_question",
            Content = "给出排查步骤",
            ClientSubmissionId = "answer-submission-1"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(ForumQuestionErrorCodes.Conflict, result.Code);
        Assert.Equal("error.forum.answer_revision_conflict", result.MessageKey);
    }

    [Fact]
    public async Task Answer_ShouldNotWrite_WhenPublishPermissionIsDenied()
    {
        var questionService = new Mock<IForumQuestionService>(MockBehavior.Strict);
        var moderationService = new Mock<IContentModerationService>(MockBehavior.Strict);
        moderationService
            .Setup(service => service.GetPublishPermissionAsync(10001))
            .ReturnsAsync(new ContentModerationPermissionVo
            {
                VoUserId = 10001,
                VoCanPublish = false,
                VoDenyReason = "当前状态无法发布内容"
            });

        var controller = CreateController(moderationService.Object, questionService.Object);
        var result = await controller.Answer(new CreateAnswerDto
        {
            PostIdentifier = "pst_question",
            Content = "这条回答不会被提交"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        questionService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Answer_ShouldKeepLegacyLongIdInsideControllerCompatibilityBoundary()
    {
        var questionService = new Mock<IForumQuestionService>(MockBehavior.Strict);
        var moderationService = AllowPublishing();
        questionService
            .Setup(service => service.CreateAnswerAsync(
                0,
                "9527",
                "兼容旧客户端",
                10001,
                "Tester",
                "legacy-answer-submission"))
            .ReturnsAsync(new PostAnswerMutationVo
            {
                VoPostPublicId = "pst_question",
                VoAnswerCount = 1,
                VoAnswer = new PostAnswerVo
                {
                    VoAnswerId = 2042219067430928384,
                    VoPublicId = "ans_0123456789abcdef0123456789abcdef",
                    VoPostId = 9527,
                    VoAuthorId = 10001,
                    VoAuthorName = "Tester",
                    VoContent = "兼容旧客户端"
                }
            });

        var controller = CreateController(moderationService.Object, questionService.Object);
        var result = await controller.Answer(new CreateAnswerDto
        {
            PostId = 9527,
            Content = "兼容旧客户端",
            ClientSubmissionId = "legacy-answer-submission"
        });

        Assert.True(result.IsSuccess);
        questionService.VerifyAll();
    }

    [Fact]
    public async Task Accept_ShouldUsePublicIdAndAcceptanceCas()
    {
        var questionService = new Mock<IForumQuestionService>(MockBehavior.Strict);
        var moderationService = new Mock<IContentModerationService>(MockBehavior.Strict);
        questionService
            .Setup(service => service.AcceptAnswerAsync(
                0,
                "pst_question",
                "ans_0123456789abcdef0123456789abcdef",
                2,
                10001,
                "Tester",
                "accept-operation-1"))
            .ReturnsAsync(new PostAnswerAcceptanceMutationVo
            {
                VoPostPublicId = "pst_question",
                VoAcceptedAnswerPublicId = "ans_0123456789abcdef0123456789abcdef",
                VoAcceptanceRevision = 3,
                VoIsSolved = true
            });

        var controller = CreateController(moderationService.Object, questionService.Object);
        var result = await controller.Accept(new ChangePostAnswerAcceptanceDto
        {
            PostIdentifier = "pst_question",
            AnswerPublicId = "ans_0123456789abcdef0123456789abcdef",
            ExpectedAcceptanceRevision = 2,
            ClientSubmissionId = "accept-operation-1"
        });

        Assert.True(result.IsSuccess);
        var mutation = Assert.IsType<PostAnswerAcceptanceMutationVo>(result.ResponseData);
        Assert.Equal(3, mutation.VoAcceptanceRevision);
        Assert.True(mutation.VoIsSolved);
    }

    [Fact]
    public async Task Accept_ShouldPreserveAcceptanceConflict()
    {
        var questionService = new Mock<IForumQuestionService>(MockBehavior.Strict);
        var moderationService = new Mock<IContentModerationService>(MockBehavior.Strict);
        questionService
            .Setup(service => service.AcceptAnswerAsync(
                0,
                It.IsAny<string>(),
                It.IsAny<string>(),
                1,
                10001,
                "Tester",
                It.IsAny<string>()))
            .ThrowsAsync(new BusinessException(
                "采纳状态已被其他请求修改，请刷新后重试",
                409,
                ForumQuestionErrorCodes.AcceptanceConflict,
                ForumQuestionErrorCodes.ResolveMessageKey(ForumQuestionErrorCodes.AcceptanceConflict)));

        var controller = CreateController(moderationService.Object, questionService.Object);
        var result = await controller.Accept(new ChangePostAnswerAcceptanceDto
        {
            PostIdentifier = "pst_question",
            AnswerPublicId = "ans_0123456789abcdef0123456789abcdef",
            ExpectedAcceptanceRevision = 1,
            ClientSubmissionId = "accept-operation-2"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(ForumQuestionErrorCodes.AcceptanceConflict, result.Code);
    }

    private static Mock<IContentModerationService> AllowPublishing()
    {
        var moderationService = new Mock<IContentModerationService>(MockBehavior.Strict);
        moderationService
            .Setup(service => service.GetPublishPermissionAsync(10001))
            .ReturnsAsync(new ContentModerationPermissionVo
            {
                VoUserId = 10001,
                VoCanPublish = true
            });
        return moderationService;
    }

    private static QuestionController CreateController(
        IContentModerationService moderationService,
        IForumQuestionService forumQuestionService)
    {
        var currentUserAccessor = new Mock<ICurrentUserAccessor>();
        currentUserAccessor.SetupGet(accessor => accessor.Current).Returns(new CurrentUser
        {
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0
        });
        return new QuestionController(
            Mock.Of<IPostService>(),
            moderationService,
            currentUserAccessor.Object,
            forumQuestionService);
    }
}
