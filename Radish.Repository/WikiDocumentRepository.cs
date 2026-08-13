using System.Buffers.Binary;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
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

    public Task<(List<WikiDocument> data, int totalCount)> QueryAuthorPageAsync(
        WikiAuthorDocumentPageQuery query)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var pending = (int)Radish.Shared.CustomEnum.WikiDocumentCollaboratorState.Pending;
            var accepted = (int)Radish.Shared.CustomEnum.WikiDocumentCollaboratorState.Accepted;
            var editing = (int)Radish.Shared.CustomEnum.WikiDocumentDraftState.Editing;
            var changesRequested = (int)Radish.Shared.CustomEnum.WikiDocumentDraftState.ChangesRequested;
            var submitted = (int)Radish.Shared.CustomEnum.WikiDocumentDraftState.Submitted;
            var terminalStates = TerminalDraftStates();
            var source = CreateTenantQueryableFor<WikiDocument>();

            if (query.Scope == Radish.Shared.CustomEnum.WikiAuthorDocumentScope.Owned)
            {
                source = source.Where(document => document.OwnerUserId == query.UserId);
            }
            else
            {
                source = source.Where(document =>
                    (query.Scope == Radish.Shared.CustomEnum.WikiAuthorDocumentScope.All &&
                     document.OwnerUserId == query.UserId) ||
                    SqlFunc.Subqueryable<WikiDocumentCollaborator>()
                        .Where(collaborator =>
                            collaborator.TenantId == document.TenantId &&
                            collaborator.DocumentId == document.Id &&
                            collaborator.UserId == query.UserId &&
                            (collaborator.InviteState == pending || collaborator.InviteState == accepted) &&
                            !collaborator.IsDeleted)
                        .Any());
            }

            if (query.DraftStage == Radish.Shared.CustomEnum.WikiAuthorDraftStage.Editable)
            {
                source = source.Where(document =>
                    document.ActiveDraftId.HasValue &&
                    SqlFunc.Subqueryable<WikiDocumentDraft>()
                        .Where(draft =>
                            draft.TenantId == document.TenantId &&
                            draft.DocumentId == document.Id &&
                            draft.Id == document.ActiveDraftId.Value &&
                            !draft.IsDeleted &&
                            (draft.ReviewState == editing || draft.ReviewState == changesRequested))
                        .Any());
            }
            else if (query.DraftStage == Radish.Shared.CustomEnum.WikiAuthorDraftStage.Submitted)
            {
                source = source.Where(document =>
                    document.ActiveDraftId.HasValue &&
                    SqlFunc.Subqueryable<WikiDocumentDraft>()
                        .Where(draft =>
                            draft.TenantId == document.TenantId &&
                            draft.DocumentId == document.Id &&
                            draft.Id == document.ActiveDraftId.Value &&
                            !draft.IsDeleted &&
                            draft.ReviewState == submitted)
                        .Any());
            }
            else if (query.DraftStage == Radish.Shared.CustomEnum.WikiAuthorDraftStage.Terminal)
            {
                source = source.Where(document =>
                    !document.ActiveDraftId.HasValue &&
                    SqlFunc.Subqueryable<WikiDocumentDraft>()
                        .Where(draft =>
                            draft.TenantId == document.TenantId &&
                            draft.DocumentId == document.Id &&
                            terminalStates.Contains(draft.ReviewState) &&
                            !draft.IsDeleted)
                        .Any());
            }
            else if (query.DraftStage == Radish.Shared.CustomEnum.WikiAuthorDraftStage.None)
            {
                source = source.Where(document =>
                    !document.ActiveDraftId.HasValue &&
                    !SqlFunc.Subqueryable<WikiDocumentDraft>()
                        .Where(draft =>
                            draft.TenantId == document.TenantId &&
                            draft.DocumentId == document.Id &&
                            terminalStates.Contains(draft.ReviewState) &&
                            !draft.IsDeleted)
                        .Any());
            }

            RefAsync<int> totalCount = 0;
            var documents = await source
                .OrderByDescending(document => document.ModifyTime ?? document.CreateTime)
                .OrderByDescending(document => document.Id)
                .ToPageListAsync(query.PageIndex, query.PageSize, totalCount);
            return (documents, totalCount.Value);
        });
    }

    public async Task<WikiDocumentDraft?> QueryLatestTerminalDraftAsync(long documentId)
    {
        if (documentId <= 0)
        {
            return null;
        }

        var terminalStates = TerminalDraftStates();
        return await ExecuteDbOperationAsync(() => CreateTenantQueryableFor<WikiDocumentDraft>()
            .Where(draft =>
                draft.DocumentId == documentId &&
                terminalStates.Contains(draft.ReviewState) &&
                !draft.IsDeleted)
            .OrderByDescending(draft => draft.Id)
            .FirstAsync());
    }

    public async Task<List<WikiTerminalDraftEvidence>> QueryLatestTerminalDraftEvidenceAsync(
        IReadOnlyCollection<long> documentIds)
    {
        var normalizedDocumentIds = documentIds
            .Where(documentId => documentId > 0)
            .Distinct()
            .ToArray();
        if (normalizedDocumentIds.Length == 0)
        {
            return [];
        }

        var terminalStates = TerminalDraftStates();
        return await ExecuteDbOperationAsync(async () =>
        {
            var latestDraftIdentities = await CreateTenantQueryableFor<WikiDocumentDraft>()
                .Where(draft =>
                    normalizedDocumentIds.Contains(draft.DocumentId) &&
                    terminalStates.Contains(draft.ReviewState) &&
                    !draft.IsDeleted)
                .GroupBy(draft => draft.DocumentId)
                .Select(draft => new
                {
                    draft.DocumentId,
                    DraftId = SqlFunc.AggregateMax(draft.Id)
                })
                .ToListAsync();
            var latestDraftIds = latestDraftIdentities
                .Select(identity => identity.DraftId)
                .ToArray();
            if (latestDraftIds.Length == 0)
            {
                return [];
            }

            return await CreateTenantQueryableFor<WikiDocumentDraft>()
                .Where(draft => latestDraftIds.Contains(draft.Id) && !draft.IsDeleted)
                .Select(draft => new WikiTerminalDraftEvidence
                {
                    DraftId = draft.Id,
                    DocumentId = draft.DocumentId,
                    Title = draft.Title,
                    Slug = draft.Slug,
                    Summary = draft.Summary,
                    DraftVersion = draft.DraftVersion,
                    ReviewState = draft.ReviewState,
                    PayloadPurgedAt = draft.PayloadPurgedAt,
                    ModifyTime = draft.ModifyTime
                })
                .ToListAsync();
        });
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
                Version = command.Draft.BaseDocumentVersion + 1,
                ModifyTime = command.NowUtc,
                ModifyBy = command.OperatorName,
                ModifyId = command.OperatorId
            })
            .Where(document =>
                document.Id == command.DocumentId &&
                document.TenantId == command.TenantId &&
                document.Version == command.Draft.BaseDocumentVersion &&
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
                (draft.ModifyTime ?? draft.ReviewedAt ?? draft.CreateTime) <= cutoffUtc)
            .OrderBy(draft => draft.ModifyTime ?? draft.ReviewedAt ?? draft.CreateTime)
            .OrderBy(draft => draft.Id)
            .Take(batchSize)
            .Select(draft => draft.Id)
            .ToListAsync());
        if (candidateIds.Count == 0)
        {
            return 0;
        }

        return await ExecuteDbOperationAsync(async () =>
        {
            var ownsTransaction = DbProtectedClient.Ado.Transaction == null;
            if (ownsTransaction)
            {
                DbProtectedClient.Ado.BeginTran();
            }

            try
            {
                var purgedCount = await DbProtectedClient.Updateable<WikiDocumentDraft>()
                    .SetColumns(draft => new WikiDocumentDraft
                    {
                        MarkdownContent = string.Empty,
                        PayloadPurgedAt = nowUtc,
                        ModifyTime = nowUtc,
                        ModifyBy = "System"
                    })
                    .Where(draft => candidateIds.Contains(draft.Id) && draft.PayloadPurgedAt == null)
                    .ExecuteCommandAsync();
                if (purgedCount <= 0)
                {
                    if (ownsTransaction)
                    {
                        DbProtectedClient.Ado.CommitTran();
                    }
                    return 0;
                }

                var draftKinds = new[]
                {
                    (int)Radish.Shared.CustomEnum.WikiAttachmentReferenceKind.DraftContent,
                    (int)Radish.Shared.CustomEnum.WikiAttachmentReferenceKind.DraftCover
                };
                await DbProtectedClient.Updateable<WikiAttachmentReference>()
                    .SetColumns(reference => new WikiAttachmentReference
                    {
                        IsDeleted = true,
                        DeletedAt = nowUtc,
                        DeletedBy = "System",
                        ModifyTime = nowUtc,
                        ModifyBy = "System"
                    })
                    .Where(reference =>
                        candidateIds.Contains(reference.ReferenceSourceId) &&
                        draftKinds.Contains(reference.ReferenceKind) &&
                        !reference.IsDeleted)
                    .ExecuteCommandAsync();
                if (ownsTransaction)
                {
                    DbProtectedClient.Ado.CommitTran();
                }
                return purgedCount;
            }
            catch
            {
                if (ownsTransaction)
                {
                    DbProtectedClient.Ado.RollbackTran();
                }
                throw;
            }
        });
    }

    public Task<(IReadOnlyList<WikiDocumentGovernanceEvent> Items, int Total)> QueryGovernanceHistoryAsync(
        WikiDocumentGovernanceHistoryQuery query)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var documentExists = await CreateTenantQueryableFor<WikiDocument>(includeDeleted: true)
                .Where(document => document.Id == query.DocumentId && document.TenantId == query.TenantId)
                .AnyAsync();
            if (!documentExists)
            {
                throw new WikiDocumentGovernanceTargetUnavailableException();
            }

            RefAsync<int> total = 0;
            var items = await DbProtectedClient.Queryable<WikiDocumentGovernanceEvent>()
                .Where(governanceEvent =>
                    governanceEvent.TenantId == query.TenantId &&
                    governanceEvent.DocumentId == query.DocumentId)
                .OrderByDescending(governanceEvent => governanceEvent.ResultGovernanceVersion)
                .OrderByDescending(governanceEvent => governanceEvent.Id)
                .ToPageListAsync(query.PageIndex, query.PageSize, total);
            return ((IReadOnlyList<WikiDocumentGovernanceEvent>)items, total.Value);
        });
    }

    public Task<WikiDocumentGovernanceWriteResult> ApplyGovernanceMutationAsync(
        WikiDocumentGovernanceMutationCommand command)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var ownsTransaction = _unitOfWorkManage.TranCount <= 0;
            if (ownsTransaction)
            {
                DbProtectedClient.Ado.BeginTran();
            }

            try
            {
                await AcquireGovernanceLockAsync(command.TenantId, command.DocumentId);
                var document = await CreateTenantQueryableFor<WikiDocument>(includeDeleted: true)
                    .Where(candidate =>
                        candidate.Id == command.DocumentId &&
                        candidate.TenantId == command.TenantId)
                    .FirstAsync();
                if (document == null)
                {
                    throw new WikiDocumentGovernanceTargetUnavailableException();
                }

                if (document.GovernanceVersion != command.ExpectedGovernanceVersion)
                {
                    throw new WikiDocumentGovernanceVersionConflictException();
                }

                if (command.ExpectedDocumentVersion.HasValue &&
                    document.Version != command.ExpectedDocumentVersion.Value)
                {
                    throw new WikiDocumentContentVersionConflictException();
                }

                var resultGovernanceVersion = command.ExpectedGovernanceVersion + 1;
                var update = DbProtectedClient.Updateable<WikiDocument>()
                    .SetColumns(candidate => new WikiDocument
                    {
                        Status = command.TargetStatus,
                        PublishedAt = command.TargetPublishedAt,
                        Visibility = command.TargetVisibility,
                        AllowedRoles = command.TargetAllowedRoles,
                        AllowedPermissions = command.TargetAllowedPermissions,
                        IsDeleted = command.TargetIsDeleted,
                        DeletedAt = command.TargetDeletedAt,
                        DeletedBy = command.TargetDeletedBy,
                        GovernanceVersion = resultGovernanceVersion,
                        ModifyTime = command.NowUtc,
                        ModifyBy = command.ActorName,
                        ModifyId = command.ActorUserId
                    });
                if (command.ContentMutation != null)
                {
                    update = update.SetColumns(candidate => new WikiDocument
                    {
                        Title = command.ContentMutation.Title,
                        MarkdownContent = command.ContentMutation.MarkdownContent,
                        Version = command.ContentMutation.ResultDocumentVersion
                    });
                }

                update = update.Where(candidate =>
                    candidate.Id == command.DocumentId &&
                    candidate.TenantId == command.TenantId &&
                    candidate.GovernanceVersion == command.ExpectedGovernanceVersion);
                if (command.ExpectedDocumentVersion.HasValue)
                {
                    var expectedDocumentVersion = command.ExpectedDocumentVersion.Value;
                    update = update.Where(candidate => candidate.Version == expectedDocumentVersion);
                }

                var affected = await update.ExecuteCommandAsync();
                if (affected != 1)
                {
                    var current = await CreateTenantQueryableFor<WikiDocument>(includeDeleted: true)
                        .Where(candidate =>
                            candidate.Id == command.DocumentId &&
                            candidate.TenantId == command.TenantId)
                        .FirstAsync();
                    if (current == null)
                    {
                        throw new WikiDocumentGovernanceTargetUnavailableException();
                    }
                    if (command.ExpectedDocumentVersion.HasValue &&
                        current.Version != command.ExpectedDocumentVersion.Value)
                    {
                        throw new WikiDocumentContentVersionConflictException();
                    }
                    throw new WikiDocumentGovernanceVersionConflictException();
                }

                var resultDocumentVersion = command.ContentMutation?.ResultDocumentVersion ?? document.Version;
                var governanceEvent = new WikiDocumentGovernanceEvent
                {
                    Id = SnowFlakeSingle.Instance.NextId(),
                    TenantId = command.TenantId,
                    DocumentId = command.DocumentId,
                    Action = command.Action,
                    FromStatus = document.Status,
                    ToStatus = command.TargetStatus,
                    FromVisibility = document.Visibility,
                    ToVisibility = command.TargetVisibility,
                    FromAllowedRoles = document.AllowedRoles,
                    ToAllowedRoles = command.TargetAllowedRoles,
                    FromAllowedPermissions = document.AllowedPermissions,
                    ToAllowedPermissions = command.TargetAllowedPermissions,
                    FromIsDeleted = document.IsDeleted,
                    ToIsDeleted = command.TargetIsDeleted,
                    FromDocumentVersion = document.Version,
                    ToDocumentVersion = resultDocumentVersion,
                    ExpectedGovernanceVersion = command.ExpectedGovernanceVersion,
                    ResultGovernanceVersion = resultGovernanceVersion,
                    SourceRevisionId = command.SourceRevisionId,
                    Reason = command.Reason,
                    ActorUserId = command.ActorUserId,
                    ActorName = command.ActorName,
                    CreateTime = command.NowUtc
                };
                await DbProtectedClient.Insertable(governanceEvent).ExecuteCommandAsync();

                document.Status = command.TargetStatus;
                document.PublishedAt = command.TargetPublishedAt;
                document.Visibility = command.TargetVisibility;
                document.AllowedRoles = command.TargetAllowedRoles;
                document.AllowedPermissions = command.TargetAllowedPermissions;
                document.IsDeleted = command.TargetIsDeleted;
                document.DeletedAt = command.TargetDeletedAt;
                document.DeletedBy = command.TargetDeletedBy;
                document.GovernanceVersion = resultGovernanceVersion;
                document.ModifyTime = command.NowUtc;
                document.ModifyBy = command.ActorName;
                document.ModifyId = command.ActorUserId;
                if (command.ContentMutation != null)
                {
                    document.Title = command.ContentMutation.Title;
                    document.MarkdownContent = command.ContentMutation.MarkdownContent;
                    document.Version = command.ContentMutation.ResultDocumentVersion;
                }

                if (ownsTransaction)
                {
                    DbProtectedClient.Ado.CommitTran();
                }
                return new WikiDocumentGovernanceWriteResult(document, governanceEvent);
            }
            catch
            {
                if (ownsTransaction)
                {
                    DbProtectedClient.Ado.RollbackTran();
                }
                throw;
            }
        });
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

    private static int[] TerminalDraftStates() =>
    [
        (int)Radish.Shared.CustomEnum.WikiDocumentDraftState.Applied,
        (int)Radish.Shared.CustomEnum.WikiDocumentDraftState.Rejected,
        (int)Radish.Shared.CustomEnum.WikiDocumentDraftState.Withdrawn
    ];

    private async Task AcquireGovernanceLockAsync(long tenantId, long documentId)
    {
        if (DbProtectedClient.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return;
        }

        var source = $"radish-wiki-governance:{tenantId}:{documentId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(hash);
        await DbProtectedClient.Ado.ExecuteCommandAsync(
            "SELECT pg_advisory_xact_lock(@LockKey)",
            new SugarParameter("@LockKey", lockKey));
    }
}
