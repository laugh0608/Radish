using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;

namespace Radish.Service;

/// <summary>论坛内容创建写入可靠性服务。</summary>
public class ForumContentWriteService : IForumContentWriteService
{
    private const int PostCreateDuplicateWindowSeconds = 180;
    private const int CommentCreateDuplicateWindowSeconds = 60;
    private const int AnswerCreateDuplicateWindowSeconds = 120;

    private const string CategoryTargetType = "Category";
    private const string PostTargetType = "Post";

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

    public async Task<ContentWriteResult<long>> PublishPostAsync(
        Post post,
        CreatePollDto? poll,
        CreateLotteryDto? lottery,
        bool isQuestion,
        List<string>? tagNames,
        bool allowCreateTag,
        string? clientSubmissionId)
    {
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
            DuplicateWindowSeconds = PostCreateDuplicateWindowSeconds
        });

        if (TryResolveReplay(beginResult, ContentSubmissionResultTypes.Post, out var postId))
        {
            return beginResult.Status == ContentSubmissionBeginStatus.DuplicateContent
                ? ContentWriteResult<long>.DuplicateContentResult(postId, beginResult.Message)
                : ContentWriteResult<long>.ReplayedResult(postId, beginResult.Message);
        }

        EnsureStarted(beginResult);

        long? createdPostId = null;
        try
        {
            createdPostId = await _postService.PublishPostAsync(
                post,
                poll,
                lottery,
                isQuestion,
                tagNames,
                allowCreateTag);

            await CompleteSuccessAsync(
                beginResult,
                ContentSubmissionResultTypes.Post,
                createdPostId.Value,
                post.PublicId);

            return ContentWriteResult<long>.CreatedResult(createdPostId.Value);
        }
        catch (Exception ex)
        {
            if (createdPostId.HasValue)
            {
                await CompleteSuccessAsync(
                    beginResult,
                    ContentSubmissionResultTypes.Post,
                    createdPostId.Value,
                    post.PublicId);

                return ContentWriteResult<long>.CreatedResult(createdPostId.Value);
            }

            await CompleteFailureAsync(beginResult, ex);
            throw;
        }
    }

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
            DuplicateWindowSeconds = CommentCreateDuplicateWindowSeconds
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

        long? createdCommentId = null;
        CommentHighlightRecheckResultVo? createdHighlightRecheckResult = null;
        try
        {
            var (newCommentId, highlightRecheckResult) = await _commentService.AddCommentAsync(comment);
            createdCommentId = newCommentId;
            createdHighlightRecheckResult = highlightRecheckResult;

            await CompleteSuccessAsync(
                beginResult,
                ContentSubmissionResultTypes.Comment,
                createdCommentId.Value,
                null);

            return ContentWriteResult<CommentCreateResult>.CreatedResult(new CommentCreateResult
            {
                CommentId = createdCommentId.Value,
                HighlightRecheckResult = createdHighlightRecheckResult
            });
        }
        catch (Exception ex)
        {
            if (createdCommentId.HasValue)
            {
                await CompleteSuccessAsync(
                    beginResult,
                    ContentSubmissionResultTypes.Comment,
                    createdCommentId.Value,
                    null);

                return ContentWriteResult<CommentCreateResult>.CreatedResult(new CommentCreateResult
                {
                    CommentId = createdCommentId.Value,
                    HighlightRecheckResult = createdHighlightRecheckResult ?? new CommentHighlightRecheckResultVo
                    {
                        VoPostId = comment.PostId,
                        VoChanged = false
                    }
                });
            }

            await CompleteFailureAsync(beginResult, ex);
            throw;
        }
    }

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
            DuplicateWindowSeconds = AnswerCreateDuplicateWindowSeconds
        });

        if (TryResolveReplay(beginResult, ContentSubmissionResultTypes.PostQuestion, out var replayPostId))
        {
            var question = await ResolveQuestionAsync(replayPostId, authorId);
            return beginResult.Status == ContentSubmissionBeginStatus.DuplicateContent
                ? ContentWriteResult<PostQuestionVo>.DuplicateContentResult(question, beginResult.Message)
                : ContentWriteResult<PostQuestionVo>.ReplayedResult(question, beginResult.Message);
        }

        EnsureStarted(beginResult);

        PostQuestionVo? createdQuestion = null;
        try
        {
            createdQuestion = await _postService.AddAnswerAsync(postId, content, authorId, authorName, tenantId);
            await CompleteSuccessAsync(
                beginResult,
                ContentSubmissionResultTypes.PostQuestion,
                postId,
                null);

            return ContentWriteResult<PostQuestionVo>.CreatedResult(createdQuestion);
        }
        catch (Exception ex)
        {
            if (createdQuestion != null)
            {
                await CompleteSuccessAsync(
                    beginResult,
                    ContentSubmissionResultTypes.PostQuestion,
                    postId,
                    null);

                return ContentWriteResult<PostQuestionVo>.CreatedResult(createdQuestion);
            }

            await CompleteFailureAsync(beginResult, ex);
            throw;
        }
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

    private static void EnsureStarted(ContentSubmissionBeginResult beginResult)
    {
        if (beginResult.Status == ContentSubmissionBeginStatus.Started && beginResult.RecordId.HasValue)
        {
            return;
        }

        throw new ArgumentException(beginResult.Message ?? "内容提交状态无效，请稍后重试");
    }

    private async Task CompleteSuccessAsync(
        ContentSubmissionBeginResult beginResult,
        string resultType,
        long resultId,
        string? resultPublicId)
    {
        if (!beginResult.RecordId.HasValue)
        {
            return;
        }

        await _contentSubmissionService.CompleteSuccessAsync(new ContentSubmissionCompletionRequest
        {
            RecordId = beginResult.RecordId.Value,
            ResultType = resultType,
            ResultId = resultId,
            ResultPublicId = resultPublicId
        });
    }

    private async Task CompleteFailureAsync(ContentSubmissionBeginResult beginResult, Exception exception)
    {
        if (!beginResult.RecordId.HasValue)
        {
            return;
        }

        await _contentSubmissionService.CompleteFailureAsync(
            beginResult.RecordId.Value,
            exception.GetType().Name,
            exception.Message);
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
