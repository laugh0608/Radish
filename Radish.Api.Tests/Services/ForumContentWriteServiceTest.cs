using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
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
        Mock<IPostService> postService)
    {
        return new ForumContentWriteService(
            contentSubmissionService.Object,
            postService.Object,
            new Mock<ICommentService>(MockBehavior.Strict).Object);
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
