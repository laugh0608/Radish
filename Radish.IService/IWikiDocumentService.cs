using Radish.IService.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>Wiki 文档服务接口</summary>
public interface IWikiDocumentService : IBaseService<WikiDocument, WikiDocumentVo>
{
    Task<PageModel<WikiAuthorDocumentVo>> AuthorGetListAsync(long userId, int pageIndex, int pageSize);
    Task<WikiAuthorDraftDetailVo?> AuthorGetByIdAsync(long documentId, long userId, bool isSystemOrAdmin = false);
    Task<WikiAuthorDraftDetailVo> AuthorCreateAsync(CreateWikiAuthorDraftDto dto, long userId, string userName, long tenantId);
    Task<WikiAuthorDraftDetailVo> AuthorStartDraftAsync(long documentId, long userId, string userName, long tenantId, bool isSystemOrAdmin = false);
    Task<WikiAuthorDraftDetailVo> AuthorSaveDraftAsync(long draftId, SaveWikiAuthorDraftDto dto, long userId, string userName, long tenantId, bool isSystemOrAdmin = false);
    Task<WikiAuthorDraftDetailVo> AuthorSubmitDraftAsync(long draftId, SubmitWikiDraftDto dto, long userId, string userName, long tenantId, bool isSystemOrAdmin = false);
    Task<WikiAuthorDraftDetailVo> AuthorWithdrawDraftAsync(long draftId, int expectedDraftVersion, long userId, string userName, long tenantId, bool isSystemOrAdmin = false);
    Task<IReadOnlyList<WikiDocumentCollaboratorVo>> AuthorGetCollaboratorsAsync(long documentId, long userId, bool isSystemOrAdmin = false);
    Task<WikiDocumentCollaboratorVo> AuthorInviteCollaboratorAsync(long documentId, InviteWikiCollaboratorDto dto, long userId, string userName, long tenantId, bool isSystemOrAdmin = false);
    Task<WikiDocumentCollaboratorVo> AuthorRespondInvitationAsync(long collaboratorId, RespondWikiCollaboratorInvitationDto dto, long userId, string userName, long tenantId);
    Task<bool> AuthorRemoveCollaboratorAsync(long collaboratorId, long userId, string userName, long tenantId, bool isSystemOrAdmin = false);
    Task<PageModel<WikiReviewQueueItemVo>> AdminGetReviewQueueAsync(int pageIndex, int pageSize);
    Task<WikiAuthorDraftDetailVo?> AdminGetDraftByIdAsync(long draftId);
    Task<WikiAuthorDraftDetailVo> AdminReviewDraftAsync(long draftId, ReviewWikiDraftDto dto, long reviewerId, string reviewerName, long tenantId);

    Task<PageModel<WikiDocumentVo>> GetPublicListAsync(
        int pageIndex = 1,
        int pageSize = 20,
        string? keyword = null,
        long? parentId = null);

    Task<List<WikiDocumentTreeNodeVo>> GetPublicTreeAsync();

    Task<WikiDocumentDetailVo?> GetPublicBySlugAsync(string slug);

    Task<PageModel<WikiDocumentVo>> GetListAsync(
        int pageIndex = 1,
        int pageSize = 20,
        string? keyword = null,
        int? status = null,
        long? parentId = null,
        bool includeUnpublished = false,
        bool includeDeleted = false,
        bool deletedOnly = false,
        bool isAuthenticated = false,
        IReadOnlyCollection<string>? roleNames = null);

    Task<List<WikiDocumentTreeNodeVo>> GetTreeAsync(
        bool includeUnpublished = false,
        bool isAuthenticated = false,
        IReadOnlyCollection<string>? roleNames = null);

    Task<WikiDocumentDetailVo?> GetDetailAsync(
        long id,
        bool includeUnpublished = false,
        bool includeDeleted = false,
        bool isAuthenticated = false,
        IReadOnlyCollection<string>? roleNames = null);

    Task<WikiDocumentDetailVo?> GetBySlugAsync(
        string slug,
        bool includeUnpublished = false,
        bool includeDeleted = false,
        bool isAuthenticated = false,
        IReadOnlyCollection<string>? roleNames = null);

    Task<PageModel<WikiDocumentVo>> GetGovernanceListAsync(
        int pageIndex = 1,
        int pageSize = 20,
        string? keyword = null,
        int? status = null,
        int? visibility = null,
        long? parentId = null,
        string? sourceType = null,
        bool includeDeleted = false,
        bool deletedOnly = false);

    Task<List<WikiDocumentTreeNodeVo>> GetGovernanceTreeAsync(bool includeDeleted = false);

    Task<WikiDocumentDetailVo?> GetGovernanceDetailAsync(long id, bool includeDeleted = true);

    Task<long> CreateDocumentAsync(CreateWikiDocumentDto createDto, long operatorId, string operatorName, long tenantId);

    Task<bool> UpdateDocumentAsync(long id, UpdateWikiDocumentDto updateDto, long operatorId, string operatorName);

    Task<bool> UpdateAccessPolicyAsync(long id, UpdateWikiDocumentAccessPolicyDto updateDto, long operatorId, string operatorName);

    Task<bool> DeleteDocumentAsync(long id, long operatorId, string operatorName);

    Task<bool> RestoreDocumentAsync(long id, long operatorId, string operatorName);

    Task<bool> PublishAsync(long id, long operatorId, string operatorName);

    Task<bool> UnpublishAsync(long id, long operatorId, string operatorName);

    Task<bool> ArchiveAsync(long id, long operatorId, string operatorName);

    Task<List<WikiDocumentRevisionItemVo>> GetRevisionListAsync(long documentId);

    Task<WikiDocumentRevisionDetailVo?> GetRevisionDetailAsync(long revisionId);

    Task<bool> RollbackAsync(long revisionId, long operatorId, string operatorName);

    Task<long> ImportMarkdownAsync(WikiMarkdownImportDto importDto, long operatorId, string operatorName, long tenantId);

    Task<(string fileName, string markdownContent)?> ExportMarkdownAsync(long id, bool includeUnpublished = false);

    Task<WikiBuiltInSyncSummary> SyncBuiltInDocumentsAsync(CancellationToken cancellationToken = default);
}
