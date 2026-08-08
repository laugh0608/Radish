using System.Linq.Expressions;
using Radish.IRepository.Base;
using Radish.Model;
using SqlSugar;

namespace Radish.IRepository;

/// <summary>Wiki 文档仓储接口</summary>
public interface IWikiDocumentRepository : IBaseRepository<WikiDocument>
{
    Task<WikiDocumentDraft?> QueryLatestTerminalDraftAsync(long documentId);
    Task<List<WikiTerminalDraftEvidence>> QueryLatestTerminalDraftEvidenceAsync(IReadOnlyCollection<long> documentIds);
    Task<int> SaveDraftAsync(WikiDraftSaveCommand command);
    Task<int> TransitionDraftAsync(WikiDraftTransitionCommand command);
    Task<bool> TryAddCollaboratorAsync(WikiDocumentCollaborator collaborator);
    Task<int> TransitionCollaboratorAsync(WikiCollaboratorTransitionCommand command);
    Task<int> SetActiveDraftAsync(long documentId, long tenantId, long? expectedDraftId, long? targetDraftId, long operatorId, string operatorName, DateTime nowUtc);
    Task<int> ApplyDraftToDocumentAsync(WikiDraftApplyCommand command);
    Task<int> PurgeTerminalDraftPayloadsAsync(DateTime cutoffUtc, int batchSize, DateTime nowUtc);
    /// <summary>根据 ID 查询文档（包含已删除数据）</summary>
    Task<WikiDocument?> QueryByIdIncludingDeletedAsync(long id);

    /// <summary>分页查询文档（包含已删除数据）</summary>
    Task<(List<WikiDocument> data, int totalCount)> QueryPageIncludingDeletedAsync(
        Expression<Func<WikiDocument, bool>>? whereExpression,
        int pageIndex,
        int pageSize,
        Expression<Func<WikiDocument, object>>? orderByExpression,
        OrderByType orderByType,
        Expression<Func<WikiDocument, object>>? thenByExpression,
        OrderByType thenByType);

    /// <summary>查询文档列表（包含已删除数据）</summary>
    Task<List<WikiDocument>> QueryIncludingDeletedAsync(
        Expression<Func<WikiDocument, bool>>? whereExpression,
        Expression<Func<WikiDocument, object>>? orderByExpression,
        OrderByType orderByType);
}

public sealed class WikiTerminalDraftEvidence
{
    public long DraftId { get; set; }
    public long DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public int DraftVersion { get; set; }
    public int ReviewState { get; set; }
    public DateTime? PayloadPurgedAt { get; set; }
    public DateTime? ModifyTime { get; set; }
}

public sealed record WikiDraftSaveCommand(
    long DraftId,
    long TenantId,
    int ExpectedDraftVersion,
    string Title,
    string Slug,
    string? Summary,
    string MarkdownContent,
    long? CoverAttachmentId,
    long? ProposedParentId,
    string? ChangeSummary,
    long OperatorId,
    string OperatorName,
    DateTime NowUtc);

public sealed record WikiDraftTransitionCommand(
    long DraftId,
    long TenantId,
    int ExpectedDraftVersion,
    IReadOnlyCollection<int> AllowedSourceStates,
    int TargetState,
    string? ChangeSummary,
    string? ReviewComment,
    long OperatorId,
    string OperatorName,
    DateTime NowUtc);

public sealed record WikiDraftApplyCommand(
    long DocumentId,
    long TenantId,
    WikiDocumentDraft Draft,
    long? FinalParentId,
    long OperatorId,
    string OperatorName,
    DateTime NowUtc);

public sealed record WikiCollaboratorTransitionCommand(
    long CollaboratorId,
    long TenantId,
    int ExpectedState,
    int TargetState,
    long OperatorId,
    string OperatorName,
    DateTime NowUtc);
