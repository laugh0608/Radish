using Radish.IRepository.Base;
using Radish.Model;

namespace Radish.IRepository;

public interface IWikiAttachmentReferenceRepository : IBaseRepository<WikiAttachmentReference>
{
    Task<List<WikiAttachmentReference>> QueryActiveByAttachmentAsync(long tenantId, long attachmentId);

    Task<List<WikiAttachmentReference>> QueryActiveByAttachmentsAsync(
        long tenantId,
        IReadOnlyCollection<long> attachmentIds);

    Task<List<WikiAttachmentReference>> QueryActiveBySourceAsync(
        long tenantId,
        int referenceKind,
        long referenceSourceId);

    Task SyncSourceAsync(WikiAttachmentReferenceSyncCommand command);

    Task AppendRevisionAsync(WikiAttachmentReferenceSyncCommand command);

    Task<int> SoftDeleteSourceAsync(
        long tenantId,
        int referenceKind,
        long referenceSourceId,
        long operatorId,
        string operatorName,
        DateTime nowUtc);

    Task<HashSet<long>> GetReferencedAttachmentIdsAsync(IReadOnlyCollection<long> attachmentIds);
}

public sealed record WikiAttachmentReferenceSyncCommand(
    long TenantId,
    long DocumentId,
    int ReferenceKind,
    long ReferenceSourceId,
    IReadOnlyCollection<long> AttachmentIds,
    long OperatorId,
    string OperatorName,
    DateTime NowUtc);

public sealed class WikiAttachmentReferenceConflictException : Exception
{
}
