using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using AutoMapper;
using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Service;

/// <summary>Wiki 文档服务</summary>
public partial class WikiDocumentService : BaseService<WikiDocument, WikiDocumentVo>, IWikiDocumentService
{
    private static readonly Regex HeadingRegex = new(@"^#\s+(.+)$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex InvalidSlugCharRegex = new(@"[^a-z0-9-]", RegexOptions.Compiled);
    private static readonly Regex MultiDashRegex = new(@"-{2,}", RegexOptions.Compiled);

    private readonly IWikiDocumentRepository _wikiDocumentRepository;
    private readonly IBaseRepository<WikiDocumentRevision> _wikiDocumentRevisionRepository;
    private readonly IMapper _mapper;
    private readonly DocumentOptions _documentOptions;

    public WikiDocumentService(
        IMapper mapper,
        IWikiDocumentRepository wikiDocumentRepository,
        IBaseRepository<WikiDocumentRevision> wikiDocumentRevisionRepository,
        IOptions<DocumentOptions> documentOptions)
        : base(mapper, wikiDocumentRepository)
    {
        _mapper = mapper;
        _wikiDocumentRepository = wikiDocumentRepository;
        _wikiDocumentRevisionRepository = wikiDocumentRevisionRepository;
        _documentOptions = documentOptions.Value;
    }

    public async Task<PageModel<WikiDocumentVo>> GetListAsync(
        int pageIndex = 1,
        int pageSize = 20,
        string? keyword = null,
        int? status = null,
        long? parentId = null,
        bool includeUnpublished = false,
        bool includeDeleted = false,
        bool deletedOnly = false)
    {
        if (pageIndex < 1)
        {
            pageIndex = 1;
        }

        if (pageSize < 1 || pageSize > 100)
        {
            pageSize = 20;
        }

        var whereExpression = Expressionable.Create<WikiDocument>();

        if (deletedOnly)
        {
            includeDeleted = true;
            whereExpression.And(d => d.IsDeleted);
        }

        if (!ShouldIncludeBuiltInDocuments())
        {
            whereExpression.And(d => d.SourceType != BuiltInSourceType);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var keywordValue = keyword.Trim();
            whereExpression.And(d =>
                d.Title.Contains(keywordValue) ||
                d.Slug.Contains(keywordValue) ||
                (d.Summary != null && d.Summary.Contains(keywordValue)));
        }

        if (parentId.HasValue)
        {
            var parentIdValue = parentId.Value;
            whereExpression.And(d => d.ParentId == parentIdValue);
        }

        if (status.HasValue)
        {
            var statusValue = status.Value;
            whereExpression.And(d => d.Status == statusValue);
        }
        else if (!includeUnpublished)
        {
            whereExpression.And(d => d.Status == (int)WikiDocumentStatusEnum.Published);
        }

        List<WikiDocumentVo> data;
        int totalCount;

        if (includeDeleted)
        {
            var (entityData, entityTotalCount) = await _wikiDocumentRepository.QueryPageIncludingDeletedAsync(
                whereExpression.ToExpression(),
                pageIndex,
                pageSize,
                d => d.Sort,
                OrderByType.Asc,
                d => d.Id,
                OrderByType.Desc);
            data = _mapper.Map<List<WikiDocumentVo>>(entityData);
            totalCount = entityTotalCount;
        }
        else
        {
            var (voData, voTotalCount) = await QueryPageAsync(
                whereExpression.ToExpression(),
                pageIndex,
                pageSize,
                d => d.Sort,
                OrderByType.Asc,
                d => d.Id,
                OrderByType.Desc);
            data = voData;
            totalCount = voTotalCount;
        }

        return new PageModel<WikiDocumentVo>
        {
            Page = pageIndex,
            PageSize = pageSize,
            DataCount = totalCount,
            PageCount = (int)Math.Ceiling(totalCount / (double)pageSize),
            Data = data
        };
    }

    public async Task<List<WikiDocumentTreeNodeVo>> GetTreeAsync(bool includeUnpublished = false)
    {
        var whereExpression = Expressionable.Create<WikiDocument>()
            .And(d => !d.IsDeleted);

        if (!ShouldIncludeBuiltInDocuments())
        {
            whereExpression.And(d => d.SourceType != BuiltInSourceType);
        }
        if (!includeUnpublished)
        {
            whereExpression.And(d => d.Status == (int)WikiDocumentStatusEnum.Published);
        }

        var documents = await QueryWithOrderAsync(whereExpression.ToExpression(), d => d.Sort, OrderByType.Asc);
        var allNodes = documents
            .Select(document => new WikiDocumentTreeNodeVo
            {
                VoId = document.VoId,
                VoTitle = document.VoTitle,
                VoSlug = document.VoSlug,
                VoParentId = document.VoParentId,
                VoSort = document.VoSort,
                VoStatus = document.VoStatus,
                VoChildren = new List<WikiDocumentTreeNodeVo>()
            })
            .ToList();

        var lookup = allNodes.ToDictionary(node => node.VoId);
        var roots = new List<WikiDocumentTreeNodeVo>();

        foreach (var node in allNodes)
        {
            if (node.VoParentId.HasValue && lookup.TryGetValue(node.VoParentId.Value, out var parent))
            {
                parent.VoChildren.Add(node);
                continue;
            }

            roots.Add(node);
        }

        return roots;
    }

    public async Task<WikiDocumentDetailVo?> GetDetailAsync(long id, bool includeUnpublished = false, bool includeDeleted = false)
    {
        var document = includeDeleted
            ? await _wikiDocumentRepository.QueryByIdIncludingDeletedAsync(id)
            : await _wikiDocumentRepository.QueryByIdAsync(id);
        if (document == null)
        {
            return null;
        }

        if (!ShouldExposeDocument(document, includeUnpublished, includeDeleted))
        {
            return null;
        }

        return _mapper.Map<WikiDocumentDetailVo>(document);
    }

    public async Task<WikiDocumentDetailVo?> GetBySlugAsync(string slug, bool includeUnpublished = false, bool includeDeleted = false)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var document = await _wikiDocumentRepository.QueryFirstAsync(d => d.Slug == normalizedSlug && !d.IsDeleted);
        if (document == null)
        {
            return null;
        }

        if (!ShouldExposeDocument(document, includeUnpublished, includeDeleted))
        {
            return null;
        }

        return _mapper.Map<WikiDocumentDetailVo>(document);
    }

    public async Task<long> CreateDocumentAsync(CreateWikiDocumentDto createDto, long operatorId, string operatorName, long tenantId)
    {
        if (createDto == null)
        {
            throw new ArgumentNullException(nameof(createDto));
        }

        var title = NormalizeRequired(createDto.Title, nameof(createDto.Title));
        var markdownContent = NormalizeRequired(createDto.MarkdownContent, nameof(createDto.MarkdownContent));
        var slug = await EnsureUniqueSlugForCreateAsync(createDto.Slug, title);

        await ValidateParentDocumentAsync(createDto.ParentId, null);

        var document = new WikiDocument
        {
            Title = title,
            Slug = slug,
            Summary = NormalizeOptional(createDto.Summary),
            MarkdownContent = markdownContent,
            CoverAttachmentId = createDto.CoverAttachmentId,
            ParentId = createDto.ParentId,
            Sort = createDto.Sort,
            Status = (int)WikiDocumentStatusEnum.Draft,
            SourceType = "Custom",
            SourcePath = null,
            Version = 1,
            TenantId = tenantId,
            CreateId = operatorId,
            CreateBy = ResolveOperatorName(operatorName),
            CreateTime = DateTime.Now
        };

        var id = await AddAsync(document);
        document.Id = id;

        await AddRevisionAsync(document, null, "Custom", operatorId, operatorName);
        return id;
    }

    public async Task<bool> UpdateDocumentAsync(long id, UpdateWikiDocumentDto updateDto, long operatorId, string operatorName)
    {
        if (id <= 0)
        {
            throw new ArgumentException("文档ID无效", nameof(id));
        }

        if (updateDto == null)
        {
            throw new ArgumentNullException(nameof(updateDto));
        }

        var document = await _wikiDocumentRepository.QueryByIdAsync(id);
        if (document == null || document.IsDeleted)
        {
            return false;
        }

        EnsureDocumentIsEditable(document);

        var title = NormalizeRequired(updateDto.Title, nameof(updateDto.Title));
        var markdownContent = NormalizeRequired(updateDto.MarkdownContent, nameof(updateDto.MarkdownContent));
        var slug = await EnsureUniqueSlugForUpdateAsync(updateDto.Slug, title, id);

        await ValidateParentDocumentAsync(updateDto.ParentId, id);

        var hasMeaningfulChanges =
            document.Title != title ||
            document.Slug != slug ||
            document.Summary != NormalizeOptional(updateDto.Summary) ||
            document.MarkdownContent != markdownContent ||
            document.ParentId != updateDto.ParentId ||
            document.Sort != updateDto.Sort ||
            document.CoverAttachmentId != updateDto.CoverAttachmentId;

        document.Title = title;
        document.Slug = slug;
        document.Summary = NormalizeOptional(updateDto.Summary);
        document.MarkdownContent = markdownContent;
        document.ParentId = updateDto.ParentId;
        document.Sort = updateDto.Sort;
        document.CoverAttachmentId = updateDto.CoverAttachmentId;
        document.ModifyId = operatorId;
        document.ModifyBy = ResolveOperatorName(operatorName);
        document.ModifyTime = DateTime.Now;

        if (hasMeaningfulChanges)
        {
            document.Version += 1;
        }

        var updated = await UpdateAsync(document);
        if (updated && hasMeaningfulChanges)
        {
            await AddRevisionAsync(document, NormalizeOptional(updateDto.ChangeSummary), document.SourceType, operatorId, operatorName);
        }

        return updated;
    }

    public async Task<bool> DeleteDocumentAsync(long id, long operatorId, string operatorName)
    {
        if (id <= 0)
        {
            throw new ArgumentException("文档ID无效", nameof(id));
        }

        var document = await _wikiDocumentRepository.QueryByIdAsync(id);
        if (document == null || document.IsDeleted)
        {
            return false;
        }

        EnsureDocumentIsEditable(document);

        var hasChildren = await _wikiDocumentRepository.QueryExistsAsync(d => d.ParentId == id && !d.IsDeleted);
        if (hasChildren)
        {
            throw new InvalidOperationException("请先处理子文档后再删除当前文档");
        }

        return await _wikiDocumentRepository.SoftDeleteByIdAsync(id, ResolveOperatorName(operatorName));
    }

    public async Task<bool> RestoreDocumentAsync(long id, long operatorId, string operatorName)
    {
        if (id <= 0)
        {
            throw new ArgumentException("文档ID无效", nameof(id));
        }

        var document = await _wikiDocumentRepository.QueryByIdIncludingDeletedAsync(id);
        if (document == null || !document.IsDeleted)
        {
            return false;
        }

        EnsureDocumentIsEditable(document);
        await ValidateParentDocumentAsync(document.ParentId, document.Id);

        var restored = await _wikiDocumentRepository.RestoreByIdAsync(id);
        if (!restored)
        {
            return false;
        }

        document.IsDeleted = false;
        document.DeletedAt = null;
        document.DeletedBy = null;
        document.ModifyId = operatorId;
        document.ModifyBy = ResolveOperatorName(operatorName);
        document.ModifyTime = DateTime.Now;
        return await UpdateAsync(document);
    }

    public async Task<bool> PublishAsync(long id, long operatorId, string operatorName)
    {
        var document = await _wikiDocumentRepository.QueryByIdAsync(id);
        if (document == null || document.IsDeleted)
        {
            return false;
        }

        EnsureDocumentIsEditable(document);

        document.Status = (int)WikiDocumentStatusEnum.Published;
        document.PublishedAt ??= DateTime.Now;
        document.ModifyId = operatorId;
        document.ModifyBy = ResolveOperatorName(operatorName);
        document.ModifyTime = DateTime.Now;
        return await UpdateAsync(document);
    }

    public async Task<bool> UnpublishAsync(long id, long operatorId, string operatorName)
    {
        var document = await _wikiDocumentRepository.QueryByIdAsync(id);
        if (document == null || document.IsDeleted)
        {
            return false;
        }

        EnsureDocumentIsEditable(document);

        document.Status = (int)WikiDocumentStatusEnum.Draft;
        document.ModifyId = operatorId;
        document.ModifyBy = ResolveOperatorName(operatorName);
        document.ModifyTime = DateTime.Now;
        return await UpdateAsync(document);
    }

    public async Task<bool> ArchiveAsync(long id, long operatorId, string operatorName)
    {
        var document = await _wikiDocumentRepository.QueryByIdAsync(id);
        if (document == null || document.IsDeleted)
        {
            return false;
        }

        EnsureDocumentIsEditable(document);

        document.Status = (int)WikiDocumentStatusEnum.Archived;
        document.ModifyId = operatorId;
        document.ModifyBy = ResolveOperatorName(operatorName);
        document.ModifyTime = DateTime.Now;
        return await UpdateAsync(document);
    }

    public async Task<List<WikiDocumentRevisionItemVo>> GetRevisionListAsync(long documentId)
    {
        if (documentId <= 0)
        {
            return [];
        }

        var document = await _wikiDocumentRepository.QueryByIdAsync(documentId);
        if (document == null || document.IsDeleted)
        {
            return [];
        }

        var revisions = await _wikiDocumentRevisionRepository.QueryWithOrderAsync(
            r => r.DocumentId == documentId,
            r => r.Version,
            OrderByType.Desc);

        return revisions
            .Select(revision => MapRevisionItem(revision, document.Version))
            .ToList();
    }

    public async Task<WikiDocumentRevisionDetailVo?> GetRevisionDetailAsync(long revisionId)
    {
        if (revisionId <= 0)
        {
            return null;
        }

        var revision = await _wikiDocumentRevisionRepository.QueryByIdAsync(revisionId);
        if (revision == null)
        {
            return null;
        }

        var document = await _wikiDocumentRepository.QueryByIdAsync(revision.DocumentId);
        if (document == null || document.IsDeleted)
        {
            return null;
        }

        return MapRevisionDetail(revision, document.Version);
    }

    public async Task<bool> RollbackAsync(long revisionId, long operatorId, string operatorName)
    {
        if (revisionId <= 0)
        {
            throw new ArgumentException("版本ID无效", nameof(revisionId));
        }

        var revision = await _wikiDocumentRevisionRepository.QueryByIdAsync(revisionId);
        if (revision == null)
        {
            return false;
        }

        var document = await _wikiDocumentRepository.QueryByIdAsync(revision.DocumentId);
        if (document == null || document.IsDeleted)
        {
            return false;
        }

        EnsureDocumentIsEditable(document);

        var isSameContent =
            document.Title == revision.Title &&
            document.MarkdownContent == revision.MarkdownContent;

        if (isSameContent)
        {
            throw new InvalidOperationException($"当前文档已是 v{revision.Version} 的内容，无需回滚");
        }

        document.Title = revision.Title;
        document.MarkdownContent = revision.MarkdownContent;
        document.Version += 1;
        document.ModifyId = operatorId;
        document.ModifyBy = ResolveOperatorName(operatorName);
        document.ModifyTime = DateTime.Now;

        var updated = await UpdateAsync(document);
        if (!updated)
        {
            return false;
        }

        await AddRevisionAsync(document, $"回滚到 v{revision.Version}", "Rollback", operatorId, operatorName);
        return true;
    }

    public async Task<long> ImportMarkdownAsync(WikiMarkdownImportDto importDto, long operatorId, string operatorName, long tenantId)
    {
        if (importDto == null)
        {
            throw new ArgumentNullException(nameof(importDto));
        }

        if (importDto.File == null || importDto.File.Length <= 0)
        {
            throw new InvalidOperationException("Markdown 文件不能为空");
        }

        var extension = Path.GetExtension(importDto.File.FileName).ToLowerInvariant();
        if (extension != ".md" && extension != ".markdown" && extension != ".txt")
        {
            throw new InvalidOperationException("仅支持 Markdown 文本文件导入");
        }

        var markdownContent = await ReadMarkdownAsync(importDto.File);
        if (string.IsNullOrWhiteSpace(markdownContent))
        {
            throw new InvalidOperationException("导入的 Markdown 内容不能为空");
        }

        var titleFromHeading = ExtractTitle(markdownContent);
        var title = !string.IsNullOrWhiteSpace(titleFromHeading)
            ? titleFromHeading
            : Path.GetFileNameWithoutExtension(importDto.File.FileName);

        var slug = await EnsureUniqueSlugForCreateAsync(importDto.Slug, title);

        await ValidateParentDocumentAsync(importDto.ParentId, null);

        var status = importDto.PublishAfterImport
            ? (int)WikiDocumentStatusEnum.Published
            : (int)WikiDocumentStatusEnum.Draft;

        var document = new WikiDocument
        {
            Title = NormalizeRequired(title, nameof(title)),
            Slug = slug,
            Summary = NormalizeOptional(importDto.Summary),
            MarkdownContent = markdownContent,
            ParentId = importDto.ParentId,
            Sort = importDto.Sort,
            Status = status,
            SourceType = "Imported",
            SourcePath = importDto.File.FileName,
            Version = 1,
            TenantId = tenantId,
            PublishedAt = importDto.PublishAfterImport ? DateTime.Now : null,
            CreateId = operatorId,
            CreateBy = ResolveOperatorName(operatorName),
            CreateTime = DateTime.Now
        };

        var id = await AddAsync(document);
        document.Id = id;
        await AddRevisionAsync(document, "导入 Markdown 文档", "Imported", operatorId, operatorName);
        return id;
    }

    public async Task<(string fileName, string markdownContent)?> ExportMarkdownAsync(long id, bool includeUnpublished = false)
    {
        var document = await _wikiDocumentRepository.QueryByIdAsync(id);
        if (document == null || document.IsDeleted)
        {
            return null;
        }

        if (!ShouldExposeDocument(document, includeUnpublished, includeDeleted: false))
        {
            return null;
        }

        var fileName = string.IsNullOrWhiteSpace(document.Slug) ? $"wiki-{id}.md" : $"{document.Slug}.md";
        return (fileName, document.MarkdownContent);
    }

    private async Task AddRevisionAsync(WikiDocument document, string? changeSummary, string sourceType, long operatorId, string operatorName)
    {
        var revision = new WikiDocumentRevision
        {
            DocumentId = document.Id,
            Version = document.Version,
            Title = document.Title,
            MarkdownContent = document.MarkdownContent,
            ChangeSummary = changeSummary,
            SourceType = string.IsNullOrWhiteSpace(sourceType) ? document.SourceType : sourceType,
            TenantId = document.TenantId,
            CreateId = operatorId,
            CreateBy = ResolveOperatorName(operatorName),
            CreateTime = DateTime.Now
        };

        await _wikiDocumentRevisionRepository.AddAsync(revision);
    }

    private async Task<string> EnsureUniqueSlugForCreateAsync(string? requestedSlug, string titleSeed)
    {
        var baseSlug = BuildSlug(!string.IsNullOrWhiteSpace(requestedSlug) ? requestedSlug : titleSeed);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = $"wiki-{Guid.NewGuid():N}"[..13];
        }

        var slug = baseSlug;
        var index = 2;
        while (await _wikiDocumentRepository.QueryExistsAsync(d => d.Slug == slug && !d.IsDeleted))
        {
            slug = $"{baseSlug}-{index}";
            index++;
        }

        return slug;
    }

    private async Task<string> EnsureUniqueSlugForUpdateAsync(string? requestedSlug, string titleSeed, long documentId)
    {
        var baseSlug = BuildSlug(!string.IsNullOrWhiteSpace(requestedSlug) ? requestedSlug : titleSeed);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = $"wiki-{Guid.NewGuid():N}"[..13];
        }

        var slug = baseSlug;
        var index = 2;
        while (await _wikiDocumentRepository.QueryExistsAsync(d => d.Slug == slug && !d.IsDeleted && d.Id != documentId))
        {
            slug = $"{baseSlug}-{index}";
            index++;
        }

        return slug;
    }

    private static string BuildSlug(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        normalized = normalized.Replace("_", "-").Replace(" ", "-");
        normalized = InvalidSlugCharRegex.Replace(normalized, "-");
        normalized = MultiDashRegex.Replace(normalized, "-").Trim('-');
        return normalized.Length > 80 ? normalized[..80].Trim('-') : normalized;
    }

    private static string NormalizeRequired(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} 不能为空", paramName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string ResolveOperatorName(string? operatorName)
    {
        return string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName.Trim();
    }

    private async Task ValidateParentDocumentAsync(long? parentId, long? currentDocumentId)
    {
        if (!parentId.HasValue)
        {
            return;
        }

        var candidateParentId = parentId.Value;
        if (currentDocumentId.HasValue && candidateParentId == currentDocumentId.Value)
        {
            throw new InvalidOperationException("父级文档不能是自身");
        }

        var currentParent = await _wikiDocumentRepository.QueryByIdAsync(candidateParentId);
        if (currentParent == null || currentParent.IsDeleted)
        {
            throw new InvalidOperationException("父级文档不存在");
        }

        if (!currentDocumentId.HasValue)
        {
            return;
        }

        while (currentParent.ParentId.HasValue)
        {
            if (currentParent.ParentId.Value == currentDocumentId.Value)
            {
                throw new InvalidOperationException("父级文档不能设置为当前文档的子孙节点");
            }

            currentParent = await _wikiDocumentRepository.QueryByIdAsync(currentParent.ParentId.Value);
            if (currentParent == null || currentParent.IsDeleted)
            {
                break;
            }
        }
    }

    private static string? ExtractTitle(string markdownContent)
    {
        var match = HeadingRegex.Match(markdownContent);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static async Task<string> ReadMarkdownAsync(Microsoft.AspNetCore.Http.IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync();
    }

    private static WikiDocumentRevisionItemVo MapRevisionItem(WikiDocumentRevision revision, int currentVersion)
    {
        return new WikiDocumentRevisionItemVo
        {
            VoId = revision.Id,
            VoDocumentId = revision.DocumentId,
            VoVersion = revision.Version,
            VoTitle = revision.Title,
            VoChangeSummary = revision.ChangeSummary,
            VoSourceType = revision.SourceType,
            VoCreateTime = revision.CreateTime,
            VoCreateBy = revision.CreateBy,
            VoIsCurrent = revision.Version == currentVersion
        };
    }

    private static WikiDocumentRevisionDetailVo MapRevisionDetail(WikiDocumentRevision revision, int currentVersion)
    {
        return new WikiDocumentRevisionDetailVo
        {
            VoId = revision.Id,
            VoDocumentId = revision.DocumentId,
            VoVersion = revision.Version,
            VoTitle = revision.Title,
            VoMarkdownContent = revision.MarkdownContent,
            VoChangeSummary = revision.ChangeSummary,
            VoSourceType = revision.SourceType,
            VoCreateTime = revision.CreateTime,
            VoCreateBy = revision.CreateBy,
            VoCreateId = revision.CreateId,
            VoIsCurrent = revision.Version == currentVersion
        };
    }
}
