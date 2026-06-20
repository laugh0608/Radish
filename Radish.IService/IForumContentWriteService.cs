using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>论坛内容创建写入可靠性服务。</summary>
public interface IForumContentWriteService
{
    Task<ContentWriteResult<long>> PublishPostAsync(
        Post post,
        CreatePollDto? poll,
        CreateLotteryDto? lottery,
        bool isQuestion,
        List<string>? tagNames,
        bool allowCreateTag,
        string? clientSubmissionId);

    Task<ContentWriteResult<CommentCreateResult>> CreateCommentAsync(
        Comment comment,
        string? clientSubmissionId);

    Task<ContentWriteResult<PostQuestionVo>> AddAnswerAsync(
        long postId,
        string content,
        long authorId,
        string authorName,
        long tenantId,
        string? clientSubmissionId);
}

public enum ContentWriteStatus
{
    Created = 1,
    Replayed = 2,
    DuplicateContent = 3
}

public sealed class ContentWriteResult<T>
{
    public ContentWriteStatus Status { get; init; }

    public T Result { get; init; } = default!;

    public string? Message { get; init; }

    public bool Created => Status == ContentWriteStatus.Created;

    public static ContentWriteResult<T> CreatedResult(T result, string? message = null)
    {
        return new ContentWriteResult<T>
        {
            Status = ContentWriteStatus.Created,
            Result = result,
            Message = message
        };
    }

    public static ContentWriteResult<T> ReplayedResult(T result, string? message = null)
    {
        return new ContentWriteResult<T>
        {
            Status = ContentWriteStatus.Replayed,
            Result = result,
            Message = message
        };
    }

    public static ContentWriteResult<T> DuplicateContentResult(T result, string? message = null)
    {
        return new ContentWriteResult<T>
        {
            Status = ContentWriteStatus.DuplicateContent,
            Result = result,
            Message = message
        };
    }
}

public sealed class CommentCreateResult
{
    public long CommentId { get; init; }

    public CommentHighlightRecheckResultVo HighlightRecheckResult { get; init; } = new();
}
