using Radish.IService.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>Wiki 文档服务接口</summary>
public interface IWikiDocumentService : IBaseService<WikiDocument, WikiDocumentVo>
{
    Task<PageModel<WikiDocumentVo>> GetListAsync(
        int pageIndex = 1,
        int pageSize = 20,
        string? keyword = null,
        int? status = null,
        long? parentId = null,
        bool includeUnpublished = false);

    Task<List<WikiDocumentTreeNodeVo>> GetTreeAsync(bool includeUnpublished = false);

    Task<WikiDocumentDetailVo?> GetDetailAsync(long id, bool includeUnpublished = false);

    Task<WikiDocumentDetailVo?> GetBySlugAsync(string slug, bool includeUnpublished = false);

    Task<long> CreateDocumentAsync(CreateWikiDocumentDto createDto, long operatorId, string operatorName, long tenantId);

    Task<bool> UpdateDocumentAsync(long id, UpdateWikiDocumentDto updateDto, long operatorId, string operatorName);

    Task<bool> PublishAsync(long id, long operatorId, string operatorName);

    Task<bool> UnpublishAsync(long id, long operatorId, string operatorName);

    Task<bool> ArchiveAsync(long id, long operatorId, string operatorName);

    Task<List<WikiDocumentRevisionItemVo>> GetRevisionListAsync(long documentId);

    Task<WikiDocumentRevisionDetailVo?> GetRevisionDetailAsync(long revisionId);

    Task<bool> RollbackAsync(long revisionId, long operatorId, string operatorName);

    Task<long> ImportMarkdownAsync(WikiMarkdownImportDto importDto, long operatorId, string operatorName, long tenantId);

    Task<(string fileName, string markdownContent)?> ExportMarkdownAsync(long id, bool includeUnpublished = false);
}
