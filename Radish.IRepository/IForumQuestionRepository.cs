using Radish.Model;

namespace Radish.IRepository;

public sealed record ForumQuestionContext(Post Post, PostQuestion Question);

public sealed class ForumAnswerAttachmentUnavailableException : Exception;

public interface IForumQuestionRepository
{
    Task<ForumQuestionContext?> QueryQuestionAsync(long tenantId, string postIdentifier);

    Task<PostAnswer?> QueryAnswerAsync(long tenantId, string answerPublicId, bool includeDeleted = false);

    Task<PostAnswer?> QueryAnswerByIdAsync(long tenantId, long answerId, bool includeDeleted = false);

    Task<(IReadOnlyList<PostAnswer> Items, int Total)> QueryAnswerPageAsync(
        long tenantId,
        long postId,
        long? excludedAnswerId,
        int pageIndex,
        int pageSize,
        string sort);

    Task<IReadOnlyList<PostAnswerContentRevision>> QueryAnswerRevisionsAsync(
        long tenantId,
        long answerId);

    Task<PostAnswerContentRevision?> QueryAnswerRevisionAsync(
        long tenantId,
        long answerId,
        int revisionNumber);

    Task<long> InsertAnswerAsync(PostAnswer answer, PostAnswerContentRevision revision);

    Task<bool> UpdateAnswerContentAsync(
        PostAnswer answer,
        int expectedRevision,
        PostAnswerContentRevision revision);

    Task<bool> SoftDeleteAnswerAsync(
        PostAnswer answer,
        int expectedRevision,
        string operatorName,
        long operatorId,
        DateTime now);

    Task<bool> ChangeAcceptanceAsync(
        ForumQuestionContext context,
        PostAnswer? previousAnswer,
        PostAnswer? nextAnswer,
        int expectedAcceptanceRevision,
        PostAnswerAcceptanceEvent acceptanceEvent,
        string operatorName,
        long operatorId,
        DateTime now);

    Task BindAnswerAttachmentsAsync(
        long tenantId,
        long answerId,
        long revisionId,
        long authorId,
        string operatorName,
        IReadOnlySet<long> attachmentIds,
        DateTime now);
}
