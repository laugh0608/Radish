using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Moq;
using Radish.Api.Controllers;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
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
            .ThrowsAsync(new InvalidOperationException("当前帖子不是问答帖"));

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
            .ThrowsAsync(new InvalidOperationException("只有提问者可以采纳答案"));

        var controller = CreateController(postServiceMock.Object, moderationServiceMock.Object);

        var result = await controller.Accept(new AcceptAnswerDto
        {
            PostId = 9527,
            AnswerId = 3001
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Contains("只有提问者可以采纳答案", result.MessageInfo);
    }

    private static QuestionController CreateController(
        IPostService postService,
        IContentModerationService moderationService)
    {
        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(accessor => accessor.Current).Returns(new CurrentUser
        {
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0
        });

        return new QuestionController(
            postService,
            moderationService,
            currentUserAccessorMock.Object);
    }
}
