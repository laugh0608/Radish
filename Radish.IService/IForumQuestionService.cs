using Radish.Model.ViewModels;

namespace Radish.IService;

public interface IForumQuestionService
{
    Task<PostAnswerPageVo> GetAnswerPageAsync(
        long tenantId,
        string postIdentifier,
        int pageIndex,
        int pageSize,
        string sort,
        long currentUserId);

    Task<PostAnswerMutationVo> CreateAnswerAsync(
        long tenantId,
        string postIdentifier,
        string content,
        long authorId,
        string authorName,
        string? clientSubmissionId);

    Task<PostAnswerMutationVo> UpdateAnswerAsync(
        long tenantId,
        string answerPublicId,
        string content,
        int expectedContentRevision,
        long operatorId,
        string operatorName,
        string clientSubmissionId);

    Task<PostAnswerMutationVo> DeleteAnswerAsync(
        long tenantId,
        string answerPublicId,
        int expectedContentRevision,
        long operatorId,
        string operatorName,
        string clientSubmissionId);

    Task<PostAnswerRevisionListVo> GetAnswerRevisionsAsync(
        long tenantId,
        string answerPublicId,
        long currentUserId);

    Task<PostAnswerRevisionDetailVo> GetAnswerRevisionAsync(
        long tenantId,
        string answerPublicId,
        int revisionNumber,
        long currentUserId);

    Task<PostAnswerMutationVo> RestoreAnswerRevisionAsync(
        long tenantId,
        string answerPublicId,
        int revisionNumber,
        int expectedContentRevision,
        long operatorId,
        string operatorName,
        string clientSubmissionId);

    Task<PostAnswerAcceptanceMutationVo> AcceptAnswerAsync(
        long tenantId,
        string postIdentifier,
        string answerPublicId,
        int expectedAcceptanceRevision,
        long operatorId,
        string operatorName,
        string clientSubmissionId);

    Task<PostAnswerAcceptanceMutationVo> RevokeAcceptanceAsync(
        long tenantId,
        string postIdentifier,
        int expectedAcceptanceRevision,
        long operatorId,
        string operatorName,
        string clientSubmissionId);
}
