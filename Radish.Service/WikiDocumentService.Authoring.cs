using System.Text;
using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Service;

public partial class WikiDocumentService
{
    public async Task<PageModel<WikiAuthorDocumentVo>> AuthorGetListAsync(long userId, int pageIndex, int pageSize)
    {
        EnsureAuthoringAvailable();
        pageIndex = Math.Max(1, pageIndex);
        pageSize = pageSize is > 0 and <= 100 ? pageSize : 20;
        var collaboratorDocumentIds = (await _wikiCollaboratorRepository!.QueryAsync(item =>
                item.UserId == userId &&
                item.InviteState == (int)WikiDocumentCollaboratorState.Accepted &&
                !item.IsDeleted))
            .Select(item => item.DocumentId)
            .Distinct()
            .ToList();
        var (documents, total) = await _wikiDocumentRepository.QueryPageAsync(
            document =>
                !document.IsDeleted &&
                (document.OwnerUserId == userId || collaboratorDocumentIds.Contains(document.Id)),
            pageIndex,
            pageSize,
            document => document.ModifyTime ?? document.CreateTime,
            OrderByType.Desc);
        var draftIds = documents.Where(document => document.ActiveDraftId.HasValue)
            .Select(document => document.ActiveDraftId!.Value)
            .ToList();
        var drafts = draftIds.Count == 0
            ? []
            : await _wikiDraftRepository!.QueryAsync(draft => draftIds.Contains(draft.Id) && !draft.IsDeleted);
        var draftMap = drafts.ToDictionary(draft => draft.Id);

        return new PageModel<WikiAuthorDocumentVo>
        {
            Page = pageIndex,
            PageSize = pageSize,
            DataCount = total,
            PageCount = (int)Math.Ceiling(total / (double)pageSize),
            Data = documents.Select(document =>
            {
                draftMap.TryGetValue(document.ActiveDraftId.GetValueOrDefault(), out var draft);
                var isOwner = document.OwnerUserId == userId;
                return new WikiAuthorDocumentVo
                {
                    VoDocumentId = document.Id,
                    VoDraftId = draft?.Id,
                    VoTitle = draft?.Title ?? document.Title,
                    VoSlug = draft?.Slug ?? document.Slug,
                    VoSummary = draft?.Summary ?? document.Summary,
                    VoDocumentVersion = document.Version,
                    VoDraftVersion = draft?.DraftVersion,
                    VoReviewState = draft?.ReviewState,
                    VoStatus = document.Status,
                    VoAuthorRole = isOwner ? "Owner" : "Editor",
                    VoCanEdit = draft != null && IsEditableDraftState(draft.ReviewState),
                    VoCanSubmit = isOwner && draft != null && IsEditableDraftState(draft.ReviewState),
                    VoCanManageCollaborators = isOwner,
                    VoCreateTime = document.CreateTime,
                    VoModifyTime = draft?.ModifyTime ?? document.ModifyTime
                };
            }).ToList()
        };
    }

    public async Task<WikiAuthorDraftDetailVo?> AuthorGetByIdAsync(
        long documentId,
        long userId,
        bool isSystemOrAdmin = false)
    {
        EnsureAuthoringAvailable();
        var document = await _wikiDocumentRepository.QueryByIdAsync(documentId);
        if (document == null || document.IsDeleted || document.ActiveDraftId == null)
        {
            return null;
        }

        var role = await ResolveAuthorRoleAsync(document, userId, isSystemOrAdmin);
        if (role == null)
        {
            return null;
        }

        var draft = await _wikiDraftRepository!.QueryByIdAsync(document.ActiveDraftId.Value);
        return draft == null || draft.IsDeleted
            ? null
            : await BuildDraftDetailWithEvidenceAsync(document, draft, role);
    }

    [UseTran]
    public async Task<WikiAuthorDraftDetailVo> AuthorCreateAsync(
        CreateWikiAuthorDraftDto dto,
        long userId,
        string userName,
        long tenantId)
    {
        EnsureAuthoringAvailable();
        await EnsureCanOpenContributionAsync(userId);
        await EnsureOwnedDraftCapacityAsync(userId);
        ValidateDraftPayload(dto.MarkdownContent);
        var now = DateTime.UtcNow;
        var title = NormalizeRequired(dto.Title, nameof(dto.Title));
        var slug = await EnsureUniqueSlugForCreateAsync(dto.Slug, title);
        await ValidateParentDocumentAsync(dto.ProposedParentId, null);
        var draftId = SnowFlakeSingle.Instance.NextId();
        var document = new WikiDocument
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = tenantId,
            Title = title,
            Slug = slug,
            Summary = NormalizeOptional(dto.Summary),
            MarkdownContent = string.Empty,
            CoverAttachmentId = null,
            ParentId = null,
            Status = (int)WikiDocumentStatusEnum.Draft,
            Visibility = (int)WikiDocumentVisibilityEnum.Authenticated,
            SourceType = "Custom",
            Version = 0,
            OwnerUserId = userId,
            ActiveDraftId = draftId,
            CreateId = userId,
            CreateBy = ResolveOperatorName(userName),
            CreateTime = now
        };
        var draft = new WikiDocumentDraft
        {
            Id = draftId,
            TenantId = tenantId,
            DocumentId = document.Id,
            BaseDocumentVersion = 0,
            DraftVersion = 1,
            Title = title,
            Slug = slug,
            Summary = NormalizeOptional(dto.Summary),
            MarkdownContent = NormalizeRequired(dto.MarkdownContent, nameof(dto.MarkdownContent)),
            CoverAttachmentId = dto.CoverAttachmentId,
            ProposedParentId = dto.ProposedParentId,
            ChangeSummary = NormalizeOptional(dto.ChangeSummary),
            ReviewState = (int)WikiDocumentDraftState.Editing,
            CreateId = userId,
            CreateBy = ResolveOperatorName(userName),
            CreateTime = now
        };
        await _wikiDocumentRepository.AddAsync(document);
        await _wikiDraftRepository!.AddAsync(draft);
        return BuildDraftDetail(document, draft, "Owner");
    }

    [UseTran]
    public async Task<WikiAuthorDraftDetailVo> AuthorStartDraftAsync(
        long documentId,
        long userId,
        string userName,
        long tenantId,
        bool isSystemOrAdmin = false)
    {
        EnsureAuthoringAvailable();
        var document = await RequireDocumentAsync(documentId, tenantId);
        EnsureCustomDocument(document);
        var role = await ResolveAuthorRoleAsync(document, userId, isSystemOrAdmin);
        if (role is not ("Owner" or "Administrator"))
        {
            throw AuthorNotFound();
        }
        if (document.ActiveDraftId.HasValue)
        {
            throw Conflict("文档已有活跃草稿", "Wiki.ActiveDraftAlreadyExists", "error.wiki.active_draft_exists");
        }
        await EnsureOwnedDraftCapacityAsync(document.OwnerUserId ?? userId);
        var now = DateTime.UtcNow;
        var draft = new WikiDocumentDraft
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = tenantId,
            DocumentId = document.Id,
            BaseDocumentVersion = document.Version,
            DraftVersion = 1,
            Title = document.Title,
            Slug = document.Slug,
            Summary = document.Summary,
            MarkdownContent = document.MarkdownContent,
            CoverAttachmentId = document.CoverAttachmentId,
            ProposedParentId = document.ParentId,
            ReviewState = (int)WikiDocumentDraftState.Editing,
            CreateId = userId,
            CreateBy = ResolveOperatorName(userName),
            CreateTime = now
        };
        await _wikiDraftRepository!.AddAsync(draft);
        if (await _wikiDocumentRepository.SetActiveDraftAsync(
                document.Id, tenantId, null, draft.Id, userId, ResolveOperatorName(userName), now) != 1)
        {
            throw Conflict("文档草稿状态已变化", "Wiki.DraftStateConflict", "error.wiki.draft_state_conflict");
        }
        document.ActiveDraftId = draft.Id;
        return BuildDraftDetail(document, draft, role);
    }

    [UseTran]
    public async Task<WikiAuthorDraftDetailVo> AuthorSaveDraftAsync(
        long draftId,
        SaveWikiAuthorDraftDto dto,
        long userId,
        string userName,
        long tenantId,
        bool isSystemOrAdmin = false)
    {
        EnsureAuthoringAvailable();
        ValidateDraftPayload(dto.MarkdownContent);
        var (document, draft, role) = await RequireDraftAccessAsync(draftId, userId, tenantId, isSystemOrAdmin);
        if (role == "Reviewer" || !IsEditableDraftState(draft.ReviewState))
        {
            throw Conflict("当前草稿状态不可编辑", "Wiki.DraftStateConflict", "error.wiki.draft_state_conflict");
        }
        var title = NormalizeRequired(dto.Title, nameof(dto.Title));
        var slug = await EnsureUniqueSlugForUpdateAsync(dto.Slug, title, document.Id);
        await ValidateParentDocumentAsync(dto.ProposedParentId, document.Id);
        var affected = await _wikiDocumentRepository.SaveDraftAsync(new WikiDraftSaveCommand(
            draft.Id, tenantId, dto.ExpectedDraftVersion, title, slug, NormalizeOptional(dto.Summary),
            NormalizeRequired(dto.MarkdownContent, nameof(dto.MarkdownContent)), dto.CoverAttachmentId,
            dto.ProposedParentId, NormalizeOptional(dto.ChangeSummary), userId, ResolveOperatorName(userName), DateTime.UtcNow));
        if (affected != 1)
        {
            var (_, currentDraft, _) = await RequireDraftAccessAsync(
                draftId, userId, tenantId, isSystemOrAdmin);
            if (!IsEditableDraftState(currentDraft.ReviewState))
            {
                throw Conflict("当前草稿状态不可编辑", "Wiki.DraftStateConflict", "error.wiki.draft_state_conflict");
            }
            throw Conflict("草稿版本已变化", "Wiki.DraftVersionConflict", "error.wiki.draft_version_conflict");
        }
        draft = await _wikiDraftRepository!.QueryByIdAsync(draft.Id)
            ?? throw AuthorNotFound();
        return BuildDraftDetail(document, draft, role);
    }

    [UseTran]
    public async Task<WikiAuthorDraftDetailVo> AuthorSubmitDraftAsync(
        long draftId,
        SubmitWikiDraftDto dto,
        long userId,
        string userName,
        long tenantId,
        bool isSystemOrAdmin = false)
    {
        EnsureAuthoringAvailable();
        var (document, draft, role) = await RequireDraftAccessAsync(draftId, userId, tenantId, isSystemOrAdmin);
        if (role is not ("Owner" or "Administrator"))
        {
            throw AuthorNotFound();
        }
        if (draft.ReviewState == (int)WikiDocumentDraftState.Submitted &&
            draft.DraftVersion == dto.ExpectedDraftVersion + 1)
        {
            return BuildDraftDetail(document, draft, role);
        }
        await EnsureCanOpenContributionAsync(userId);
        var now = DateTime.UtcNow;
        var affected = await _wikiDocumentRepository.TransitionDraftAsync(new WikiDraftTransitionCommand(
            draft.Id, tenantId, dto.ExpectedDraftVersion,
            [(int)WikiDocumentDraftState.Editing, (int)WikiDocumentDraftState.ChangesRequested],
            (int)WikiDocumentDraftState.Submitted, NormalizeOptional(dto.ChangeSummary), null,
            userId, ResolveOperatorName(userName), now));
        if (affected != 1)
        {
            var (currentDocument, currentDraft, currentRole) = await RequireDraftAccessAsync(
                draftId, userId, tenantId, isSystemOrAdmin);
            if (currentDraft.ReviewState == (int)WikiDocumentDraftState.Submitted &&
                currentDraft.DraftVersion == dto.ExpectedDraftVersion + 1)
            {
                return BuildDraftDetail(currentDocument, currentDraft, currentRole);
            }
            if (!IsEditableDraftState(currentDraft.ReviewState))
            {
                throw Conflict("当前草稿状态不可提交", "Wiki.DraftStateConflict", "error.wiki.draft_state_conflict");
            }
            throw Conflict("草稿版本或状态已变化", "Wiki.DraftVersionConflict", "error.wiki.draft_version_conflict");
        }
        await AddReviewEventAsync(document, draft, WikiDocumentReviewActions.Submit, userId, userName,
            NormalizeOptional(dto.ChangeSummary), draft.DraftVersion + 1, now);
        draft = await _wikiDraftRepository!.QueryByIdAsync(draft.Id) ?? throw AuthorNotFound();
        return BuildDraftDetail(document, draft, role);
    }

    [UseTran]
    public async Task<WikiAuthorDraftDetailVo> AuthorWithdrawDraftAsync(
        long draftId,
        int expectedDraftVersion,
        long userId,
        string userName,
        long tenantId,
        bool isSystemOrAdmin = false)
    {
        EnsureAuthoringAvailable();
        var (document, draft, role) = await RequireDraftAccessAsync(draftId, userId, tenantId, isSystemOrAdmin);
        if (role is not ("Owner" or "Administrator"))
        {
            throw AuthorNotFound();
        }
        if (draft.ReviewState == (int)WikiDocumentDraftState.Withdrawn &&
            draft.DraftVersion == expectedDraftVersion + 1)
        {
            return BuildDraftDetail(document, draft, role);
        }
        var now = DateTime.UtcNow;
        var affected = await _wikiDocumentRepository.TransitionDraftAsync(new WikiDraftTransitionCommand(
            draft.Id, tenantId, expectedDraftVersion,
            [(int)WikiDocumentDraftState.Editing, (int)WikiDocumentDraftState.Submitted, (int)WikiDocumentDraftState.ChangesRequested],
            (int)WikiDocumentDraftState.Withdrawn, draft.ChangeSummary, null,
            userId, ResolveOperatorName(userName), now));
        if (affected != 1)
        {
            var (currentDocument, currentDraft, currentRole) = await RequireDraftAccessAsync(
                draftId, userId, tenantId, isSystemOrAdmin);
            if (currentDraft.ReviewState == (int)WikiDocumentDraftState.Withdrawn &&
                currentDraft.DraftVersion == expectedDraftVersion + 1)
            {
                return BuildDraftDetail(currentDocument, currentDraft, currentRole);
            }
            if (currentDraft.ReviewState is not ((int)WikiDocumentDraftState.Editing or
                (int)WikiDocumentDraftState.Submitted or
                (int)WikiDocumentDraftState.ChangesRequested))
            {
                throw Conflict("当前草稿状态不可撤回", "Wiki.DraftStateConflict", "error.wiki.draft_state_conflict");
            }
            throw Conflict("草稿版本已变化", "Wiki.DraftVersionConflict", "error.wiki.draft_version_conflict");
        }
        if (await _wikiDocumentRepository.SetActiveDraftAsync(
                document.Id, tenantId, draft.Id, null, userId, ResolveOperatorName(userName), now) != 1)
        {
            throw Conflict("草稿版本或状态已变化", "Wiki.DraftStateConflict", "error.wiki.draft_state_conflict");
        }
        await AddReviewEventAsync(document, draft, WikiDocumentReviewActions.Withdraw, userId, userName,
            null, expectedDraftVersion + 1, now);
        document.ActiveDraftId = null;
        draft = await _wikiDraftRepository!.QueryByIdAsync(draft.Id) ?? throw AuthorNotFound();
        return BuildDraftDetail(document, draft, role);
    }

    public async Task<IReadOnlyList<WikiDocumentCollaboratorVo>> AuthorGetCollaboratorsAsync(
        long documentId,
        long userId,
        bool isSystemOrAdmin = false)
    {
        EnsureAuthoringAvailable();
        var document = await _wikiDocumentRepository.QueryByIdAsync(documentId);
        if (document == null || await ResolveAuthorRoleAsync(document, userId, isSystemOrAdmin) == null)
        {
            throw AuthorNotFound();
        }
        var collaborators = await _wikiCollaboratorRepository!.QueryAsync(item =>
            item.DocumentId == documentId && !item.IsDeleted &&
            (item.InviteState == (int)WikiDocumentCollaboratorState.Pending ||
             item.InviteState == (int)WikiDocumentCollaboratorState.Accepted));
        var result = new List<WikiDocumentCollaboratorVo>();
        foreach (var collaborator in collaborators.OrderBy(item => item.InvitedAt))
        {
            var user = await _userRepository!.QueryByIdAsync(collaborator.UserId);
            result.Add(MapCollaborator(collaborator, user));
        }
        return result;
    }

    [UseTran]
    public async Task<WikiDocumentCollaboratorVo> AuthorInviteCollaboratorAsync(
        long documentId,
        InviteWikiCollaboratorDto dto,
        long userId,
        string userName,
        long tenantId,
        bool isSystemOrAdmin = false)
    {
        EnsureAuthoringAvailable();
        var document = await RequireDocumentAsync(documentId, tenantId);
        var role = await ResolveAuthorRoleAsync(document, userId, isSystemOrAdmin);
        if (role is not ("Owner" or "Administrator"))
        {
            throw AuthorNotFound();
        }
        var targetPublicId = NormalizeRequired(dto.UserPublicId, nameof(dto.UserPublicId));
        var target = await _userRepository!.QueryFirstAsync(candidate =>
            candidate.PublicId == targetPublicId && candidate.TenantId == tenantId && candidate.IsEnable && !candidate.IsDeleted)
            ?? throw AuthorNotFound();
        if (target.Id == document.OwnerUserId)
        {
            throw Conflict("所有者不能成为协作者", "Wiki.CollaboratorAlreadyExists", "error.wiki.collaborator_exists");
        }
        var now = DateTime.UtcNow;
        var existing = await _wikiCollaboratorRepository!.QueryFirstAsync(item =>
            item.DocumentId == documentId && item.UserId == target.Id && !item.IsDeleted);
        if (existing is { InviteState: (int)WikiDocumentCollaboratorState.Pending or (int)WikiDocumentCollaboratorState.Accepted })
        {
            return MapCollaborator(existing, target);
        }
        var activeCount = await _wikiCollaboratorRepository.QueryCountAsync(item =>
            item.DocumentId == documentId && !item.IsDeleted &&
            (item.InviteState == (int)WikiDocumentCollaboratorState.Pending ||
             item.InviteState == (int)WikiDocumentCollaboratorState.Accepted));
        if (activeCount >= AuthoringOptions.MaxCollaboratorsPerDocument)
        {
            throw Conflict("协作者数量已达到上限", "Wiki.CollaboratorLimitReached", "error.wiki.collaborator_limit");
        }
        var existingState = existing?.InviteState;
        var collaborator = existing ?? new WikiDocumentCollaborator
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = tenantId,
            DocumentId = documentId,
            UserId = target.Id,
            Role = (int)WikiDocumentCollaboratorRole.Editor,
            CreateId = userId,
            CreateBy = ResolveOperatorName(userName),
            CreateTime = now
        };
        collaborator.InviteState = (int)WikiDocumentCollaboratorState.Pending;
        collaborator.InvitedBy = userId;
        collaborator.InvitedAt = now;
        collaborator.RespondedAt = null;
        collaborator.RevokedAt = null;
        collaborator.RevokedBy = null;
        if (existing == null)
        {
            if (!await _wikiDocumentRepository.TryAddCollaboratorAsync(collaborator))
            {
                var concurrent = await _wikiCollaboratorRepository.QueryFirstAsync(item =>
                    item.DocumentId == documentId && item.UserId == target.Id && !item.IsDeleted);
                if (concurrent is { InviteState: (int)WikiDocumentCollaboratorState.Pending or (int)WikiDocumentCollaboratorState.Accepted })
                {
                    return MapCollaborator(concurrent, target);
                }

                throw Conflict("邀请状态已变化", "Wiki.DraftStateConflict", "error.wiki.draft_state_conflict");
            }
        }
        else
        {
            var affected = await _wikiDocumentRepository.TransitionCollaboratorAsync(
                new WikiCollaboratorTransitionCommand(
                    collaborator.Id,
                    tenantId,
                    existingState!.Value,
                    (int)WikiDocumentCollaboratorState.Pending,
                    userId,
                    ResolveOperatorName(userName),
                    now));
            if (affected != 1)
            {
                var concurrent = await _wikiCollaboratorRepository.QueryByIdAsync(collaborator.Id);
                if (concurrent is { InviteState: (int)WikiDocumentCollaboratorState.Pending or (int)WikiDocumentCollaboratorState.Accepted })
                {
                    return MapCollaborator(concurrent, target);
                }

                throw Conflict("邀请状态已变化", "Wiki.DraftStateConflict", "error.wiki.draft_state_conflict");
            }
        }
        await QueueWikiNotificationAsync(
            NotificationType.WikiCollaboratorInvited,
            target.Id,
            document,
            document.ActiveDraftId,
            userId,
            ResolveOperatorName(userName),
            "Invite",
            collaborator.InvitedAt,
            $"invite:{collaborator.Id}:{collaborator.InvitedAt.Ticks}");
        return MapCollaborator(collaborator, target);
    }

    public async Task<WikiDocumentCollaboratorVo> AuthorRespondInvitationAsync(
        long collaboratorId,
        RespondWikiCollaboratorInvitationDto dto,
        long userId,
        string userName,
        long tenantId)
    {
        EnsureAuthoringAvailable();
        var collaborator = await _wikiCollaboratorRepository!.QueryByIdAsync(collaboratorId);
        if (collaborator == null || collaborator.TenantId != tenantId || collaborator.UserId != userId || collaborator.IsDeleted)
        {
            throw AuthorNotFound();
        }
        var targetState = dto.Accept
            ? WikiDocumentCollaboratorState.Accepted
            : WikiDocumentCollaboratorState.Declined;
        if (collaborator.InviteState != (int)WikiDocumentCollaboratorState.Pending)
        {
            if (collaborator.InviteState != (int)targetState)
            {
                throw Conflict("邀请状态已变化", "Wiki.DraftStateConflict", "error.wiki.draft_state_conflict");
            }
        }
        else
        {
            var now = DateTime.UtcNow;
            var affected = await _wikiDocumentRepository.TransitionCollaboratorAsync(
                new WikiCollaboratorTransitionCommand(
                    collaborator.Id,
                    tenantId,
                    (int)WikiDocumentCollaboratorState.Pending,
                    (int)targetState,
                    userId,
                    ResolveOperatorName(userName),
                    now));
            if (affected != 1)
            {
                collaborator = await _wikiCollaboratorRepository.QueryByIdAsync(collaborator.Id)
                    ?? throw AuthorNotFound();
                if (collaborator.InviteState != (int)targetState)
                {
                    throw Conflict("邀请状态已变化", "Wiki.DraftStateConflict", "error.wiki.draft_state_conflict");
                }
            }
            else
            {
                collaborator.InviteState = (int)targetState;
                collaborator.RespondedAt = now;
            }
        }
        var user = await _userRepository!.QueryByIdAsync(userId);
        return MapCollaborator(collaborator, user);
    }

    public async Task<bool> AuthorRemoveCollaboratorAsync(
        long collaboratorId,
        long userId,
        string userName,
        long tenantId,
        bool isSystemOrAdmin = false)
    {
        EnsureAuthoringAvailable();
        var collaborator = await _wikiCollaboratorRepository!.QueryByIdAsync(collaboratorId);
        if (collaborator == null || collaborator.TenantId != tenantId || collaborator.IsDeleted)
        {
            return true;
        }
        var document = await RequireDocumentAsync(collaborator.DocumentId, tenantId);
        var role = await ResolveAuthorRoleAsync(document, userId, isSystemOrAdmin);
        if (role is not ("Owner" or "Administrator"))
        {
            throw AuthorNotFound();
        }
        if (collaborator.InviteState == (int)WikiDocumentCollaboratorState.Revoked)
        {
            return true;
        }
        var affected = await _wikiDocumentRepository.TransitionCollaboratorAsync(
            new WikiCollaboratorTransitionCommand(
                collaborator.Id,
                tenantId,
                collaborator.InviteState,
                (int)WikiDocumentCollaboratorState.Revoked,
                userId,
                ResolveOperatorName(userName),
                DateTime.UtcNow));
        if (affected == 1)
        {
            return true;
        }

        var concurrent = await _wikiCollaboratorRepository.QueryByIdAsync(collaborator.Id);
        if (concurrent?.InviteState == (int)WikiDocumentCollaboratorState.Revoked)
        {
            return true;
        }

        throw Conflict("邀请状态已变化", "Wiki.DraftStateConflict", "error.wiki.draft_state_conflict");
    }

    public async Task<PageModel<WikiReviewQueueItemVo>> AdminGetReviewQueueAsync(int pageIndex, int pageSize)
    {
        EnsureAuthoringAvailable();
        pageIndex = Math.Max(1, pageIndex);
        pageSize = pageSize is > 0 and <= 100 ? pageSize : 20;
        var (drafts, total) = await _wikiDraftRepository!.QueryPageAsync(
            draft => draft.ReviewState == (int)WikiDocumentDraftState.Submitted && !draft.IsDeleted,
            pageIndex, pageSize, draft => draft.SubmittedAt!, OrderByType.Asc);
        var result = new List<WikiReviewQueueItemVo>();
        foreach (var draft in drafts)
        {
            var document = await _wikiDocumentRepository.QueryByIdAsync(draft.DocumentId);
            if (document != null)
            {
                var owner = document.OwnerUserId.HasValue
                    ? await _userRepository!.QueryByIdAsync(document.OwnerUserId.Value)
                    : null;
                result.Add(MapReviewQueue(document, draft, owner));
            }
        }
        return new PageModel<WikiReviewQueueItemVo>
        {
            Page = pageIndex,
            PageSize = pageSize,
            DataCount = total,
            PageCount = (int)Math.Ceiling(total / (double)pageSize),
            Data = result
        };
    }

    public async Task<WikiAuthorDraftDetailVo?> AdminGetDraftByIdAsync(long draftId)
    {
        EnsureAuthoringAvailable();
        var draft = await _wikiDraftRepository!.QueryByIdAsync(draftId);
        if (draft == null || draft.IsDeleted)
        {
            return null;
        }
        var document = await _wikiDocumentRepository.QueryByIdAsync(draft.DocumentId);
        return document == null
            ? null
            : await BuildDraftDetailWithEvidenceAsync(document, draft, "Reviewer");
    }

    [UseTran]
    public async Task<WikiAuthorDraftDetailVo> AdminReviewDraftAsync(
        long draftId,
        ReviewWikiDraftDto dto,
        long reviewerId,
        string reviewerName,
        long tenantId)
    {
        EnsureAuthoringAvailable();
        var draft = await _wikiDraftRepository!.QueryByIdAsync(draftId);
        if (draft == null || draft.TenantId != tenantId || draft.IsDeleted)
        {
            throw AuthorNotFound();
        }
        var document = await RequireDocumentAsync(draft.DocumentId, tenantId);
        var action = NormalizeRequired(dto.Action, nameof(dto.Action));
        var now = DateTime.UtcNow;
        var reviewerNameValue = ResolveOperatorName(reviewerName);
        var comment = NormalizeOptional(dto.Comment);
        int targetState;
        string eventAction;
        if (string.Equals(action, WikiDocumentReviewActions.RequestChanges, StringComparison.OrdinalIgnoreCase))
        {
            EnsureReviewComment(comment);
            targetState = (int)WikiDocumentDraftState.ChangesRequested;
            eventAction = WikiDocumentReviewActions.RequestChanges;
            if (draft.ReviewState == targetState && draft.DraftVersion == dto.ExpectedDraftVersion + 1)
            {
                return BuildDraftDetail(document, draft, "Reviewer");
            }
        }
        else if (string.Equals(action, WikiDocumentReviewActions.Reject, StringComparison.OrdinalIgnoreCase))
        {
            EnsureReviewComment(comment);
            targetState = (int)WikiDocumentDraftState.Rejected;
            eventAction = WikiDocumentReviewActions.Reject;
            if (draft.ReviewState == targetState && draft.DraftVersion == dto.ExpectedDraftVersion + 1)
            {
                return BuildDraftDetail(document, draft, "Reviewer");
            }
        }
        else if (string.Equals(action, WikiDocumentReviewActions.Apply, StringComparison.OrdinalIgnoreCase))
        {
            targetState = (int)WikiDocumentDraftState.Applied;
            eventAction = WikiDocumentReviewActions.Apply;
            if (draft.ReviewState == targetState &&
                draft.DraftVersion == dto.ExpectedDraftVersion + 1 &&
                document.Version == dto.ExpectedDocumentVersion + 1)
            {
                return BuildDraftDetail(document, draft, "Reviewer");
            }
            await ValidateParentDocumentAsync(dto.FinalParentId, document.Id);
            var normalizedSlug = await EnsureUniqueSlugForUpdateAsync(draft.Slug, draft.Title, document.Id);
            draft.Slug = normalizedSlug;
            var applied = await _wikiDocumentRepository.ApplyDraftToDocumentAsync(new WikiDraftApplyCommand(
                document.Id, tenantId, dto.ExpectedDocumentVersion, draft, dto.FinalParentId,
                reviewerId, reviewerNameValue, now));
            if (applied != 1)
            {
                var currentDraft = await _wikiDraftRepository.QueryByIdAsync(draft.Id);
                var currentDocument = await _wikiDocumentRepository.QueryByIdAsync(document.Id);
                if (currentDraft?.ReviewState == (int)WikiDocumentDraftState.Applied &&
                    currentDraft.DraftVersion == dto.ExpectedDraftVersion + 1 &&
                    currentDocument?.Version == dto.ExpectedDocumentVersion + 1)
                {
                    return BuildDraftDetail(currentDocument, currentDraft, "Reviewer");
                }
                throw Conflict("正式文档版本已变化", "Wiki.DocumentVersionConflict", "error.wiki.document_version_conflict");
            }
            document.Title = draft.Title;
            document.Slug = draft.Slug;
            document.Summary = draft.Summary;
            document.MarkdownContent = draft.MarkdownContent;
            document.CoverAttachmentId = draft.CoverAttachmentId;
            document.ParentId = dto.FinalParentId;
            document.Version = dto.ExpectedDocumentVersion + 1;
            await AddRevisionAsync(document, draft.ChangeSummary, document.SourceType, reviewerId, reviewerNameValue);
        }
        else
        {
            throw new BusinessException("审核动作无效", 400, "Wiki.InvalidReviewAction", "error.wiki.invalid_review_action");
        }

        var transitioned = await _wikiDocumentRepository.TransitionDraftAsync(new WikiDraftTransitionCommand(
            draft.Id, tenantId, dto.ExpectedDraftVersion, [(int)WikiDocumentDraftState.Submitted],
            targetState, draft.ChangeSummary, comment, reviewerId, reviewerNameValue, now));
        if (transitioned != 1)
        {
            var currentDraft = await _wikiDraftRepository.QueryByIdAsync(draft.Id);
            if (currentDraft?.ReviewState == targetState &&
                currentDraft.DraftVersion == dto.ExpectedDraftVersion + 1)
            {
                return BuildDraftDetail(document, currentDraft, "Reviewer");
            }
            if (currentDraft?.ReviewState != (int)WikiDocumentDraftState.Submitted)
            {
                throw Conflict("草稿审核状态已变化", "Wiki.DraftStateConflict", "error.wiki.draft_state_conflict");
            }
            throw Conflict("草稿版本或审核状态已变化", "Wiki.DraftVersionConflict", "error.wiki.draft_version_conflict");
        }
        if (targetState is (int)WikiDocumentDraftState.Applied or (int)WikiDocumentDraftState.Rejected &&
            await _wikiDocumentRepository.SetActiveDraftAsync(
                document.Id, tenantId, draft.Id, null, reviewerId, reviewerNameValue, now) != 1)
        {
            throw Conflict("活跃草稿状态已变化", "Wiki.DraftStateConflict", "error.wiki.draft_state_conflict");
        }
        await AddReviewEventAsync(document, draft, eventAction, reviewerId, reviewerNameValue, comment,
            dto.ExpectedDraftVersion + 1, now);
        if (document.OwnerUserId.HasValue)
        {
            await QueueWikiNotificationAsync(
                NotificationType.WikiReviewUpdated,
                document.OwnerUserId.Value,
                document,
                draft.Id,
                reviewerId,
                reviewerNameValue,
                eventAction,
                now,
                $"review:{draft.Id}:{dto.ExpectedDraftVersion + 1}:{eventAction}");
        }
        draft = await _wikiDraftRepository.QueryByIdAsync(draft.Id) ?? throw AuthorNotFound();
        document.ActiveDraftId = targetState == (int)WikiDocumentDraftState.ChangesRequested ? draft.Id : null;
        return BuildDraftDetail(document, draft, "Reviewer");
    }

    private WikiAuthoringOptions AuthoringOptions => _documentOptions.Authoring;

    private void EnsureAuthoringAvailable()
    {
        if (_wikiDraftRepository == null || _wikiCollaboratorRepository == null ||
            _wikiReviewEventRepository == null || _userRepository == null)
        {
            throw new InvalidOperationException("Wiki authoring repositories are not registered.");
        }
    }

    private async Task EnsureCanOpenContributionAsync(long userId)
    {
        if (_contentModerationService == null)
        {
            return;
        }
        var permission = await _contentModerationService.GetPublishPermissionAsync(userId);
        if (!permission.VoCanPublish)
        {
            throw new BusinessException(permission.VoDenyReason ?? "当前状态不能提交文档贡献", 403,
                "Wiki.AuthorPublishingBlocked", "error.wiki.author_publishing_blocked");
        }
    }

    private async Task EnsureOwnedDraftCapacityAsync(long ownerUserId)
    {
        var activeCount = await _wikiDocumentRepository.QueryCountAsync(document =>
            document.OwnerUserId == ownerUserId && document.ActiveDraftId != null && !document.IsDeleted);
        if (activeCount >= AuthoringOptions.MaxActiveOwnedDrafts)
        {
            throw Conflict("活跃草稿数量已达到上限", "Wiki.ActiveDraftLimitReached", "error.wiki.active_draft_limit");
        }
    }

    private void ValidateDraftPayload(string? markdownContent)
    {
        if (string.IsNullOrWhiteSpace(markdownContent))
        {
            throw new BusinessException("Markdown 内容不能为空", 400, "Wiki.ValidationFailed", "error.wiki.validation_failed");
        }
        if (Encoding.UTF8.GetByteCount(markdownContent) > AuthoringOptions.MaxMarkdownUtf8Bytes)
        {
            throw new BusinessException("Markdown 内容超过大小限制", 400,
                "Wiki.MarkdownTooLarge", "error.wiki.markdown_too_large");
        }
    }

    private async Task<WikiDocument> RequireDocumentAsync(long documentId, long tenantId)
    {
        var document = await _wikiDocumentRepository.QueryByIdAsync(documentId);
        if (document == null || document.TenantId != tenantId || document.IsDeleted)
        {
            throw AuthorNotFound();
        }
        return document;
    }

    private static void EnsureCustomDocument(WikiDocument document)
    {
        if (string.Equals(document.SourceType, "BuiltIn", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(document.SourceType, "LocalMirror", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("固定文档为只读内容", 409,
                "Wiki.ReadOnlySource", "error.wiki.read_only_source");
        }
    }

    private async Task<(WikiDocument document, WikiDocumentDraft draft, string role)> RequireDraftAccessAsync(
        long draftId,
        long userId,
        long tenantId,
        bool isSystemOrAdmin)
    {
        var draft = await _wikiDraftRepository!.QueryByIdAsync(draftId);
        if (draft == null || draft.TenantId != tenantId || draft.IsDeleted)
        {
            throw AuthorNotFound();
        }
        var document = await RequireDocumentAsync(draft.DocumentId, tenantId);
        if (document.ActiveDraftId != draft.Id && IsActiveDraftState(draft.ReviewState))
        {
            throw Conflict("活跃草稿状态不一致", "Wiki.DraftStateConflict", "error.wiki.draft_state_conflict");
        }
        var role = await ResolveAuthorRoleAsync(document, userId, isSystemOrAdmin);
        if (role == null)
        {
            throw AuthorNotFound();
        }
        return (document, draft, role);
    }

    private async Task<string?> ResolveAuthorRoleAsync(WikiDocument document, long userId, bool isSystemOrAdmin)
    {
        if (isSystemOrAdmin)
        {
            return "Administrator";
        }
        if (document.OwnerUserId == userId)
        {
            return "Owner";
        }
        var collaborator = await _wikiCollaboratorRepository!.QueryFirstAsync(item =>
            item.DocumentId == document.Id && item.UserId == userId && !item.IsDeleted &&
            item.InviteState == (int)WikiDocumentCollaboratorState.Accepted);
        return collaborator == null ? null : "Editor";
    }

    private static bool IsEditableDraftState(int state) =>
        state is (int)WikiDocumentDraftState.Editing or (int)WikiDocumentDraftState.ChangesRequested;

    private static bool IsActiveDraftState(int state) =>
        state is (int)WikiDocumentDraftState.Editing or
            (int)WikiDocumentDraftState.Submitted or
            (int)WikiDocumentDraftState.ChangesRequested;

    private static WikiAuthorDraftDetailVo BuildDraftDetail(
        WikiDocument document,
        WikiDocumentDraft draft,
        string role)
    {
        var editable = IsEditableDraftState(draft.ReviewState) && role is not "Reviewer";
        return new WikiAuthorDraftDetailVo
        {
            VoDocumentId = document.Id,
            VoDraftId = draft.Id,
            VoOwnerUserId = document.OwnerUserId,
            VoTitle = draft.Title,
            VoSlug = draft.Slug,
            VoSummary = draft.Summary,
            VoMarkdownContent = draft.MarkdownContent,
            VoCoverAttachmentId = draft.CoverAttachmentId,
            VoProposedParentId = draft.ProposedParentId,
            VoDocumentVersion = document.Version,
            VoBaseDocumentVersion = draft.BaseDocumentVersion,
            VoDraftVersion = draft.DraftVersion,
            VoReviewState = draft.ReviewState,
            VoDocumentStatus = document.Status,
            VoAuthorRole = role,
            VoCanEdit = editable,
            VoCanSubmit = editable && (role == "Owner" || role == "Administrator"),
            VoCanManageCollaborators = role is "Owner" or "Administrator",
            VoReadOnlyReason = editable ? null : ResolveReadOnlyReason(draft.ReviewState),
            VoChangeSummary = draft.ChangeSummary,
            VoReviewComment = draft.ReviewComment,
            VoSubmittedAt = draft.SubmittedAt
        };
    }

    private async Task<WikiAuthorDraftDetailVo> BuildDraftDetailWithEvidenceAsync(
        WikiDocument document,
        WikiDocumentDraft draft,
        string role)
    {
        var detail = BuildDraftDetail(document, draft, role);
        var owner = document.OwnerUserId.HasValue
            ? await _userRepository!.QueryByIdAsync(document.OwnerUserId.Value)
            : null;
        detail.VoOwnerUserPublicId = owner?.PublicId ?? string.Empty;
        detail.VoOwnerUserName = owner?.UserName ?? string.Empty;

        var collaborators = await _wikiCollaboratorRepository!.QueryAsync(item =>
            item.DocumentId == document.Id &&
            !item.IsDeleted &&
            (item.InviteState == (int)WikiDocumentCollaboratorState.Pending ||
             item.InviteState == (int)WikiDocumentCollaboratorState.Accepted));
        var collaboratorUserIds = collaborators.Select(item => item.UserId).Distinct().ToList();
        List<User> collaboratorUsers = collaboratorUserIds.Count == 0
            ? []
            : await _userRepository!.QueryAsync(user => collaboratorUserIds.Contains(user.Id) && !user.IsDeleted);
        var collaboratorUserMap = collaboratorUsers.ToDictionary(user => user.Id);
        detail.VoCollaborators = collaborators
            .OrderBy(item => item.InvitedAt)
            .Select(item => MapCollaborator(
                item,
                collaboratorUserMap.GetValueOrDefault(item.UserId)))
            .ToList();

        var reviewEvents = await _wikiReviewEventRepository!.QueryAsync(item =>
            item.DocumentId == document.Id && item.DraftId == draft.Id);
        detail.VoReviewEvents = reviewEvents
            .OrderBy(item => item.CreateTime)
            .ThenBy(item => item.Id)
            .Select(MapReviewEvent)
            .ToList();
        return detail;
    }

    private static string? ResolveReadOnlyReason(int state) => state switch
    {
        (int)WikiDocumentDraftState.Submitted => "Submitted",
        (int)WikiDocumentDraftState.Applied => "Applied",
        (int)WikiDocumentDraftState.Rejected => "Rejected",
        (int)WikiDocumentDraftState.Withdrawn => "Withdrawn",
        _ => null
    };

    private static WikiDocumentCollaboratorVo MapCollaborator(WikiDocumentCollaborator collaborator, User? user) => new()
    {
        VoId = collaborator.Id,
        VoDocumentId = collaborator.DocumentId,
        VoUserPublicId = user?.PublicId ?? string.Empty,
        VoUserName = user?.UserName ?? string.Empty,
        VoRole = collaborator.Role,
        VoInviteState = collaborator.InviteState,
        VoInvitedAt = collaborator.InvitedAt,
        VoRespondedAt = collaborator.RespondedAt
    };

    private static WikiReviewQueueItemVo MapReviewQueue(
        WikiDocument document,
        WikiDocumentDraft draft,
        User? owner) => new()
    {
        VoDocumentId = document.Id,
        VoDraftId = draft.Id,
        VoOwnerUserId = document.OwnerUserId,
        VoOwnerUserPublicId = owner?.PublicId ?? string.Empty,
        VoOwnerUserName = owner?.UserName ?? string.Empty,
        VoTitle = draft.Title,
        VoSlug = draft.Slug,
        VoDocumentVersion = document.Version,
        VoBaseDocumentVersion = draft.BaseDocumentVersion,
        VoDraftVersion = draft.DraftVersion,
        VoReviewState = draft.ReviewState,
        VoChangeSummary = draft.ChangeSummary,
        VoSubmittedAt = draft.SubmittedAt
    };

    private static WikiDocumentReviewEventVo MapReviewEvent(WikiDocumentReviewEvent reviewEvent) => new()
    {
        VoId = reviewEvent.Id,
        VoAction = reviewEvent.Action,
        VoActorName = reviewEvent.ActorName,
        VoComment = reviewEvent.Comment,
        VoDocumentVersion = reviewEvent.DocumentVersion,
        VoDraftVersion = reviewEvent.DraftVersion,
        VoCreateTime = reviewEvent.CreateTime
    };

    private async Task AddReviewEventAsync(
        WikiDocument document,
        WikiDocumentDraft draft,
        string action,
        long actorId,
        string actorName,
        string? comment,
        int draftVersion,
        DateTime now)
    {
        await _wikiReviewEventRepository!.AddAsync(new WikiDocumentReviewEvent
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = document.TenantId,
            DocumentId = document.Id,
            DraftId = draft.Id,
            Action = action,
            ActorUserId = actorId,
            ActorName = ResolveOperatorName(actorName),
            Comment = comment,
            DocumentVersion = document.Version,
            DraftVersion = draftVersion,
            CreateTime = now
        });
    }

    private async Task QueueWikiNotificationAsync(
        string notificationType,
        long receiverUserId,
        WikiDocument document,
        long? draftId,
        long actorId,
        string actorName,
        string action,
        DateTime occurredAtUtc,
        string eventKey)
    {
        if (_reliableOutboxService == null || receiverUserId <= 0)
        {
            return;
        }
        var notificationId = SnowFlakeSingle.Instance.NextId();
        var notification = new CreateNotificationDto
        {
            NotificationId = notificationId,
            BusinessKey = $"notification:wiki:{eventKey}:receiver:{receiverUserId}",
            Type = notificationType,
            Title = notificationType == NotificationType.WikiCollaboratorInvited ? "文档协作邀请" : "文档审核状态更新",
            Content = document.Title,
            Priority = (int)NotificationPriority.Normal,
            BusinessType = BusinessType.Wiki,
            BusinessId = document.Id,
            TriggerId = actorId,
            TriggerName = actorName,
            ReceiverUserIds = [receiverUserId],
            TenantId = document.TenantId,
            TemplateArguments = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["actorName"] = actorName,
                ["targetTitle"] = document.Title,
                ["reviewAction"] = action
            },
            TargetKind = NotificationTargetKind.DocsAuthorDraft,
            Target = new NotificationTargetData
            {
                DocumentId = document.Id,
                DraftId = draftId
            },
            OccurredAtUtc = occurredAtUtc
        };
        await _reliableOutboxService.AddAsync(
            ReliableOutboxSources.Main,
            document.TenantId,
            ReliableTaskTypes.NotificationRequested,
            $"task:notification:wiki:{eventKey}:receiver:{receiverUserId}",
            "WikiDocument",
            document.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new NotificationRequestedTaskPayload(notification),
            occurredAtUtc);
    }

    private static void EnsureReviewComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new BusinessException("请求修改或驳回时必须填写审核意见", 400,
                "Wiki.ReviewCommentRequired", "error.wiki.review_comment_required");
        }
    }

    private static BusinessException AuthorNotFound() => new(
        "文档或草稿不存在", 404, "Wiki.DraftNotFound", "error.wiki.draft_not_found");

    private static BusinessException Conflict(string message, string code, string key) =>
        new(message, 409, code, key);
}
