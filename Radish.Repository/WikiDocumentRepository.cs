using System.Linq.Expressions;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>Wiki 文档仓储</summary>
public class WikiDocumentRepository : BaseRepository<WikiDocument>, IWikiDocumentRepository
{
    private readonly IUnitOfWorkManage _unitOfWorkManage;

    public WikiDocumentRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
        _unitOfWorkManage = unitOfWorkManage;
    }

    public async Task<int> SaveDraftAsync(WikiDraftSaveCommand command)
    {
        var editing = (int)Radish.Shared.CustomEnum.WikiDocumentDraftState.Editing;
        var changesRequested = (int)Radish.Shared.CustomEnum.WikiDocumentDraftState.ChangesRequested;
        return await ExecuteDbOperationAsync(() => DbProtectedClient.Updateable<WikiDocumentDraft>()
            .SetColumns(draft => new WikiDocumentDraft
            {
                Title = command.Title,
                Slug = command.Slug,
                Summary = command.Summary,
                MarkdownContent = command.MarkdownContent,
                CoverAttachmentId = command.CoverAttachmentId,
                ProposedParentId = command.ProposedParentId,
                ChangeSummary = command.ChangeSummary,
                DraftVersion = command.ExpectedDraftVersion + 1,
                ModifyTime = command.NowUtc,
                ModifyBy = command.OperatorName,
                ModifyId = command.OperatorId
            })
            .Where(draft =>
                draft.Id == command.DraftId &&
                draft.TenantId == command.TenantId &&
                draft.DraftVersion == command.ExpectedDraftVersion &&
                !draft.IsDeleted &&
                (draft.ReviewState == editing || draft.ReviewState == changesRequested))
            .ExecuteCommandAsync());
    }

    public async Task<int> TransitionDraftAsync(WikiDraftTransitionCommand command)
    {
        var sourceStates = command.AllowedSourceStates.ToArray();
        var submitted = command.TargetState == (int)Radish.Shared.CustomEnum.WikiDocumentDraftState.Submitted;
        var reviewed = command.TargetState is
            (int)Radish.Shared.CustomEnum.WikiDocumentDraftState.ChangesRequested or
            (int)Radish.Shared.CustomEnum.WikiDocumentDraftState.Applied or
            (int)Radish.Shared.CustomEnum.WikiDocumentDraftState.Rejected;
        return await ExecuteDbOperationAsync(() => DbProtectedClient.Updateable<WikiDocumentDraft>()
            .SetColumns(draft => new WikiDocumentDraft
            {
                ReviewState = command.TargetState,
                DraftVersion = command.ExpectedDraftVersion + 1,
                ChangeSummary = command.ChangeSummary,
                SubmittedAt = submitted ? command.NowUtc : draft.SubmittedAt,
                SubmittedBy = submitted ? command.OperatorId : draft.SubmittedBy,
                ReviewedAt = reviewed ? command.NowUtc : draft.ReviewedAt,
                ReviewedBy = reviewed ? command.OperatorId : draft.ReviewedBy,
                ReviewComment = reviewed ? command.ReviewComment : draft.ReviewComment,
                ModifyTime = command.NowUtc,
                ModifyBy = command.OperatorName,
                ModifyId = command.OperatorId
            })
            .Where(draft =>
                draft.Id == command.DraftId &&
                draft.TenantId == command.TenantId &&
                draft.DraftVersion == command.ExpectedDraftVersion &&
                !draft.IsDeleted &&
                sourceStates.Contains(draft.ReviewState))
            .ExecuteCommandAsync());
    }

    public Task<bool> TryAddCollaboratorAsync(WikiDocumentCollaborator collaborator)
    {
        ArgumentNullException.ThrowIfNull(collaborator);
        return ExecuteDbOperationAsync(() => TryAddCollaboratorCoreAsync(collaborator));
    }

    private async Task<bool> TryAddCollaboratorCoreAsync(WikiDocumentCollaborator collaborator)
    {
        try
        {
            await _unitOfWorkManage.ExecuteInSavepointAsync(() =>
                DbProtectedClient.Insertable(collaborator).ExecuteCommandAsync());
            return true;
        }
        catch
        {
            var existing = await DbProtectedClient.Queryable<WikiDocumentCollaborator>()
                .Where(candidate =>
                    candidate.TenantId == collaborator.TenantId &&
                    candidate.DocumentId == collaborator.DocumentId &&
                    candidate.UserId == collaborator.UserId &&
                    !candidate.IsDeleted)
                .FirstAsync();
            if (existing != null)
            {
                return false;
            }

            throw;
        }
    }

    public async Task<int> TransitionCollaboratorAsync(WikiCollaboratorTransitionCommand command)
    {
        var pending = command.TargetState ==
            (int)Radish.Shared.CustomEnum.WikiDocumentCollaboratorState.Pending;
        var responded = command.TargetState is
            (int)Radish.Shared.CustomEnum.WikiDocumentCollaboratorState.Accepted or
            (int)Radish.Shared.CustomEnum.WikiDocumentCollaboratorState.Declined;
        var revoked = command.TargetState ==
            (int)Radish.Shared.CustomEnum.WikiDocumentCollaboratorState.Revoked;
        return await ExecuteDbOperationAsync(() => DbProtectedClient.Updateable<WikiDocumentCollaborator>()
            .SetColumns(collaborator => new WikiDocumentCollaborator
            {
                InviteState = command.TargetState,
                InvitedBy = pending ? command.OperatorId : collaborator.InvitedBy,
                InvitedAt = pending ? command.NowUtc : collaborator.InvitedAt,
                RespondedAt = pending ? null : responded ? command.NowUtc : collaborator.RespondedAt,
                RevokedBy = pending ? null : revoked ? command.OperatorId : collaborator.RevokedBy,
                RevokedAt = pending ? null : revoked ? command.NowUtc : collaborator.RevokedAt,
                ModifyTime = command.NowUtc,
                ModifyBy = command.OperatorName,
                ModifyId = command.OperatorId
            })
            .Where(collaborator =>
                collaborator.Id == command.CollaboratorId &&
                collaborator.TenantId == command.TenantId &&
                collaborator.InviteState == command.ExpectedState &&
                !collaborator.IsDeleted)
            .ExecuteCommandAsync());
    }

    public async Task<int> SetActiveDraftAsync(
        long documentId,
        long tenantId,
        long? expectedDraftId,
        long? targetDraftId,
        long operatorId,
        string operatorName,
        DateTime nowUtc)
    {
        return await ExecuteDbOperationAsync(() => DbProtectedClient.Updateable<WikiDocument>()
            .SetColumns(document => new WikiDocument
            {
                ActiveDraftId = targetDraftId,
                ModifyTime = nowUtc,
                ModifyBy = operatorName,
                ModifyId = operatorId
            })
            .Where(document =>
                document.Id == documentId &&
                document.TenantId == tenantId &&
                document.ActiveDraftId == expectedDraftId &&
                !document.IsDeleted)
            .ExecuteCommandAsync());
    }

    public async Task<int> ApplyDraftToDocumentAsync(WikiDraftApplyCommand command)
    {
        return await ExecuteDbOperationAsync(() => DbProtectedClient.Updateable<WikiDocument>()
            .SetColumns(document => new WikiDocument
            {
                Title = command.Draft.Title,
                Slug = command.Draft.Slug,
                Summary = command.Draft.Summary,
                MarkdownContent = command.Draft.MarkdownContent,
                CoverAttachmentId = command.Draft.CoverAttachmentId,
                ParentId = command.FinalParentId,
                Version = command.ExpectedDocumentVersion + 1,
                ModifyTime = command.NowUtc,
                ModifyBy = command.OperatorName,
                ModifyId = command.OperatorId
            })
            .Where(document =>
                document.Id == command.DocumentId &&
                document.TenantId == command.TenantId &&
                document.Version == command.ExpectedDocumentVersion &&
                document.ActiveDraftId == command.Draft.Id &&
                !document.IsDeleted)
            .ExecuteCommandAsync());
    }

    public async Task<int> PurgeTerminalDraftPayloadsAsync(DateTime cutoffUtc, int batchSize, DateTime nowUtc)
    {
        if (batchSize <= 0)
        {
            return 0;
        }

        var applied = (int)Radish.Shared.CustomEnum.WikiDocumentDraftState.Applied;
        var rejected = (int)Radish.Shared.CustomEnum.WikiDocumentDraftState.Rejected;
        var withdrawn = (int)Radish.Shared.CustomEnum.WikiDocumentDraftState.Withdrawn;
        var candidateIds = await ExecuteDbOperationAsync(() => DbProtectedClient.Queryable<WikiDocumentDraft>()
            .Where(draft =>
                !draft.IsDeleted &&
                draft.PayloadPurgedAt == null &&
                (draft.ReviewState == applied || draft.ReviewState == rejected || draft.ReviewState == withdrawn) &&
                ((draft.ReviewedAt != null && draft.ReviewedAt <= cutoffUtc) ||
                 (draft.ReviewedAt == null && draft.ModifyTime != null && draft.ModifyTime <= cutoffUtc)))
            .OrderBy(draft => draft.ReviewedAt ?? draft.ModifyTime)
            .OrderBy(draft => draft.Id)
            .Take(batchSize)
            .Select(draft => draft.Id)
            .ToListAsync());
        if (candidateIds.Count == 0)
        {
            return 0;
        }

        return await ExecuteDbOperationAsync(() => DbProtectedClient.Updateable<WikiDocumentDraft>()
            .SetColumns(draft => new WikiDocumentDraft
            {
                MarkdownContent = string.Empty,
                PayloadPurgedAt = nowUtc,
                ModifyTime = nowUtc,
                ModifyBy = "System"
            })
            .Where(draft => candidateIds.Contains(draft.Id) && draft.PayloadPurgedAt == null)
            .ExecuteCommandAsync());
    }

    public async Task<WikiDocument?> QueryByIdIncludingDeletedAsync(long id)
    {
        return await ExecuteDbOperationAsync(
            () => CreateTenantQueryableFor<WikiDocument>(includeDeleted: true).InSingleAsync(id));
    }

    public async Task<(List<WikiDocument> data, int totalCount)> QueryPageIncludingDeletedAsync(
        Expression<Func<WikiDocument, bool>>? whereExpression,
        int pageIndex,
        int pageSize,
        Expression<Func<WikiDocument, object>>? orderByExpression,
        OrderByType orderByType,
        Expression<Func<WikiDocument, object>>? thenByExpression,
        OrderByType thenByType)
    {
        return await ExecuteDbOperationAsync(async () =>
        {
            RefAsync<int> totalCount = 0;
            var query = CreateTenantQueryableFor<WikiDocument>(includeDeleted: true);
            if (whereExpression != null)
            {
                query = query.Where(whereExpression);
            }

            if (orderByExpression != null)
            {
                query = orderByType == OrderByType.Asc
                    ? query.OrderBy(orderByExpression)
                    : query.OrderByDescending(orderByExpression);
            }

            if (thenByExpression != null)
            {
                query = query.OrderBy(thenByExpression, thenByType);
            }

            var data = await query.ToPageListAsync(pageIndex, pageSize, totalCount);
            return (data, totalCount.Value);
        });
    }

    public async Task<List<WikiDocument>> QueryIncludingDeletedAsync(
        Expression<Func<WikiDocument, bool>>? whereExpression,
        Expression<Func<WikiDocument, object>>? orderByExpression,
        OrderByType orderByType)
    {
        return await ExecuteDbOperationAsync(async () =>
        {
            var query = CreateTenantQueryableFor<WikiDocument>(includeDeleted: true);
            if (whereExpression != null)
            {
                query = query.Where(whereExpression);
            }

            if (orderByExpression != null)
            {
                query = orderByType == OrderByType.Asc
                    ? query.OrderBy(orderByExpression)
                    : query.OrderByDescending(orderByExpression);
            }

            return await query.ToListAsync();
        });
    }
}
