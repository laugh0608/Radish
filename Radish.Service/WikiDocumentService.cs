using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using AutoMapper;
using Microsoft.Extensions.Options;
using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using Radish.Shared.Constants;
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
    private readonly IBaseRepository<Attachment> _attachmentRepository;
    private readonly IWikiAttachmentReferenceRepository _wikiAttachmentReferenceRepository;
    private readonly IConsoleAuthorizationService _consoleAuthorizationService;
    private readonly IMapper _mapper;
    private readonly DocumentOptions _documentOptions;
    private readonly IBaseRepository<WikiDocumentDraft>? _wikiDraftRepository;
    private readonly IBaseRepository<WikiDocumentCollaborator>? _wikiCollaboratorRepository;
    private readonly IBaseRepository<WikiDocumentReviewEvent>? _wikiReviewEventRepository;
    private readonly IBaseRepository<User>? _userRepository;
    private readonly IContentModerationService? _contentModerationService;
    private readonly IReliableOutboxService? _reliableOutboxService;

    public WikiDocumentService(
        IMapper mapper,
        IWikiDocumentRepository wikiDocumentRepository,
        IBaseRepository<WikiDocumentRevision> wikiDocumentRevisionRepository,
        IBaseRepository<Attachment> attachmentRepository,
        IWikiAttachmentReferenceRepository wikiAttachmentReferenceRepository,
        IConsoleAuthorizationService consoleAuthorizationService,
        IOptions<DocumentOptions> documentOptions,
        IBaseRepository<WikiDocumentDraft>? wikiDraftRepository = null,
        IBaseRepository<WikiDocumentCollaborator>? wikiCollaboratorRepository = null,
        IBaseRepository<WikiDocumentReviewEvent>? wikiReviewEventRepository = null,
        IBaseRepository<User>? userRepository = null,
        IContentModerationService? contentModerationService = null,
        IReliableOutboxService? reliableOutboxService = null)
        : base(mapper, wikiDocumentRepository)
    {
        _mapper = mapper;
        _wikiDocumentRepository = wikiDocumentRepository;
        _wikiDocumentRevisionRepository = wikiDocumentRevisionRepository;
        _attachmentRepository = attachmentRepository;
        _wikiAttachmentReferenceRepository = wikiAttachmentReferenceRepository;
        _consoleAuthorizationService = consoleAuthorizationService;
        _documentOptions = documentOptions.Value;
        _wikiDraftRepository = wikiDraftRepository;
        _wikiCollaboratorRepository = wikiCollaboratorRepository;
        _wikiReviewEventRepository = wikiReviewEventRepository;
        _userRepository = userRepository;
        _contentModerationService = contentModerationService;
        _reliableOutboxService = reliableOutboxService;
    }

    public async Task<PageModel<WikiDocumentVo>> GetPublicListAsync(
        int pageIndex = 1,
        int pageSize = 20,
        string? keyword = null,
        long? parentId = null)
    {
        if (pageIndex < 1)
        {
            pageIndex = 1;
        }

        if (pageSize < 1 || pageSize > 100)
        {
            pageSize = 20;
        }

        var whereExpression = BuildPublicReadExpression();
        if (!ShouldIncludeBuiltInDocuments())
        {
            whereExpression.And(document => document.SourceType != BuiltInSourceType);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var keywordValue = keyword.Trim();
            whereExpression.And(document =>
                document.Title.Contains(keywordValue) ||
                document.Slug.Contains(keywordValue) ||
                (document.Summary != null && document.Summary.Contains(keywordValue)));
        }

        if (parentId.HasValue)
        {
            var parentIdValue = parentId.Value;
            whereExpression.And(document => document.ParentId == parentIdValue);
        }

        var (data, totalCount) = await QueryPageAsync(
            whereExpression.ToExpression(),
            pageIndex,
            pageSize,
            document => document.Sort,
            OrderByType.Asc,
            document => document.Id,
            OrderByType.Desc);

        return new PageModel<WikiDocumentVo>
        {
            Page = pageIndex,
            PageSize = pageSize,
            DataCount = totalCount,
            PageCount = (int)Math.Ceiling(totalCount / (double)pageSize),
            Data = data
        };
    }

    public async Task<List<WikiDocumentTreeNodeVo>> GetPublicTreeAsync()
    {
        var whereExpression = BuildPublicReadExpression();
        if (!ShouldIncludeBuiltInDocuments())
        {
            whereExpression.And(document => document.SourceType != BuiltInSourceType);
        }

        var documents = await QueryWithOrderAsync(
            whereExpression.ToExpression(),
            document => document.Sort,
            OrderByType.Asc);
        return BuildTree(documents);
    }

    public async Task<WikiDocumentDetailVo?> GetPublicBySlugAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var document = await _wikiDocumentRepository.QueryFirstAsync(document =>
            document.Slug == normalizedSlug &&
            !document.IsDeleted &&
            document.Status == (int)WikiDocumentStatusEnum.Published &&
            document.Visibility == (int)WikiDocumentVisibilityEnum.Public);
        if (document == null || (!ShouldIncludeBuiltInDocuments() && IsBuiltInSourceType(document.SourceType)))
        {
            return null;
        }

        return _mapper.Map<WikiDocumentDetailVo>(document);
    }

    public async Task<PageModel<WikiDocumentVo>> GetListAsync(
        int pageIndex = 1,
        int pageSize = 20,
        string? keyword = null,
        int? status = null,
        long? parentId = null,
        bool includeUnpublished = false,
        bool includeDeleted = false,
        bool deletedOnly = false,
        bool isAuthenticated = false,
        IReadOnlyCollection<string>? roleNames = null)
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

        whereExpression.And(await BuildAccessExpressionAsync(isAuthenticated, roleNames));

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

    public async Task<List<WikiDocumentTreeNodeVo>> GetTreeAsync(
        bool includeUnpublished = false,
        bool isAuthenticated = false,
        IReadOnlyCollection<string>? roleNames = null)
    {
        var whereExpression = Expressionable.Create<WikiDocument>()
            .And(d => !d.IsDeleted)
            .And(await BuildAccessExpressionAsync(isAuthenticated, roleNames));

        if (!ShouldIncludeBuiltInDocuments())
        {
            whereExpression.And(d => d.SourceType != BuiltInSourceType);
        }
        if (!includeUnpublished)
        {
            whereExpression.And(d => d.Status == (int)WikiDocumentStatusEnum.Published);
        }

        var documents = await QueryWithOrderAsync(whereExpression.ToExpression(), d => d.Sort, OrderByType.Asc);
        return BuildTree(documents);
    }

    private static List<WikiDocumentTreeNodeVo> BuildTree(IEnumerable<WikiDocumentVo> documents)
    {
        var allNodes = documents
            .Select(document => new WikiDocumentTreeNodeVo
            {
                VoId = document.VoId,
                VoTitle = document.VoTitle,
                VoSlug = document.VoSlug,
                VoParentId = document.VoParentId,
                VoSort = document.VoSort,
                VoStatus = document.VoStatus,
                VoVisibility = document.VoVisibility,
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

    public async Task<WikiDocumentDetailVo?> GetDetailAsync(
        long id,
        bool includeUnpublished = false,
        bool includeDeleted = false,
        bool isAuthenticated = false,
        IReadOnlyCollection<string>? roleNames = null)
    {
        var document = includeDeleted
            ? await _wikiDocumentRepository.QueryByIdIncludingDeletedAsync(id)
            : await _wikiDocumentRepository.QueryByIdAsync(id);
        if (document == null)
        {
            return null;
        }

        if (!await ShouldExposeDocumentAsync(document, includeUnpublished, includeDeleted, isAuthenticated, roleNames))
        {
            return null;
        }

        return _mapper.Map<WikiDocumentDetailVo>(document);
    }

    public async Task<WikiDocumentDetailVo?> GetBySlugAsync(
        string slug,
        bool includeUnpublished = false,
        bool includeDeleted = false,
        bool isAuthenticated = false,
        IReadOnlyCollection<string>? roleNames = null)
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

        if (!await ShouldExposeDocumentAsync(document, includeUnpublished, includeDeleted, isAuthenticated, roleNames))
        {
            return null;
        }

        return _mapper.Map<WikiDocumentDetailVo>(document);
    }

    public async Task<PageModel<WikiDocumentVo>> GetGovernanceListAsync(
        int pageIndex = 1,
        int pageSize = 20,
        string? keyword = null,
        int? status = null,
        int? visibility = null,
        long? parentId = null,
        string? sourceType = null,
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
            whereExpression.And(document => document.IsDeleted);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var keywordValue = keyword.Trim();
            whereExpression.And(document =>
                document.Title.Contains(keywordValue) ||
                document.Slug.Contains(keywordValue) ||
                (document.Summary != null && document.Summary.Contains(keywordValue)) ||
                (document.SourcePath != null && document.SourcePath.Contains(keywordValue)));
        }

        if (status.HasValue)
        {
            var statusValue = status.Value;
            whereExpression.And(document => document.Status == statusValue);
        }

        if (visibility.HasValue)
        {
            var visibilityValue = visibility.Value;
            whereExpression.And(document => document.Visibility == visibilityValue);
        }

        if (parentId.HasValue)
        {
            var parentIdValue = parentId.Value;
            whereExpression.And(document => document.ParentId == parentIdValue);
        }

        var normalizedSourceType = NormalizeOptional(sourceType);
        if (!string.IsNullOrWhiteSpace(normalizedSourceType))
        {
            whereExpression.And(document => document.SourceType == normalizedSourceType);
        }

        List<WikiDocumentVo> data;
        int totalCount;

        if (includeDeleted)
        {
            var (entityData, entityTotalCount) = await _wikiDocumentRepository.QueryPageIncludingDeletedAsync(
                whereExpression.ToExpression(),
                pageIndex,
                pageSize,
                document => document.Sort,
                OrderByType.Asc,
                document => document.Id,
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
                document => document.Sort,
                OrderByType.Asc,
                document => document.Id,
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

    public async Task<List<WikiDocumentTreeNodeVo>> GetGovernanceTreeAsync(bool includeDeleted = false)
    {
        var documents = includeDeleted
            ? _mapper.Map<List<WikiDocumentVo>>(await _wikiDocumentRepository.QueryIncludingDeletedAsync(
                null,
                document => document.Sort,
                OrderByType.Asc))
            : await QueryWithOrderAsync(null, document => document.Sort, OrderByType.Asc);

        var allNodes = documents
            .Select(document => new WikiDocumentTreeNodeVo
            {
                VoId = document.VoId,
                VoTitle = document.VoTitle,
                VoSlug = document.VoSlug,
                VoParentId = document.VoParentId,
                VoSort = document.VoSort,
                VoStatus = document.VoStatus,
                VoVisibility = document.VoVisibility,
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

    public async Task<WikiDocumentDetailVo?> GetGovernanceDetailAsync(long id, bool includeDeleted = true)
    {
        if (id <= 0)
        {
            return null;
        }

        var document = includeDeleted
            ? await _wikiDocumentRepository.QueryByIdIncludingDeletedAsync(id)
            : await _wikiDocumentRepository.QueryByIdAsync(id);

        return document == null ? null : _mapper.Map<WikiDocumentDetailVo>(document);
    }

    public async Task<PageModel<WikiDocumentGovernanceEventVo>> GetGovernanceHistoryAsync(
        long documentId,
        int pageIndex = 1,
        int pageSize = 20)
    {
        if (documentId <= 0)
        {
            throw InvalidGovernanceRequest();
        }

        var safePageIndex = Math.Max(1, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        try
        {
            var document = await _wikiDocumentRepository.QueryByIdIncludingDeletedAsync(documentId);
            if (document == null)
            {
                throw GovernanceTargetUnavailable();
            }

            var (items, total) = await _wikiDocumentRepository.QueryGovernanceHistoryAsync(
                new WikiDocumentGovernanceHistoryQuery(
                    document.TenantId,
                    documentId,
                    safePageIndex,
                    safePageSize));
            return new PageModel<WikiDocumentGovernanceEventVo>
            {
                Page = safePageIndex,
                PageSize = safePageSize,
                DataCount = total,
                PageCount = (int)Math.Ceiling(total / (double)safePageSize),
                Data = items.Select(MapGovernanceEvent).ToList()
            };
        }
        catch (WikiDocumentGovernanceTargetUnavailableException)
        {
            throw GovernanceTargetUnavailable();
        }
    }

    [UseTran]
    public async Task<long> CreateDocumentAsync(CreateWikiDocumentDto createDto, long operatorId, string operatorName, long tenantId)
    {
        if (createDto == null)
        {
            throw new ArgumentNullException(nameof(createDto));
        }

        var title = NormalizeRequired(createDto.Title, nameof(createDto.Title));
        var markdownContent = NormalizeRequired(createDto.MarkdownContent, nameof(createDto.MarkdownContent));
        var slug = await EnsureUniqueSlugForCreateAsync(createDto.Slug, title);
        ValidateAccessPolicy(createDto.Visibility, createDto.AllowedRoles, createDto.AllowedPermissions);

        await ValidateParentDocumentAsync(createDto.ParentId, null);
        await ValidateWikiAttachmentReferencesAsync(
            tenantId,
            null,
            markdownContent,
            createDto.CoverAttachmentId,
            operatorId);

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
            Visibility = NormalizeVisibility(createDto.Visibility),
            AllowedRoles = SerializeAccessList(createDto.AllowedRoles),
            AllowedPermissions = SerializeAccessList(createDto.AllowedPermissions),
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

        await SyncDocumentAttachmentReferencesAsync(document, operatorId, operatorName, DateTime.UtcNow);
        await AddRevisionAsync(document, null, "Custom", operatorId, operatorName);
        return id;
    }

    [UseTran]
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
        await ValidateWikiAttachmentReferencesAsync(
            document.TenantId,
            document.Id,
            markdownContent,
            updateDto.CoverAttachmentId,
            operatorId);

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
        if (updated)
        {
            await SyncDocumentAttachmentReferencesAsync(
                document,
                operatorId,
                operatorName,
                document.ModifyTime ?? DateTime.UtcNow);
        }
        if (updated && hasMeaningfulChanges)
        {
            await AddRevisionAsync(document, NormalizeOptional(updateDto.ChangeSummary), document.SourceType, operatorId, operatorName);
        }

        return updated;
    }

    [UseTran(Propagation = Propagation.Required)]
    public async Task<WikiDocumentGovernanceMutationVo> UpdateAccessPolicyAsync(
        long id,
        UpdateWikiDocumentAccessPolicyDto updateDto,
        long operatorId,
        string operatorName)
    {
        ArgumentNullException.ThrowIfNull(updateDto);
        if (id <= 0)
        {
            throw new ArgumentException("文档ID无效", nameof(id));
        }

        var document = await _wikiDocumentRepository.QueryByIdAsync(id);
        if (document == null || document.IsDeleted)
        {
            throw GovernanceTargetUnavailable();
        }

        EnsureDocumentIsEditable(document);
        EnsureExpectedVersions(document, updateDto.ExpectedGovernanceVersion, null);
        ValidateAccessPolicy(updateDto.Visibility, updateDto.AllowedRoles, updateDto.AllowedPermissions);

        var normalizedVisibility = NormalizeVisibility(updateDto.Visibility);
        var normalizedRoles = SerializeAccessList(updateDto.AllowedRoles);
        var normalizedPermissions = SerializeAccessList(updateDto.AllowedPermissions);

        var hasChanges =
            NormalizeVisibility(document.Visibility) != normalizedVisibility ||
            document.AllowedRoles != normalizedRoles ||
            document.AllowedPermissions != normalizedPermissions;

        if (!hasChanges)
        {
            throw GovernanceActionNotApplicable();
        }

        return await ApplyGovernanceMutationAsync(
            document,
            WikiDocumentGovernanceActions.UpdateAccessPolicy,
            updateDto.ExpectedGovernanceVersion,
            null,
            document.Status,
            document.PublishedAt,
            normalizedVisibility,
            normalizedRoles,
            normalizedPermissions,
            false,
            null,
            null,
            null,
            null,
            updateDto.Reason,
            operatorId,
            operatorName);
    }

    [UseTran(Propagation = Propagation.Required)]
    public async Task<WikiDocumentGovernanceMutationVo> DeleteDocumentAsync(
        long id,
        WikiDocumentGovernanceActionDto actionDto,
        long operatorId,
        string operatorName)
    {
        ArgumentNullException.ThrowIfNull(actionDto);
        if (id <= 0)
        {
            throw new ArgumentException("文档ID无效", nameof(id));
        }

        var document = await _wikiDocumentRepository.QueryByIdAsync(id);
        if (document == null || document.IsDeleted)
        {
            throw GovernanceTargetUnavailable();
        }

        EnsureDocumentIsEditable(document);
        EnsureExpectedVersions(document, actionDto.ExpectedGovernanceVersion, null);

        var hasChildren = await _wikiDocumentRepository.QueryExistsAsync(d => d.ParentId == id && !d.IsDeleted);
        if (hasChildren)
        {
            throw new BusinessException("请先处理子文档后再删除当前文档", 409, "Wiki.ChildDocumentConflict", "error.wiki.child_document_conflict");
        }

        var now = DateTime.UtcNow;
        return await ApplyGovernanceMutationAsync(
            document,
            WikiDocumentGovernanceActions.Delete,
            actionDto.ExpectedGovernanceVersion,
            null,
            document.Status,
            document.PublishedAt,
            document.Visibility,
            document.AllowedRoles,
            document.AllowedPermissions,
            true,
            now,
            ResolveOperatorName(operatorName),
            null,
            null,
            actionDto.Reason,
            operatorId,
            operatorName,
            now);
    }

    [UseTran(Propagation = Propagation.Required)]
    public async Task<WikiDocumentGovernanceMutationVo> RestoreDocumentAsync(
        long id,
        WikiDocumentGovernanceActionDto actionDto,
        long operatorId,
        string operatorName)
    {
        ArgumentNullException.ThrowIfNull(actionDto);
        if (id <= 0)
        {
            throw new ArgumentException("文档ID无效", nameof(id));
        }

        var document = await _wikiDocumentRepository.QueryByIdIncludingDeletedAsync(id);
        if (document == null || !document.IsDeleted)
        {
            throw GovernanceTargetUnavailable();
        }

        EnsureDocumentIsEditable(document);
        EnsureExpectedVersions(document, actionDto.ExpectedGovernanceVersion, null);
        await ValidateParentDocumentAsync(document.ParentId, document.Id);

        return await ApplyGovernanceMutationAsync(
            document,
            WikiDocumentGovernanceActions.Restore,
            actionDto.ExpectedGovernanceVersion,
            null,
            document.Status,
            document.PublishedAt,
            document.Visibility,
            document.AllowedRoles,
            document.AllowedPermissions,
            false,
            null,
            null,
            null,
            null,
            actionDto.Reason,
            operatorId,
            operatorName);
    }

    [UseTran(Propagation = Propagation.Required)]
    public async Task<WikiDocumentGovernanceMutationVo> PublishAsync(
        long id,
        WikiDocumentContentGovernanceActionDto actionDto,
        long operatorId,
        string operatorName)
    {
        ArgumentNullException.ThrowIfNull(actionDto);
        var document = await _wikiDocumentRepository.QueryByIdAsync(id);
        if (document == null || document.IsDeleted)
        {
            throw GovernanceTargetUnavailable();
        }

        EnsureDocumentIsEditable(document);
        EnsureExpectedVersions(
            document,
            actionDto.ExpectedGovernanceVersion,
            actionDto.ExpectedDocumentVersion);

        if (document.Status == (int)WikiDocumentStatusEnum.Published)
        {
            throw GovernanceActionNotApplicable();
        }

        return await ApplyGovernanceMutationAsync(
            document,
            WikiDocumentGovernanceActions.Publish,
            actionDto.ExpectedGovernanceVersion,
            actionDto.ExpectedDocumentVersion,
            (int)WikiDocumentStatusEnum.Published,
            document.PublishedAt ?? DateTime.UtcNow,
            document.Visibility,
            document.AllowedRoles,
            document.AllowedPermissions,
            false,
            null,
            null,
            null,
            null,
            actionDto.Reason,
            operatorId,
            operatorName);
    }

    [UseTran(Propagation = Propagation.Required)]
    public async Task<WikiDocumentGovernanceMutationVo> UnpublishAsync(
        long id,
        WikiDocumentGovernanceActionDto actionDto,
        long operatorId,
        string operatorName)
    {
        ArgumentNullException.ThrowIfNull(actionDto);
        var document = await _wikiDocumentRepository.QueryByIdAsync(id);
        if (document == null || document.IsDeleted)
        {
            throw GovernanceTargetUnavailable();
        }

        EnsureDocumentIsEditable(document);
        EnsureExpectedVersions(document, actionDto.ExpectedGovernanceVersion, null);

        if (document.Status == (int)WikiDocumentStatusEnum.Draft)
        {
            throw GovernanceActionNotApplicable();
        }

        return await ApplyGovernanceMutationAsync(
            document,
            WikiDocumentGovernanceActions.Unpublish,
            actionDto.ExpectedGovernanceVersion,
            null,
            (int)WikiDocumentStatusEnum.Draft,
            document.PublishedAt,
            document.Visibility,
            document.AllowedRoles,
            document.AllowedPermissions,
            false,
            null,
            null,
            null,
            null,
            actionDto.Reason,
            operatorId,
            operatorName);
    }

    [UseTran(Propagation = Propagation.Required)]
    public async Task<WikiDocumentGovernanceMutationVo> ArchiveAsync(
        long id,
        WikiDocumentGovernanceActionDto actionDto,
        long operatorId,
        string operatorName)
    {
        ArgumentNullException.ThrowIfNull(actionDto);
        var document = await _wikiDocumentRepository.QueryByIdAsync(id);
        if (document == null || document.IsDeleted)
        {
            throw GovernanceTargetUnavailable();
        }

        EnsureDocumentIsEditable(document);
        EnsureExpectedVersions(document, actionDto.ExpectedGovernanceVersion, null);

        if (document.Status == (int)WikiDocumentStatusEnum.Archived)
        {
            throw GovernanceActionNotApplicable();
        }

        return await ApplyGovernanceMutationAsync(
            document,
            WikiDocumentGovernanceActions.Archive,
            actionDto.ExpectedGovernanceVersion,
            null,
            (int)WikiDocumentStatusEnum.Archived,
            document.PublishedAt,
            document.Visibility,
            document.AllowedRoles,
            document.AllowedPermissions,
            false,
            null,
            null,
            null,
            null,
            actionDto.Reason,
            operatorId,
            operatorName);
    }

    public async Task<PageModel<WikiDocumentRevisionItemVo>> GetRevisionListAsync(
        long documentId,
        int pageIndex = 1,
        int pageSize = 20)
    {
        if (documentId <= 0)
        {
            throw InvalidGovernanceRequest();
        }

        var document = await _wikiDocumentRepository.QueryByIdAsync(documentId);
        if (document == null || document.IsDeleted)
        {
            throw GovernanceTargetUnavailable();
        }

        var safePageIndex = Math.Max(1, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var (revisions, total) = await _wikiDocumentRevisionRepository.QueryPageAsync(
            r => r.DocumentId == documentId,
            safePageIndex,
            safePageSize,
            r => r.Version,
            OrderByType.Desc,
            r => r.Id,
            OrderByType.Desc);

        return new PageModel<WikiDocumentRevisionItemVo>
        {
            Page = safePageIndex,
            PageSize = safePageSize,
            DataCount = total,
            PageCount = (int)Math.Ceiling(total / (double)safePageSize),
            Data = revisions.Select(revision => MapRevisionItem(revision, document.Version)).ToList()
        };
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

    [UseTran]
    public async Task<WikiDocumentGovernanceMutationVo> RollbackAsync(
        long revisionId,
        WikiDocumentContentGovernanceActionDto actionDto,
        long operatorId,
        string operatorName)
    {
        ArgumentNullException.ThrowIfNull(actionDto);
        if (revisionId <= 0)
        {
            throw new ArgumentException("版本ID无效", nameof(revisionId));
        }

        var revision = await _wikiDocumentRevisionRepository.QueryByIdAsync(revisionId);
        if (revision == null)
        {
            throw new BusinessException("版本不存在", 404, "Wiki.RevisionNotFound", "error.wiki.revision_not_found");
        }

        var document = await _wikiDocumentRepository.QueryByIdAsync(revision.DocumentId);
        if (document == null || document.IsDeleted)
        {
            throw GovernanceTargetUnavailable();
        }

        EnsureDocumentIsEditable(document);
        EnsureExpectedVersions(
            document,
            actionDto.ExpectedGovernanceVersion,
            actionDto.ExpectedDocumentVersion);

        var isSameContent =
            document.Title == revision.Title &&
            document.MarkdownContent == revision.MarkdownContent;

        if (isSameContent)
        {
            throw new BusinessException($"当前文档已是 v{revision.Version} 的内容，无需回滚", 409, "Wiki.RevisionAlreadyCurrent", "error.wiki.revision_already_current");
        }

        await ValidateWikiAttachmentReferencesAsync(
            document.TenantId,
            document.Id,
            revision.MarkdownContent,
            document.CoverAttachmentId,
            operatorId);

        var mutation = await ApplyGovernanceMutationAsync(
            document,
            WikiDocumentGovernanceActions.Rollback,
            actionDto.ExpectedGovernanceVersion,
            actionDto.ExpectedDocumentVersion,
            document.Status,
            document.PublishedAt,
            document.Visibility,
            document.AllowedRoles,
            document.AllowedPermissions,
            false,
            null,
            null,
            new WikiDocumentGovernanceContentMutation(
                revision.Title,
                revision.MarkdownContent,
                document.Version + 1),
            revision.Id,
            actionDto.Reason,
            operatorId,
            operatorName);

        var updatedDocument = await _wikiDocumentRepository.QueryByIdAsync(document.Id)
            ?? throw GovernanceTargetUnavailable();
        await SyncDocumentAttachmentReferencesAsync(
            updatedDocument,
            operatorId,
            operatorName,
            updatedDocument.ModifyTime ?? DateTime.UtcNow);
        await AddRevisionAsync(updatedDocument, $"回滚到 v{revision.Version}", "Rollback", operatorId, operatorName);
        return mutation;
    }

    private async Task<WikiDocumentGovernanceMutationVo> ApplyGovernanceMutationAsync(
        WikiDocument document,
        string action,
        int expectedGovernanceVersion,
        int? expectedDocumentVersion,
        int targetStatus,
        DateTime? targetPublishedAt,
        int targetVisibility,
        string? targetAllowedRoles,
        string? targetAllowedPermissions,
        bool targetIsDeleted,
        DateTime? targetDeletedAt,
        string? targetDeletedBy,
        WikiDocumentGovernanceContentMutation? contentMutation,
        long? sourceRevisionId,
        string? reason,
        long operatorId,
        string operatorName,
        DateTime? nowUtc = null)
    {
        if (document.Id <= 0 || operatorId <= 0 || expectedGovernanceVersion < 0 ||
            expectedDocumentVersion is <= 0 || !WikiDocumentGovernanceActions.All.Contains(action))
        {
            throw InvalidGovernanceRequest();
        }

        var normalizedReason = NormalizeOptional(reason);
        if (string.IsNullOrWhiteSpace(normalizedReason))
        {
            throw new BusinessException(
                "文档治理操作必须填写理由",
                400,
                "Wiki.GovernanceReasonRequired",
                "error.wiki.governance_reason_required");
        }
        if (normalizedReason.Length > 500)
        {
            throw InvalidGovernanceRequest();
        }

        try
        {
            var result = await _wikiDocumentRepository.ApplyGovernanceMutationAsync(
                new WikiDocumentGovernanceMutationCommand(
                    document.TenantId,
                    document.Id,
                    action,
                    expectedGovernanceVersion,
                    expectedDocumentVersion,
                    targetStatus,
                    targetPublishedAt,
                    targetVisibility,
                    targetAllowedRoles,
                    targetAllowedPermissions,
                    targetIsDeleted,
                    targetDeletedAt,
                    targetDeletedBy,
                    contentMutation,
                    sourceRevisionId,
                    normalizedReason,
                    operatorId,
                    ResolveOperatorName(operatorName),
                    nowUtc ?? DateTime.UtcNow));
            return new WikiDocumentGovernanceMutationVo
            {
                VoDocument = _mapper.Map<WikiDocumentDetailVo>(result.Document),
                VoEvent = MapGovernanceEvent(result.GovernanceEvent)
            };
        }
        catch (WikiDocumentGovernanceTargetUnavailableException)
        {
            throw GovernanceTargetUnavailable();
        }
        catch (WikiDocumentGovernanceVersionConflictException)
        {
            throw GovernanceVersionConflict();
        }
        catch (WikiDocumentContentVersionConflictException)
        {
            throw DocumentVersionConflict();
        }
    }

    [UseTran]
    public async Task<long> ImportMarkdownAsync(WikiMarkdownImportDto importDto, long operatorId, string operatorName, long tenantId)
    {
        if (importDto == null)
        {
            throw new ArgumentNullException(nameof(importDto));
        }

        if (importDto.File == null || importDto.File.Length <= 0)
        {
            throw new BusinessException("Markdown 文件不能为空", 400, "Wiki.MarkdownFileEmpty", "error.wiki.markdown_file_empty");
        }

        var extension = Path.GetExtension(importDto.File.FileName).ToLowerInvariant();
        if (extension != ".md" && extension != ".markdown" && extension != ".txt")
        {
            throw new BusinessException("仅支持 Markdown 文本文件导入", 400, "Wiki.MarkdownFileUnsupported", "error.wiki.markdown_file_unsupported");
        }

        var markdownContent = await ReadMarkdownAsync(importDto.File);
        if (string.IsNullOrWhiteSpace(markdownContent))
        {
            throw new BusinessException("导入的 Markdown 内容不能为空", 400, "Wiki.MarkdownContentEmpty", "error.wiki.markdown_content_empty");
        }

        var titleFromHeading = ExtractTitle(markdownContent);
        var title = !string.IsNullOrWhiteSpace(titleFromHeading)
            ? titleFromHeading
            : Path.GetFileNameWithoutExtension(importDto.File.FileName);

        var slug = await EnsureUniqueSlugForCreateAsync(importDto.Slug, title);
        ValidateAccessPolicy(importDto.Visibility, importDto.AllowedRoles, importDto.AllowedPermissions);

        await ValidateParentDocumentAsync(importDto.ParentId, null);
        await ValidateWikiAttachmentReferencesAsync(
            tenantId,
            null,
            markdownContent,
            null,
            operatorId);

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
            Visibility = NormalizeVisibility(importDto.Visibility),
            AllowedRoles = SerializeAccessList(importDto.AllowedRoles),
            AllowedPermissions = SerializeAccessList(importDto.AllowedPermissions),
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
        await SyncDocumentAttachmentReferencesAsync(document, operatorId, operatorName, DateTime.UtcNow);
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

        if (!ShouldIncludeBuiltInDocuments() && IsBuiltInSourceType(document.SourceType))
        {
            return null;
        }

        if (!includeUnpublished && document.Status != (int)WikiDocumentStatusEnum.Published)
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

        revision.Id = await _wikiDocumentRevisionRepository.AddAsync(revision);
        await SyncRevisionAttachmentReferencesAsync(
            document,
            revision,
            operatorId,
            operatorName,
            revision.CreateTime);
    }

    private static WikiDocumentGovernanceEventVo MapGovernanceEvent(
        WikiDocumentGovernanceEvent governanceEvent)
    {
        return new WikiDocumentGovernanceEventVo
        {
            VoId = governanceEvent.Id,
            VoDocumentId = governanceEvent.DocumentId,
            VoAction = governanceEvent.Action,
            VoFromStatus = governanceEvent.FromStatus,
            VoToStatus = governanceEvent.ToStatus,
            VoFromVisibility = governanceEvent.FromVisibility,
            VoToVisibility = governanceEvent.ToVisibility,
            VoFromAllowedRoles = ParseAccessList(governanceEvent.FromAllowedRoles).ToList(),
            VoToAllowedRoles = ParseAccessList(governanceEvent.ToAllowedRoles).ToList(),
            VoFromAllowedPermissions = ParseAccessList(governanceEvent.FromAllowedPermissions).ToList(),
            VoToAllowedPermissions = ParseAccessList(governanceEvent.ToAllowedPermissions).ToList(),
            VoFromIsDeleted = governanceEvent.FromIsDeleted,
            VoToIsDeleted = governanceEvent.ToIsDeleted,
            VoFromDocumentVersion = governanceEvent.FromDocumentVersion,
            VoToDocumentVersion = governanceEvent.ToDocumentVersion,
            VoExpectedGovernanceVersion = governanceEvent.ExpectedGovernanceVersion,
            VoResultGovernanceVersion = governanceEvent.ResultGovernanceVersion,
            VoSourceRevisionId = governanceEvent.SourceRevisionId,
            VoReason = governanceEvent.Reason,
            VoActorUserId = governanceEvent.ActorUserId,
            VoActorName = governanceEvent.ActorName,
            VoCreateTime = governanceEvent.CreateTime
        };
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

    private static int NormalizeVisibility(int visibility)
    {
        return visibility is >= (int)WikiDocumentVisibilityEnum.Public and <= (int)WikiDocumentVisibilityEnum.Restricted
            ? visibility
            : (int)WikiDocumentVisibilityEnum.Authenticated;
    }

    private static string? SerializeAccessList(IEnumerable<string>? values)
    {
        if (values == null)
        {
            return null;
        }

        var normalized = values
            .Select(value => value?.Trim().ToLowerInvariant())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalized.Count == 0
            ? null
            : $"|{string.Join("|", normalized)}|";
    }

    private static IReadOnlyCollection<string> ParseAccessList(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        return rawValue
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<Expression<Func<WikiDocument, bool>>> BuildAccessExpressionAsync(
        bool isAuthenticated,
        IReadOnlyCollection<string>? roleNames)
    {
        var normalizedRoles = (roleNames ?? [])
            .Select(role => role.Trim().ToLowerInvariant())
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var normalizedPermissions = isAuthenticated && normalizedRoles.Length > 0
            ? (await _consoleAuthorizationService.GetPermissionKeysByRolesAsync(normalizedRoles))
                .Select(permission => permission.Trim().ToLowerInvariant())
                .Where(permission => !string.IsNullOrWhiteSpace(permission))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

        var accessExpression = Expressionable.Create<WikiDocument>()
            .And(document => document.Visibility == (int)WikiDocumentVisibilityEnum.Public);

        if (isAuthenticated)
        {
            accessExpression.Or(document =>
                document.Visibility == (int)WikiDocumentVisibilityEnum.Authenticated ||
                document.Visibility <= 0);
        }

        foreach (var role in normalizedRoles)
        {
            var marker = $"|{role}|";
            accessExpression.Or(document =>
                document.Visibility == (int)WikiDocumentVisibilityEnum.Restricted &&
                document.AllowedRoles != null &&
                document.AllowedRoles.Contains(marker));
        }

        foreach (var permission in normalizedPermissions)
        {
            var marker = $"|{permission}|";
            accessExpression.Or(document =>
                document.Visibility == (int)WikiDocumentVisibilityEnum.Restricted &&
                document.AllowedPermissions != null &&
                document.AllowedPermissions.Contains(marker));
        }

        return accessExpression.ToExpression();
    }

    private static Expressionable<WikiDocument> BuildPublicReadExpression()
    {
        return Expressionable.Create<WikiDocument>()
            .And(document => !document.IsDeleted)
            .And(document => document.Status == (int)WikiDocumentStatusEnum.Published)
            .And(document => document.Visibility == (int)WikiDocumentVisibilityEnum.Public);
    }

    private static string ResolveOperatorName(string? operatorName)
    {
        return string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName.Trim();
    }

    private static void ValidateAccessPolicy(
        int visibility,
        IEnumerable<string>? allowedRoles,
        IEnumerable<string>? allowedPermissions)
    {
        if (NormalizeVisibility(visibility) != (int)WikiDocumentVisibilityEnum.Restricted)
        {
            return;
        }

        var hasAllowedRoles = (allowedRoles ?? [])
            .Any(role => !string.IsNullOrWhiteSpace(role));
        var hasAllowedPermissions = (allowedPermissions ?? [])
            .Any(permission => !string.IsNullOrWhiteSpace(permission));

        if (!hasAllowedRoles && !hasAllowedPermissions)
        {
            throw new BusinessException("受限文档至少需要配置一个角色或权限", 400, "Wiki.AccessPolicyRequired", "error.wiki.access_policy_required");
        }
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
            throw new BusinessException("父级文档不能是自身", 400, "Wiki.ParentCannotBeSelf", "error.wiki.parent_cannot_be_self");
        }

        var currentParent = await _wikiDocumentRepository.QueryByIdAsync(candidateParentId);
        if (currentParent == null || currentParent.IsDeleted)
        {
            throw new BusinessException("父级文档不存在", 404, "Wiki.ParentNotFound", "error.wiki.parent_not_found");
        }

        if (!currentDocumentId.HasValue)
        {
            return;
        }

        while (currentParent.ParentId.HasValue)
        {
            if (currentParent.ParentId.Value == currentDocumentId.Value)
            {
                throw new BusinessException("父级文档不能设置为当前文档的子孙节点", 409, "Wiki.ParentCycleConflict", "error.wiki.parent_cycle_conflict");
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

    private static BusinessException InvalidGovernanceRequest() => new(
        "文档治理请求无效",
        400,
        "Wiki.GovernanceRequestInvalid",
        "error.wiki.governance_request_invalid");

    private static BusinessException GovernanceTargetUnavailable() => new(
        "文档不存在或不在当前租户的治理范围内",
        404,
        "Wiki.GovernanceTargetUnavailable",
        "error.wiki.governance_target_unavailable");

    private static BusinessException GovernanceActionNotApplicable() => new(
        "文档当前状态不适用该治理动作",
        409,
        "Wiki.GovernanceActionNotApplicable",
        "error.wiki.governance_action_not_applicable");

    private static void EnsureExpectedVersions(
        WikiDocument document,
        int expectedGovernanceVersion,
        int? expectedDocumentVersion)
    {
        if (document.GovernanceVersion != expectedGovernanceVersion)
        {
            throw GovernanceVersionConflict();
        }

        if (expectedDocumentVersion.HasValue && document.Version != expectedDocumentVersion.Value)
        {
            throw DocumentVersionConflict();
        }
    }

    private static BusinessException GovernanceVersionConflict() => new(
        "文档治理状态已变化，请刷新后重新确认",
        409,
        "Wiki.GovernanceVersionConflict",
        "error.wiki.governance_version_conflict");

    private static BusinessException DocumentVersionConflict() => new(
        "文档正文版本已变化，请刷新证据后重新确认",
        409,
        "Wiki.DocumentVersionConflict",
        "error.wiki.document_version_conflict");
}
