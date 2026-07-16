using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;

namespace Radish.Service;

/// <summary>论坛内容创建写入可靠性服务。</summary>
public class ForumContentWriteService : IForumContentWriteService
{
    private const int PostCreateDuplicateWindowSeconds = 180;
    private const int CommentCreateDuplicateWindowSeconds = 60;
    private const int AnswerCreateDuplicateWindowSeconds = 120;
    private const int EditDuplicateWindowSeconds = 60;
    private const int PostCreateFrequencyWindowSeconds = 30;
    private const int CommentCreateFrequencyWindowSeconds = 10;
    private const int AnswerCreateFrequencyWindowSeconds = 30;
    private const int MaxPostTitleLength = 200;
    private const int MaxPostContentLength = 50000;
    private const int MaxContentTypeLength = 20;
    private const int MaxClientSubmissionIdLength = 80;
    private const int MinPostTagCount = 1;
    private const int MaxPostTagCount = 5;
    private const int MaxPollQuestionLength = 200;
    private const int MinPollOptionCount = 2;
    private const int MaxPollOptionCount = 6;
    private const int MaxPollOptionLength = 100;
    private const int MaxLotteryPrizeNameLength = 100;
    private const int MaxLotteryPrizeDescriptionLength = 500;
    private const int MinLotteryWinnerCount = 1;
    private const int MaxLotteryWinnerCount = 20;
    private static readonly TimeSpan MinLotteryLeadTime = TimeSpan.FromHours(1);

    private const string CategoryTargetType = "Category";
    private const string PostTargetType = "Post";
    private const string CommentTargetType = "Comment";

    private readonly IContentSubmissionService _contentSubmissionService;
    private readonly IPostService _postService;
    private readonly ICommentService _commentService;

    public ForumContentWriteService(
        IContentSubmissionService contentSubmissionService,
        IPostService postService,
        ICommentService commentService)
    {
        _contentSubmissionService = contentSubmissionService;
        _postService = postService;
        _commentService = commentService;
    }

    [UseTran]
    public async Task<ContentWriteResult<long>> PublishPostAsync(
        Post post,
        CreatePollDto? poll,
        CreateLotteryDto? lottery,
        bool isQuestion,
        List<string>? tagNames,
        bool allowCreateTag,
        string? clientSubmissionId)
    {
        ValidatePostPublishRequestShape(
            post,
            poll,
            lottery,
            isQuestion,
            tagNames,
            clientSubmissionId);

        var snapshot = _contentSubmissionService.CreateRequestSnapshot(
            BuildPostCreateRequestValues(post, poll, lottery, isQuestion, tagNames),
            BuildPostCreateFingerprintValues(post, poll, lottery, isQuestion, tagNames));
        var beginResult = await _contentSubmissionService.BeginAsync(new ContentSubmissionBeginRequest
        {
            TenantId = post.TenantId,
            UserId = post.AuthorId,
            OperationType = ContentSubmissionOperationTypes.ForumPostCreate,
            ClientSubmissionId = clientSubmissionId,
            TargetType = CategoryTargetType,
            TargetId = post.CategoryId,
            RequestDigest = snapshot.RequestDigest,
            RequestSummary = snapshot.RequestSummary,
            ContentFingerprint = snapshot.ContentFingerprint,
            DuplicateWindowSeconds = PostCreateDuplicateWindowSeconds,
            FrequencyWindowSeconds = PostCreateFrequencyWindowSeconds
        });

        if (TryResolveReplay(beginResult, ContentSubmissionResultTypes.Post, out var postId))
        {
            return beginResult.Status == ContentSubmissionBeginStatus.DuplicateContent
                ? ContentWriteResult<long>.DuplicateContentResult(postId, beginResult.Message)
                : ContentWriteResult<long>.ReplayedResult(postId, beginResult.Message);
        }

        EnsurePostPublishStarted(beginResult);

        var createdPostId = await _postService.PublishPostAsync(
            post,
            poll,
            lottery,
            isQuestion,
            tagNames,
            allowCreateTag);

        await CompleteSuccessAsync(
            beginResult,
            ContentSubmissionResultTypes.Post,
            createdPostId,
            post.PublicId);

        return ContentWriteResult<long>.CreatedResult(createdPostId);
    }

    [UseTran]
    public async Task<ContentWriteResult<CommentCreateResult>> CreateCommentAsync(
        Comment comment,
        string? clientSubmissionId)
    {
        var snapshot = _contentSubmissionService.CreateRequestSnapshot(
            BuildCommentCreateRequestValues(comment),
            BuildCommentCreateFingerprintValues(comment));
        var beginResult = await _contentSubmissionService.BeginAsync(new ContentSubmissionBeginRequest
        {
            TenantId = comment.TenantId,
            UserId = comment.AuthorId,
            OperationType = ContentSubmissionOperationTypes.ForumCommentCreate,
            ClientSubmissionId = clientSubmissionId,
            TargetType = PostTargetType,
            TargetId = comment.PostId,
            RequestDigest = snapshot.RequestDigest,
            RequestSummary = snapshot.RequestSummary,
            ContentFingerprint = snapshot.ContentFingerprint,
            DuplicateWindowSeconds = CommentCreateDuplicateWindowSeconds,
            FrequencyWindowSeconds = CommentCreateFrequencyWindowSeconds,
            FrequencyTargetType = PostTargetType,
            FrequencyTargetId = comment.PostId
        });

        if (TryResolveReplay(beginResult, ContentSubmissionResultTypes.Comment, out var commentId))
        {
            var replayResult = new CommentCreateResult
            {
                CommentId = commentId,
                HighlightRecheckResult = new CommentHighlightRecheckResultVo
                {
                    VoPostId = comment.PostId,
                    VoChanged = false
                }
            };

            return beginResult.Status == ContentSubmissionBeginStatus.DuplicateContent
                ? ContentWriteResult<CommentCreateResult>.DuplicateContentResult(replayResult, beginResult.Message)
                : ContentWriteResult<CommentCreateResult>.ReplayedResult(replayResult, beginResult.Message);
        }

        EnsureStarted(beginResult);

        var (createdCommentId, createdHighlightRecheckResult) = await _commentService.AddCommentAsync(comment);

        await CompleteSuccessAsync(
            beginResult,
            ContentSubmissionResultTypes.Comment,
            createdCommentId,
            null);

        return ContentWriteResult<CommentCreateResult>.CreatedResult(new CommentCreateResult
        {
            CommentId = createdCommentId,
            HighlightRecheckResult = createdHighlightRecheckResult
        });
    }

    [UseTran]
    public async Task<ContentWriteResult<PostQuestionVo>> AddAnswerAsync(
        long postId,
        string content,
        long authorId,
        string authorName,
        long tenantId,
        string? clientSubmissionId)
    {
        var snapshot = _contentSubmissionService.CreateRequestSnapshot(
            BuildAnswerCreateRequestValues(postId, content),
            BuildAnswerCreateFingerprintValues(postId, content));
        var beginResult = await _contentSubmissionService.BeginAsync(new ContentSubmissionBeginRequest
        {
            TenantId = tenantId,
            UserId = authorId,
            OperationType = ContentSubmissionOperationTypes.ForumAnswerCreate,
            ClientSubmissionId = clientSubmissionId,
            TargetType = PostTargetType,
            TargetId = postId,
            RequestDigest = snapshot.RequestDigest,
            RequestSummary = snapshot.RequestSummary,
            ContentFingerprint = snapshot.ContentFingerprint,
            DuplicateWindowSeconds = AnswerCreateDuplicateWindowSeconds,
            FrequencyWindowSeconds = AnswerCreateFrequencyWindowSeconds,
            FrequencyTargetType = PostTargetType,
            FrequencyTargetId = postId
        });

        if (TryResolveReplay(beginResult, ContentSubmissionResultTypes.PostQuestion, out var replayPostId))
        {
            var question = await ResolveQuestionAsync(replayPostId, authorId);
            return beginResult.Status == ContentSubmissionBeginStatus.DuplicateContent
                ? ContentWriteResult<PostQuestionVo>.DuplicateContentResult(question, beginResult.Message)
                : ContentWriteResult<PostQuestionVo>.ReplayedResult(question, beginResult.Message);
        }

        EnsureStarted(beginResult);

        var createdQuestion = await _postService.AddAnswerAsync(postId, content, authorId, authorName, tenantId);
        await CompleteSuccessAsync(
            beginResult,
            ContentSubmissionResultTypes.PostQuestion,
            postId,
            null);

        return ContentWriteResult<PostQuestionVo>.CreatedResult(createdQuestion);
    }

    [UseTran]
    public async Task<ContentWriteResult<PostEditResult>> UpdatePostAsync(
        long tenantId,
        long postId,
        string title,
        string content,
        long? categoryId,
        List<string>? tagNames,
        bool allowCreateTag,
        long operatorId,
        string operatorName,
        bool isAdmin,
        string? clientSubmissionId)
    {
        var currentPost = await _postService.GetPostDetailAsync(postId, operatorId);
        var effectiveCategoryId = categoryId ?? currentPost?.VoCategoryId;
        var snapshot = _contentSubmissionService.CreateRequestSnapshot(
            BuildPostEditRequestValues(postId, title, content, effectiveCategoryId, tagNames),
            BuildPostEditFingerprintValues(postId, title, content, effectiveCategoryId, tagNames));
        var beginResult = await _contentSubmissionService.BeginAsync(new ContentSubmissionBeginRequest
        {
            TenantId = tenantId,
            UserId = operatorId,
            OperationType = ContentSubmissionOperationTypes.ForumPostEdit,
            ClientSubmissionId = clientSubmissionId,
            TargetType = PostTargetType,
            TargetId = postId,
            RequestDigest = snapshot.RequestDigest,
            RequestSummary = snapshot.RequestSummary,
            ContentFingerprint = snapshot.ContentFingerprint,
            DuplicateWindowSeconds = EditDuplicateWindowSeconds
        });

        if (TryResolveReplay(beginResult, ContentSubmissionResultTypes.Post, out var replayPostId))
        {
            return beginResult.Status == ContentSubmissionBeginStatus.DuplicateContent
                ? ContentWriteResult<PostEditResult>.DuplicateContentResult(
                    new PostEditResult { PostId = replayPostId },
                    beginResult.Message)
                : ContentWriteResult<PostEditResult>.ReplayedResult(
                    new PostEditResult { PostId = replayPostId },
                    beginResult.Message);
        }

        EnsureStarted(beginResult);

        if (IsPostEditNoChange(currentPost, title, content, effectiveCategoryId, tagNames))
        {
            await CompleteSuccessAsync(beginResult, ContentSubmissionResultTypes.Post, postId, currentPost?.VoPublicId);
            return ContentWriteResult<PostEditResult>.NoChangeResult(
                new PostEditResult { PostId = postId },
                "内容没有变化，无需保存");
        }

        await _postService.UpdatePostAsync(
            postId,
            title,
            content,
            categoryId,
            tagNames,
            allowCreateTag,
            operatorId,
            operatorName,
            isAdmin);

        await CompleteSuccessAsync(beginResult, ContentSubmissionResultTypes.Post, postId, currentPost?.VoPublicId);
        return ContentWriteResult<PostEditResult>.CreatedResult(new PostEditResult { PostId = postId });
    }

    [UseTran]
    public async Task<ContentWriteResult<CommentEditResult>> UpdateCommentAsync(
        long tenantId,
        long commentId,
        string content,
        long operatorId,
        string operatorName,
        bool isAdmin,
        string? clientSubmissionId)
    {
        var currentComment = await _commentService.QueryFirstAsync(comment => comment.Id == commentId && !comment.IsDeleted);
        var snapshot = _contentSubmissionService.CreateRequestSnapshot(
            BuildCommentEditRequestValues(commentId, content),
            BuildCommentEditFingerprintValues(commentId, content));
        var beginResult = await _contentSubmissionService.BeginAsync(new ContentSubmissionBeginRequest
        {
            TenantId = tenantId,
            UserId = operatorId,
            OperationType = ContentSubmissionOperationTypes.ForumCommentEdit,
            ClientSubmissionId = clientSubmissionId,
            TargetType = CommentTargetType,
            TargetId = commentId,
            RequestDigest = snapshot.RequestDigest,
            RequestSummary = snapshot.RequestSummary,
            ContentFingerprint = snapshot.ContentFingerprint,
            DuplicateWindowSeconds = EditDuplicateWindowSeconds
        });

        if (TryResolveReplay(beginResult, ContentSubmissionResultTypes.Comment, out var replayCommentId))
        {
            return beginResult.Status == ContentSubmissionBeginStatus.DuplicateContent
                ? ContentWriteResult<CommentEditResult>.DuplicateContentResult(
                    BuildCommentEditResult(replayCommentId, currentComment),
                    beginResult.Message)
                : ContentWriteResult<CommentEditResult>.ReplayedResult(
                    BuildCommentEditResult(replayCommentId, currentComment),
                    beginResult.Message);
        }

        EnsureStarted(beginResult);

        if (IsCommentEditNoChange(currentComment, content))
        {
            await CompleteSuccessAsync(beginResult, ContentSubmissionResultTypes.Comment, commentId, null);
            return ContentWriteResult<CommentEditResult>.NoChangeResult(
                BuildCommentEditResult(commentId, currentComment),
                "内容没有变化，无需保存");
        }

        var (success, message) = await _commentService.UpdateCommentAsync(
            commentId,
            content,
            operatorId,
            operatorName,
            isAdmin);
        if (!success)
        {
            throw new InvalidOperationException(message);
        }

        await CompleteSuccessAsync(beginResult, ContentSubmissionResultTypes.Comment, commentId, null);
        return ContentWriteResult<CommentEditResult>.CreatedResult(BuildCommentEditResult(commentId, currentComment), message);
    }

    private async Task<PostQuestionVo> ResolveQuestionAsync(long postId, long viewerUserId)
    {
        var post = await _postService.GetPostDetailAsync(postId, viewerUserId);
        return post?.VoQuestion ?? throw new InvalidOperationException("问答详情不存在");
    }

    private static bool TryResolveReplay(
        ContentSubmissionBeginResult beginResult,
        string expectedResultType,
        out long resultId)
    {
        if ((beginResult.Status == ContentSubmissionBeginStatus.Succeeded ||
             beginResult.Status == ContentSubmissionBeginStatus.DuplicateContent) &&
            string.Equals(beginResult.ResultType, expectedResultType, StringComparison.Ordinal) &&
            beginResult.ResultId.HasValue)
        {
            resultId = beginResult.ResultId.Value;
            return true;
        }

        resultId = 0;
        return false;
    }

    private static void EnsurePostPublishStarted(ContentSubmissionBeginResult beginResult)
    {
        if (beginResult.Status == ContentSubmissionBeginStatus.Started && beginResult.RecordId.HasValue)
        {
            return;
        }

        throw beginResult.Status switch
        {
            ContentSubmissionBeginStatus.InvalidKey => CreateSubmissionException(
                beginResult,
                HttpStatusCodeEnum.BadRequest,
                ForumPublishErrorCodes.SubmissionIdInvalid,
                "提交标识格式无效，请刷新后重新提交"),
            ContentSubmissionBeginStatus.Conflict => CreateSubmissionException(
                beginResult,
                HttpStatusCodeEnum.Conflict,
                ForumPublishErrorCodes.SubmissionConflict,
                "提交标识已被其他内容使用，请刷新后重新提交"),
            ContentSubmissionBeginStatus.Processing => CreateSubmissionException(
                beginResult,
                HttpStatusCodeEnum.Conflict,
                ForumPublishErrorCodes.SubmissionProcessing,
                "内容正在提交，请稍后确认"),
            ContentSubmissionBeginStatus.FrequencyLimited => CreateSubmissionException(
                beginResult,
                HttpStatusCodeEnum.TooManyRequests,
                ForumPublishErrorCodes.RateLimited,
                "发布过于频繁，请稍后重试"),
            ContentSubmissionBeginStatus.Succeeded or ContentSubmissionBeginStatus.DuplicateContent =>
                CreateSubmissionException(
                    beginResult,
                    HttpStatusCodeEnum.Conflict,
                    ForumPublishErrorCodes.SubmissionConflict,
                    "此前提交结果暂不可用，请刷新后重试"),
            _ => new InvalidOperationException("内容提交服务返回了未知状态")
        };
    }

    private static void ValidatePostPublishRequestShape(
        Post post,
        CreatePollDto? poll,
        CreateLotteryDto? lottery,
        bool isQuestion,
        List<string>? tagNames,
        string? clientSubmissionId)
    {
        if (string.IsNullOrWhiteSpace(post.Title))
        {
            throw CreatePublishValidationException(
                "帖子标题不能为空",
                ForumPublishErrorCodes.TitleRequired);
        }

        if (post.Title.Length > MaxPostTitleLength)
        {
            throw CreatePublishValidationException(
                $"帖子标题不能超过 {MaxPostTitleLength} 个字符",
                ForumPublishErrorCodes.TitleTooLong);
        }

        if (string.IsNullOrWhiteSpace(post.Content))
        {
            throw CreatePublishValidationException(
                "帖子内容不能为空",
                ForumPublishErrorCodes.ContentRequired);
        }

        if (post.Content.Length > MaxPostContentLength)
        {
            throw CreatePublishValidationException(
                $"帖子内容不能超过 {MaxPostContentLength} 个字符",
                ForumPublishErrorCodes.ContentTooLong);
        }

        if (post.CategoryId <= 0)
        {
            throw CreatePublishValidationException(
                "请选择帖子分类",
                ForumPublishErrorCodes.CategoryRequired);
        }

        if (post.ContentType?.Length > MaxContentTypeLength)
        {
            throw CreatePublishValidationException(
                "帖子内容类型无效",
                ForumPublishErrorCodes.ContentTypeInvalid);
        }

        if (clientSubmissionId?.Length > MaxClientSubmissionIdLength)
        {
            throw CreatePublishValidationException(
                "提交标识格式无效，请刷新后重新提交",
                ForumPublishErrorCodes.SubmissionIdInvalid);
        }

        var featureCount = (poll != null ? 1 : 0) + (lottery != null ? 1 : 0) + (isQuestion ? 1 : 0);
        if (featureCount > 1)
        {
            throw CreatePublishValidationException(
                "问答帖、投票和抽奖暂时互斥",
                ForumPublishErrorCodes.FeatureCombinationInvalid);
        }

        var normalizedTags = tagNames?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
        if (normalizedTags.Count is < MinPostTagCount or > MaxPostTagCount)
        {
            throw CreatePublishValidationException(
                $"发布帖子时标签数量必须在 {MinPostTagCount} 到 {MaxPostTagCount} 个之间",
                ForumPublishErrorCodes.TagCountInvalid);
        }

        if (poll != null)
        {
            ValidatePollRequestShape(poll);
        }

        if (lottery != null)
        {
            ValidateLotteryRequestShape(lottery);
        }
    }

    private static void ValidatePollRequestShape(CreatePollDto poll)
    {
        var question = poll.Question?.Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            throw CreatePublishValidationException(
                "投票问题不能为空",
                ForumPublishErrorCodes.PollQuestionRequired);
        }

        if (question.Length > MaxPollQuestionLength)
        {
            throw CreatePublishValidationException(
                $"投票问题不能超过 {MaxPollQuestionLength} 个字符",
                ForumPublishErrorCodes.PollQuestionTooLong);
        }

        if (poll.Options == null)
        {
            throw CreatePublishValidationException(
                "投票选项不能为空",
                ForumPublishErrorCodes.PollOptionsRequired);
        }

        if (poll.EndTime.HasValue && poll.EndTime.Value <= DateTime.UtcNow)
        {
            throw CreatePublishValidationException(
                "投票截止时间必须晚于当前时间",
                ForumPublishErrorCodes.PollEndTimeInvalid);
        }

        var providedOptions = poll.Options.OfType<PollOptionDto>().ToList();
        if (providedOptions.Count != poll.Options.Count)
        {
            throw CreatePublishValidationException(
                "投票选项不能为空",
                ForumPublishErrorCodes.PollOptionsRequired);
        }

        var normalizedOptions = providedOptions
            .Where(option => !string.IsNullOrWhiteSpace(option.OptionText))
            .Select(option => option.OptionText.Trim())
            .ToList();
        if (normalizedOptions.Count is < MinPollOptionCount or > MaxPollOptionCount)
        {
            throw CreatePublishValidationException(
                $"投票选项数量必须在 {MinPollOptionCount} 到 {MaxPollOptionCount} 个之间",
                ForumPublishErrorCodes.PollOptionCountInvalid);
        }

        if (normalizedOptions.Any(option => option.Length > MaxPollOptionLength))
        {
            throw CreatePublishValidationException(
                $"单个投票选项不能超过 {MaxPollOptionLength} 个字符",
                ForumPublishErrorCodes.PollOptionTooLong);
        }

        if (normalizedOptions.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedOptions.Count)
        {
            throw CreatePublishValidationException(
                "投票选项不能重复",
                ForumPublishErrorCodes.PollOptionsDuplicate);
        }
    }

    private static void ValidateLotteryRequestShape(CreateLotteryDto lottery)
    {
        var prizeName = lottery.PrizeName?.Trim();
        if (string.IsNullOrWhiteSpace(prizeName))
        {
            throw CreatePublishValidationException(
                "奖品名称不能为空",
                ForumPublishErrorCodes.LotteryPrizeNameRequired);
        }

        if (prizeName.Length > MaxLotteryPrizeNameLength)
        {
            throw CreatePublishValidationException(
                $"奖品名称不能超过 {MaxLotteryPrizeNameLength} 个字符",
                ForumPublishErrorCodes.LotteryPrizeNameTooLong);
        }

        if (lottery.PrizeDescription?.Trim().Length > MaxLotteryPrizeDescriptionLength)
        {
            throw CreatePublishValidationException(
                $"奖品说明不能超过 {MaxLotteryPrizeDescriptionLength} 个字符",
                ForumPublishErrorCodes.LotteryPrizeDescriptionTooLong);
        }

        if (lottery.WinnerCount is < MinLotteryWinnerCount or > MaxLotteryWinnerCount)
        {
            throw CreatePublishValidationException(
                $"中奖人数必须在 {MinLotteryWinnerCount} 到 {MaxLotteryWinnerCount} 之间",
                ForumPublishErrorCodes.LotteryWinnerCountInvalid);
        }

        if (!lottery.DrawTime.HasValue)
        {
            throw CreatePublishValidationException(
                "抽奖截止时间不能为空",
                ForumPublishErrorCodes.LotteryDrawTimeRequired);
        }

        if (lottery.DrawTime.Value < DateTime.UtcNow.Add(MinLotteryLeadTime))
        {
            throw CreatePublishValidationException(
                "抽奖截止时间必须至少晚于发帖时间 1 小时",
                ForumPublishErrorCodes.LotteryDrawTimeTooSoon);
        }
    }

    private static BusinessException CreatePublishValidationException(string message, string errorCode)
    {
        return new BusinessException(
            message,
            (int)HttpStatusCodeEnum.BadRequest,
            errorCode,
            ForumPublishErrorCodes.ResolveMessageKey(errorCode));
    }

    private static BusinessException CreateSubmissionException(
        ContentSubmissionBeginResult beginResult,
        HttpStatusCodeEnum statusCode,
        string errorCode,
        string fallbackMessage)
    {
        object[] messageArguments = errorCode == ForumPublishErrorCodes.RateLimited
            ? [beginResult.RetryAfterSeconds ?? throw new InvalidOperationException(
                "内容提交频率限制结果缺少 RetryAfterSeconds")]
            : [];

        return new BusinessException(
            beginResult.Message ?? fallbackMessage,
            (int)statusCode,
            errorCode,
            ForumPublishErrorCodes.ResolveMessageKey(errorCode),
            messageArguments);
    }

    private static void EnsureStarted(ContentSubmissionBeginResult beginResult)
    {
        if (beginResult.Status == ContentSubmissionBeginStatus.Started && beginResult.RecordId.HasValue)
        {
            return;
        }

        throw beginResult.Status switch
        {
            ContentSubmissionBeginStatus.InvalidKey => CreateSubmissionException(
                beginResult,
                HttpStatusCodeEnum.BadRequest,
                ForumPublishErrorCodes.SubmissionIdInvalid,
                "提交标识格式无效，请刷新后重新提交"),
            ContentSubmissionBeginStatus.Conflict => CreateSubmissionException(
                beginResult,
                HttpStatusCodeEnum.Conflict,
                ForumPublishErrorCodes.SubmissionConflict,
                "提交标识已被其他内容使用，请刷新后重新提交"),
            ContentSubmissionBeginStatus.Processing => CreateSubmissionException(
                beginResult,
                HttpStatusCodeEnum.Conflict,
                ForumPublishErrorCodes.SubmissionProcessing,
                "内容正在提交，请稍后确认"),
            ContentSubmissionBeginStatus.FrequencyLimited => CreateSubmissionException(
                beginResult,
                HttpStatusCodeEnum.TooManyRequests,
                ForumPublishErrorCodes.RateLimited,
                "操作过于频繁，请稍后重试"),
            ContentSubmissionBeginStatus.Succeeded or ContentSubmissionBeginStatus.DuplicateContent =>
                CreateSubmissionException(
                    beginResult,
                    HttpStatusCodeEnum.Conflict,
                    ForumPublishErrorCodes.SubmissionConflict,
                    "此前提交结果暂不可用，请刷新后重试"),
            _ => new ContentSubmissionConsistencyException("内容提交服务返回了未知状态")
        };
    }

    private async Task CompleteSuccessAsync(
        ContentSubmissionBeginResult beginResult,
        string resultType,
        long resultId,
        string? resultPublicId)
    {
        if (!beginResult.RecordId.HasValue)
        {
            throw new ContentSubmissionConsistencyException("内容提交记录缺少 RecordId，无法完成成功状态");
        }

        await _contentSubmissionService.CompleteSuccessAsync(new ContentSubmissionCompletionRequest
        {
            RecordId = beginResult.RecordId.Value,
            ResultType = resultType,
            ResultId = resultId,
            ResultPublicId = resultPublicId
        });
    }

    private static IReadOnlyDictionary<string, object?> BuildPostCreateRequestValues(
        Post post,
        CreatePollDto? poll,
        CreateLotteryDto? lottery,
        bool isQuestion,
        List<string>? tagNames)
    {
        return new Dictionary<string, object?>
        {
            ["title"] = post.Title,
            ["content"] = post.Content,
            ["contentType"] = post.ContentType,
            ["categoryId"] = post.CategoryId,
            ["isQuestion"] = isQuestion,
            ["tagNames"] = NormalizeTagNames(tagNames),
            ["pollQuestion"] = poll?.Question,
            ["pollEndTime"] = poll?.EndTime,
            ["pollOptions"] = poll?.Options
                .OfType<PollOptionDto>()
                .OrderBy(option => option.SortOrder ?? int.MaxValue)
                .ThenBy(option => option.OptionText, StringComparer.Ordinal)
                .Select(option => new { option.OptionText, option.SortOrder })
                .ToList(),
            ["lotteryPrizeName"] = lottery?.PrizeName,
            ["lotteryPrizeDescription"] = lottery?.PrizeDescription,
            ["lotteryDrawTime"] = lottery?.DrawTime,
            ["lotteryWinnerCount"] = lottery?.WinnerCount
        };
    }

    private static IReadOnlyDictionary<string, object?> BuildPostCreateFingerprintValues(
        Post post,
        CreatePollDto? poll,
        CreateLotteryDto? lottery,
        bool isQuestion,
        List<string>? tagNames)
    {
        return BuildPostCreateRequestValues(post, poll, lottery, isQuestion, tagNames);
    }

    private static IReadOnlyDictionary<string, object?> BuildCommentCreateRequestValues(Comment comment)
    {
        return new Dictionary<string, object?>
        {
            ["postId"] = comment.PostId,
            ["parentId"] = comment.ParentId,
            ["replyToCommentId"] = comment.ReplyToCommentId,
            ["replyToCommentSnapshot"] = comment.ReplyToCommentSnapshot,
            ["replyToUserId"] = comment.ReplyToUserId,
            ["replyToUserName"] = comment.ReplyToUserName,
            ["content"] = comment.Content
        };
    }

    private static IReadOnlyDictionary<string, object?> BuildCommentCreateFingerprintValues(Comment comment)
    {
        return BuildCommentCreateRequestValues(comment);
    }

    private static IReadOnlyDictionary<string, object?> BuildAnswerCreateRequestValues(long postId, string content)
    {
        return new Dictionary<string, object?>
        {
            ["postId"] = postId,
            ["content"] = content
        };
    }

    private static IReadOnlyDictionary<string, object?> BuildAnswerCreateFingerprintValues(long postId, string content)
    {
        return BuildAnswerCreateRequestValues(postId, content);
    }

    private static IReadOnlyDictionary<string, object?> BuildPostEditRequestValues(
        long postId,
        string title,
        string content,
        long? categoryId,
        List<string>? tagNames)
    {
        return new Dictionary<string, object?>
        {
            ["postId"] = postId,
            ["title"] = title,
            ["content"] = content,
            ["categoryId"] = categoryId,
            ["tagNames"] = NormalizeTagNames(tagNames)
        };
    }

    private static IReadOnlyDictionary<string, object?> BuildPostEditFingerprintValues(
        long postId,
        string title,
        string content,
        long? categoryId,
        List<string>? tagNames)
    {
        return BuildPostEditRequestValues(postId, title, content, categoryId, tagNames);
    }

    private static IReadOnlyDictionary<string, object?> BuildCommentEditRequestValues(long commentId, string content)
    {
        return new Dictionary<string, object?>
        {
            ["commentId"] = commentId,
            ["content"] = content
        };
    }

    private static IReadOnlyDictionary<string, object?> BuildCommentEditFingerprintValues(long commentId, string content)
    {
        return BuildCommentEditRequestValues(commentId, content);
    }

    private static bool IsPostEditNoChange(
        PostVo? currentPost,
        string title,
        string content,
        long? categoryId,
        List<string>? tagNames)
    {
        if (currentPost == null)
        {
            return false;
        }

        return string.Equals(currentPost.VoTitle.Trim(), title.Trim(), StringComparison.Ordinal) &&
            string.Equals(currentPost.VoContent.Trim(), content.Trim(), StringComparison.Ordinal) &&
            currentPost.VoCategoryId == categoryId &&
            NormalizeTagNames(ParseTagNames(currentPost.VoTags))
                .SequenceEqual(NormalizeTagNames(tagNames), StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsCommentEditNoChange(CommentVo? currentComment, string content)
    {
        return currentComment != null &&
            string.Equals(currentComment.VoContent?.Trim(), content.Trim(), StringComparison.Ordinal);
    }

    private static CommentEditResult BuildCommentEditResult(long commentId, CommentVo? currentComment)
    {
        return new CommentEditResult
        {
            CommentId = commentId,
            PostId = currentComment?.VoPostId ?? 0,
            ParentId = currentComment?.VoParentId
        };
    }

    private static List<string> ParseTagNames(string? tags)
    {
        return string.IsNullOrWhiteSpace(tags)
            ? []
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private static List<string> NormalizeTagNames(List<string>? tagNames)
    {
        return tagNames?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }
}
