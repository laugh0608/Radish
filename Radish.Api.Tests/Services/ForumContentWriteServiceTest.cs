using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Moq;
using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
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
    public void WriteMethods_Should_Keep_SubmissionLedger_And_BusinessMutation_In_One_Transaction()
    {
        string[] methodNames =
        [
            nameof(ForumContentWriteService.PublishPostAsync),
            nameof(ForumContentWriteService.CreateCommentAsync),
            nameof(ForumContentWriteService.AddAnswerAsync),
            nameof(ForumContentWriteService.UpdatePostAsync),
            nameof(ForumContentWriteService.UpdateCommentAsync)
        ];

        foreach (var methodName in methodNames)
        {
            var method = typeof(ForumContentWriteService).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);

            Assert.NotNull(method);
            Assert.NotNull(method.GetCustomAttribute<UseTranAttribute>());
        }
    }

    [Fact]
    public void PublishRequestDtos_Should_Leave_DomainValidation_To_StablePublishContract()
    {
        var requestProperties = new[]
        {
            typeof(PublishPostDto).GetProperty(nameof(PublishPostDto.ClientSubmissionId)),
            typeof(PublishPostDto).GetProperty(nameof(PublishPostDto.ContentType)),
            typeof(PublishPostDto).GetProperty(nameof(PublishPostDto.CategoryId)),
            typeof(CreatePollDto).GetProperty(nameof(CreatePollDto.Question)),
            typeof(CreatePollDto).GetProperty(nameof(CreatePollDto.Options)),
            typeof(PollOptionDto).GetProperty(nameof(PollOptionDto.OptionText)),
            typeof(CreateLotteryDto).GetProperty(nameof(CreateLotteryDto.PrizeName)),
            typeof(CreateLotteryDto).GetProperty(nameof(CreateLotteryDto.PrizeDescription)),
            typeof(CreateLotteryDto).GetProperty(nameof(CreateLotteryDto.WinnerCount))
        };

        foreach (var property in requestProperties)
        {
            Assert.NotNull(property);
            Assert.Empty(property.GetCustomAttributes<ValidationAttribute>());
        }
    }

    [Fact]
    public async Task PublishPostAsync_Should_Reject_OversizedContent_Before_Creating_SubmissionSnapshot()
    {
        var contentSubmissionService = CreateContentSubmissionService();
        var postService = new Mock<IPostService>(MockBehavior.Strict);
        var service = CreateService(contentSubmissionService, postService);
        var post = CreatePost();
        post.Content = new string('内', 50001);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.PublishPostAsync(
            post,
            poll: null,
            lottery: null,
            isQuestion: false,
            tagNames: ["Radish"],
            allowCreateTag: true,
            clientSubmissionId: "forum-post:abc"));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal(ForumPublishErrorCodes.ContentTooLong, exception.ErrorCode);
        Assert.Equal("error.forum.publish_content_too_long", exception.MessageKey);
        contentSubmissionService.Verify(
            item => item.CreateRequestSnapshot(
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>()),
            Times.Never);
        contentSubmissionService.Verify(
            item => item.BeginAsync(It.IsAny<ContentSubmissionBeginRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishPostAsync_Should_Reject_InvalidNestedPoll_Before_Creating_SubmissionSnapshot()
    {
        var contentSubmissionService = CreateContentSubmissionService();
        var postService = new Mock<IPostService>(MockBehavior.Strict);
        var service = CreateService(contentSubmissionService, postService);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.PublishPostAsync(
            CreatePost(),
            poll: new CreatePollDto
            {
                Question = "投票",
                Options =
                [
                    new PollOptionDto { OptionText = new string('A', 101) },
                    new PollOptionDto { OptionText = "B" }
                ]
            },
            lottery: null,
            isQuestion: false,
            tagNames: ["Radish"],
            allowCreateTag: true,
            clientSubmissionId: "forum-post:abc"));

        Assert.Equal(ForumPublishErrorCodes.PollOptionTooLong, exception.ErrorCode);
        Assert.Equal("error.forum.publish_poll_option_too_long", exception.MessageKey);
        contentSubmissionService.Verify(
            item => item.CreateRequestSnapshot(
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishPostAsync_Should_Reject_NullPollOption_Before_Creating_SubmissionSnapshot()
    {
        var exception = await AssertPublishShapeRejectedBeforeSnapshotAsync(
            poll: new CreatePollDto
            {
                Question = "投票",
                Options =
                [
                    new PollOptionDto { OptionText = "A" },
                    null!,
                    new PollOptionDto { OptionText = "B" }
                ]
            },
            lottery: null);

        Assert.Equal(ForumPublishErrorCodes.PollOptionsRequired, exception.ErrorCode);
        Assert.Equal("error.forum.publish_poll_options_required", exception.MessageKey);
    }

    [Fact]
    public async Task PublishPostAsync_Should_Reject_ExpiredPoll_Before_Creating_SubmissionSnapshot()
    {
        var exception = await AssertPublishShapeRejectedBeforeSnapshotAsync(
            poll: new CreatePollDto
            {
                Question = "投票",
                EndTime = DateTime.UtcNow.AddMinutes(-1),
                Options =
                [
                    new PollOptionDto { OptionText = "A" },
                    new PollOptionDto { OptionText = "B" }
                ]
            },
            lottery: null);

        Assert.Equal(ForumPublishErrorCodes.PollEndTimeInvalid, exception.ErrorCode);
        Assert.Equal("error.forum.publish_poll_end_time_invalid", exception.MessageKey);
    }

    [Fact]
    public async Task PublishPostAsync_Should_Reject_MissingLotteryDrawTime_Before_Creating_SubmissionSnapshot()
    {
        var exception = await AssertPublishShapeRejectedBeforeSnapshotAsync(
            poll: null,
            lottery: new CreateLotteryDto
            {
                PrizeName = "奖品",
                WinnerCount = 1
            });

        Assert.Equal(ForumPublishErrorCodes.LotteryDrawTimeRequired, exception.ErrorCode);
        Assert.Equal("error.forum.publish_lottery_draw_time_required", exception.MessageKey);
    }

    [Fact]
    public async Task PublishPostAsync_Should_Reject_TooSoonLotteryDrawTime_Before_Creating_SubmissionSnapshot()
    {
        var exception = await AssertPublishShapeRejectedBeforeSnapshotAsync(
            poll: null,
            lottery: new CreateLotteryDto
            {
                PrizeName = "奖品",
                DrawTime = DateTime.UtcNow.AddMinutes(30),
                WinnerCount = 1
            });

        Assert.Equal(ForumPublishErrorCodes.LotteryDrawTimeTooSoon, exception.ErrorCode);
        Assert.Equal("error.forum.publish_lottery_draw_time_too_soon", exception.MessageKey);
    }

    [Fact]
    public async Task PublishPostAsync_Should_Preserve_StableBusinessException_When_CreateFails()
    {
        var contentSubmissionService = CreateContentSubmissionService();
        var postService = new Mock<IPostService>(MockBehavior.Strict);
        postService
            .Setup(service => service.PublishPostAsync(
                It.IsAny<Post>(),
                It.IsAny<CreatePollDto?>(),
                It.IsAny<CreateLotteryDto?>(),
                It.IsAny<bool>(),
                It.IsAny<List<string>?>(),
                It.IsAny<bool>()))
            .ThrowsAsync(new BusinessException(
                "投票选项不能重复",
                400,
                ForumPublishErrorCodes.PollOptionsDuplicate,
                "error.forum.publish_poll_options_duplicate"));
        var service = CreateService(contentSubmissionService, postService);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.PublishPostAsync(
            CreatePost(),
            poll: null,
            lottery: null,
            isQuestion: false,
            tagNames: ["Radish"],
            allowCreateTag: true,
            clientSubmissionId: "forum-post:abc"));

        Assert.Equal(ForumPublishErrorCodes.PollOptionsDuplicate, exception.ErrorCode);
    }

    [Fact]
    public async Task PublishPostAsync_Should_Propagate_CompletionFailure_Without_Retry()
    {
        var contentSubmissionService = CreateContentSubmissionService();
        contentSubmissionService
            .Setup(s => s.CompleteSuccessAsync(It.IsAny<ContentSubmissionCompletionRequest>()))
            .ThrowsAsync(new InvalidOperationException("completion unavailable"));

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

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PublishPostAsync(
            CreatePost(),
            poll: null,
            lottery: null,
            isQuestion: false,
            tagNames: ["Radish"],
            allowCreateTag: true,
            clientSubmissionId: "forum-post:abc"));

        Assert.Equal("completion unavailable", exception.Message);
        contentSubmissionService.Verify(
            s => s.CompleteSuccessAsync(It.Is<ContentSubmissionCompletionRequest>(request =>
                request.RecordId == 7001 &&
                request.ResultType == ContentSubmissionResultTypes.Post &&
                request.ResultId == 9001 &&
                request.ResultPublicId == "pst_test")),
            Times.Once);
    }

    [Fact]
    public async Task PublishPostAsync_Should_Rethrow_CreateFailure_Without_Overwriting_It()
    {
        var contentSubmissionService = CreateContentSubmissionService();

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
    }

    [Fact]
    public async Task PublishPostAsync_Should_Pass_Post_Create_Frequency_Window()
    {
        var contentSubmissionService = CreateContentSubmissionService();
        contentSubmissionService
            .Setup(s => s.CompleteSuccessAsync(It.IsAny<ContentSubmissionCompletionRequest>()))
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

        await service.PublishPostAsync(
            CreatePost(),
            poll: null,
            lottery: null,
            isQuestion: false,
            tagNames: ["Radish"],
            allowCreateTag: true,
            clientSubmissionId: "forum-post:abc");

        contentSubmissionService.Verify(
            s => s.BeginAsync(It.Is<ContentSubmissionBeginRequest>(request =>
                request.OperationType == ContentSubmissionOperationTypes.ForumPostCreate &&
                request.DuplicateWindowSeconds == 180 &&
                request.FrequencyWindowSeconds == 30 &&
                request.FrequencyTargetType == null &&
                request.FrequencyTargetId == null)),
            Times.Once);
    }

    [Theory]
    [InlineData(
        ContentSubmissionBeginStatus.InvalidKey,
        400,
        "Forum.PublishSubmissionIdInvalid",
        "error.forum.publish_submission_id_invalid")]
    [InlineData(
        ContentSubmissionBeginStatus.Conflict,
        409,
        "Forum.PublishSubmissionConflict",
        "error.forum.publish_submission_conflict")]
    [InlineData(
        ContentSubmissionBeginStatus.Processing,
        409,
        "Forum.PublishSubmissionProcessing",
        "error.forum.publish_submission_processing")]
    [InlineData(
        ContentSubmissionBeginStatus.FrequencyLimited,
        429,
        "Forum.PublishRateLimited",
        "error.forum.publish_rate_limited")]
    public async Task PublishPostAsync_Should_Map_SubmissionRejection_To_StableBusinessContract(
        ContentSubmissionBeginStatus beginStatus,
        int expectedStatusCode,
        string expectedErrorCode,
        string expectedMessageKey)
    {
        var contentSubmissionService = CreateContentSubmissionService();
        contentSubmissionService
            .Setup(service => service.BeginAsync(It.IsAny<ContentSubmissionBeginRequest>()))
            .ReturnsAsync(new ContentSubmissionBeginResult
            {
                Status = beginStatus,
                RecordId = 7001,
                Message = "submission rejected",
                RetryAfterSeconds = beginStatus == ContentSubmissionBeginStatus.FrequencyLimited ? 7 : null
            });
        var postService = new Mock<IPostService>(MockBehavior.Strict);
        var service = CreateService(contentSubmissionService, postService);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.PublishPostAsync(
            CreatePost(),
            poll: null,
            lottery: null,
            isQuestion: false,
            tagNames: ["Radish"],
            allowCreateTag: true,
            clientSubmissionId: "forum-post:abc"));

        Assert.Equal(expectedStatusCode, exception.StatusCode);
        Assert.Equal(expectedErrorCode, exception.ErrorCode);
        Assert.Equal(expectedMessageKey, exception.MessageKey);
        Assert.Equal("submission rejected", exception.Message);
        if (beginStatus == ContentSubmissionBeginStatus.FrequencyLimited)
        {
            Assert.Equal(new object[] { 7 }, exception.MessageArguments);
        }
        else
        {
            Assert.Empty(exception.MessageArguments);
        }
        postService.Verify(
            item => item.PublishPostAsync(
                It.IsAny<Post>(),
                It.IsAny<CreatePollDto?>(),
                It.IsAny<CreateLotteryDto?>(),
                It.IsAny<bool>(),
                It.IsAny<List<string>?>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateCommentAsync_Should_Pass_Post_Scoped_Frequency_Window()
    {
        var contentSubmissionService = CreateContentSubmissionService();
        contentSubmissionService
            .Setup(s => s.CompleteSuccessAsync(It.IsAny<ContentSubmissionCompletionRequest>()))
            .Returns(Task.CompletedTask);

        var commentService = new Mock<ICommentService>(MockBehavior.Strict);
        commentService
            .Setup(s => s.AddCommentAsync(It.IsAny<Comment>()))
            .ReturnsAsync((3001, new CommentHighlightRecheckResultVo { VoPostId = 2001, VoChanged = false }));

        var service = CreateService(
            contentSubmissionService,
            new Mock<IPostService>(MockBehavior.Strict),
            commentService);

        await service.CreateCommentAsync(CreateComment(), "forum-comment:abc");

        contentSubmissionService.Verify(
            s => s.BeginAsync(It.Is<ContentSubmissionBeginRequest>(request =>
                request.OperationType == ContentSubmissionOperationTypes.ForumCommentCreate &&
                request.TargetType == "Post" &&
                request.TargetId == 2001 &&
                request.DuplicateWindowSeconds == 60 &&
                request.FrequencyWindowSeconds == 10 &&
                request.FrequencyTargetType == "Post" &&
                request.FrequencyTargetId == 2001)),
            Times.Once);
    }

    [Theory]
    [InlineData(
        ContentSubmissionBeginStatus.InvalidKey,
        400,
        ForumPublishErrorCodes.SubmissionIdInvalid,
        "error.forum.publish_submission_id_invalid")]
    [InlineData(
        ContentSubmissionBeginStatus.Conflict,
        409,
        ForumPublishErrorCodes.SubmissionConflict,
        "error.forum.publish_submission_conflict")]
    [InlineData(
        ContentSubmissionBeginStatus.Processing,
        409,
        ForumPublishErrorCodes.SubmissionProcessing,
        "error.forum.publish_submission_processing")]
    [InlineData(
        ContentSubmissionBeginStatus.FrequencyLimited,
        429,
        ForumPublishErrorCodes.RateLimited,
        "error.forum.publish_rate_limited")]
    public async Task CreateCommentAsync_Should_Map_SubmissionRejection_To_StableBusinessContract(
        ContentSubmissionBeginStatus beginStatus,
        int expectedStatusCode,
        string expectedErrorCode,
        string expectedMessageKey)
    {
        var contentSubmissionService = CreateContentSubmissionService();
        contentSubmissionService
            .Setup(service => service.BeginAsync(It.IsAny<ContentSubmissionBeginRequest>()))
            .ReturnsAsync(new ContentSubmissionBeginResult
            {
                Status = beginStatus,
                RecordId = 7001,
                Message = "submission rejected",
                RetryAfterSeconds = beginStatus == ContentSubmissionBeginStatus.FrequencyLimited ? 7 : null
            });
        var commentService = new Mock<ICommentService>(MockBehavior.Strict);
        var service = CreateService(
            contentSubmissionService,
            new Mock<IPostService>(MockBehavior.Strict),
            commentService);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateCommentAsync(CreateComment(), "forum-comment:abc"));

        Assert.Equal(expectedStatusCode, exception.StatusCode);
        Assert.Equal(expectedErrorCode, exception.ErrorCode);
        Assert.Equal(expectedMessageKey, exception.MessageKey);
        if (beginStatus == ContentSubmissionBeginStatus.FrequencyLimited)
        {
            Assert.Equal(new object[] { 7 }, exception.MessageArguments);
        }
        else
        {
            Assert.Empty(exception.MessageArguments);
        }

        commentService.Verify(
            item => item.AddCommentAsync(It.IsAny<Comment>()),
            Times.Never);
    }

    [Fact]
    public async Task AddAnswerAsync_Should_Pass_Post_Scoped_Frequency_Window()
    {
        var contentSubmissionService = CreateContentSubmissionService();
        contentSubmissionService
            .Setup(s => s.CompleteSuccessAsync(It.IsAny<ContentSubmissionCompletionRequest>()))
            .Returns(Task.CompletedTask);

        var postService = new Mock<IPostService>(MockBehavior.Strict);
        postService
            .Setup(s => s.AddAnswerAsync(2001, "排查数据库连接", 42, "Owner", 9))
            .ReturnsAsync(new PostQuestionVo
            {
                VoPostId = 2001,
                VoAnswerCount = 1
            });

        var service = CreateService(contentSubmissionService, postService);

        await service.AddAnswerAsync(
            postId: 2001,
            content: "排查数据库连接",
            authorId: 42,
            authorName: "Owner",
            tenantId: 9,
            clientSubmissionId: "forum-answer:abc");

        contentSubmissionService.Verify(
            s => s.BeginAsync(It.Is<ContentSubmissionBeginRequest>(request =>
                request.OperationType == ContentSubmissionOperationTypes.ForumAnswerCreate &&
                request.TargetType == "Post" &&
                request.TargetId == 2001 &&
                request.DuplicateWindowSeconds == 120 &&
                request.FrequencyWindowSeconds == 30 &&
                request.FrequencyTargetType == "Post" &&
                request.FrequencyTargetId == 2001)),
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
    public async Task UpdatePostAsync_Should_Preserve_CompletionConsistencyFailure()
    {
        var contentSubmissionService = CreateContentSubmissionService();
        contentSubmissionService
            .Setup(service => service.CompleteSuccessAsync(It.IsAny<ContentSubmissionCompletionRequest>()))
            .ThrowsAsync(new ContentSubmissionConsistencyException("completion unavailable"));
        var postService = new Mock<IPostService>(MockBehavior.Strict);
        postService
            .Setup(service => service.GetPostDetailAsync(2001, 42, "default"))
            .ReturnsAsync(new PostVo
            {
                VoId = 2001,
                VoPublicId = "pst_2001",
                VoTitle = "原帖子标题",
                VoContent = "原帖子内容",
                VoCategoryId = 101,
                VoTags = "Radish"
            });
        var service = CreateService(contentSubmissionService, postService);

        var exception = await Assert.ThrowsAsync<ContentSubmissionConsistencyException>(() =>
            service.UpdatePostAsync(
                tenantId: 9,
                postId: 2001,
                title: "原帖子标题",
                content: "原帖子内容",
                categoryId: 101,
                tagNames: ["Radish"],
                allowCreateTag: false,
                operatorId: 42,
                operatorName: "Owner",
                isAdmin: false,
                clientSubmissionId: "forum-post-edit:failure"));

        Assert.Equal("completion unavailable", exception.Message);
    }

    [Fact]
    public async Task UpdateCommentAsync_Should_Preserve_CompletionConsistencyFailure()
    {
        var contentSubmissionService = CreateContentSubmissionService();
        contentSubmissionService
            .Setup(service => service.CompleteSuccessAsync(It.IsAny<ContentSubmissionCompletionRequest>()))
            .ThrowsAsync(new ContentSubmissionConsistencyException("completion unavailable"));
        var commentService = new Mock<ICommentService>(MockBehavior.Strict);
        commentService
            .Setup(service => service.QueryFirstAsync(It.IsAny<Expression<Func<Comment, bool>>?>()))
            .ReturnsAsync(new CommentVo
            {
                VoId = 3001,
                VoPostId = 2001,
                VoContent = "原评论内容"
            });
        var service = CreateService(
            contentSubmissionService,
            new Mock<IPostService>(MockBehavior.Strict),
            commentService);

        var exception = await Assert.ThrowsAsync<ContentSubmissionConsistencyException>(() =>
            service.UpdateCommentAsync(
                tenantId: 9,
                commentId: 3001,
                content: "原评论内容",
                operatorId: 42,
                operatorName: "Owner",
                isAdmin: false,
                clientSubmissionId: "forum-comment-edit:failure"));

        Assert.Equal("completion unavailable", exception.Message);
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

    private static async Task<BusinessException> AssertPublishShapeRejectedBeforeSnapshotAsync(
        CreatePollDto? poll,
        CreateLotteryDto? lottery)
    {
        var contentSubmissionService = CreateContentSubmissionService();
        var postService = new Mock<IPostService>(MockBehavior.Strict);
        var service = CreateService(contentSubmissionService, postService);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.PublishPostAsync(
            CreatePost(),
            poll,
            lottery,
            isQuestion: false,
            tagNames: ["Radish"],
            allowCreateTag: true,
            clientSubmissionId: "forum-post:validation"));

        Assert.Equal(400, exception.StatusCode);
        contentSubmissionService.Verify(
            item => item.CreateRequestSnapshot(
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>()),
            Times.Never);
        contentSubmissionService.Verify(
            item => item.BeginAsync(It.IsAny<ContentSubmissionBeginRequest>()),
            Times.Never);

        return exception;
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

    private static Comment CreateComment()
    {
        return new Comment(new CommentInitializationOptions("测试评论")
        {
            PostId = 2001,
            AuthorId = 42,
            AuthorName = "Owner",
            TenantId = 9
        });
    }
}
