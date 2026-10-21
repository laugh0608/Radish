using System.Linq.Expressions;
using Radish.Service.Mapping;
using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Service;

/// <summary>Wiki 文档服务</summary>
public partial class WikiDocumentService
{
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
            data = WikiMapper.ToWikiDocumentVoList(entityData);
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
            ? WikiMapper.ToWikiDocumentVoList(await _wikiDocumentRepository.QueryIncludingDeletedAsync(
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

        return document == null ? null : WikiMapper.ToWikiDocumentDetailVo(document);
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
                VoDocument = WikiMapper.ToWikiDocumentDetailVo(result.Document),
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
