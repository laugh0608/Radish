using Radish.Model;

namespace Radish.IService;

public interface IWikiAttachmentAccessService
{
    Task<bool> IsWikiControlledAsync(Attachment attachment);

    Task<bool> CanReadAsync(
        Attachment attachment,
        long tenantId,
        long? userId,
        IReadOnlyCollection<string>? roleNames);

    Task<IReadOnlySet<long>> GetReadableAttachmentIdsAsync(
        IReadOnlyCollection<Attachment> attachments,
        long tenantId,
        long? userId,
        IReadOnlyCollection<string>? roleNames);

    Task<bool> CanManageAsync(
        Attachment attachment,
        long tenantId,
        long userId,
        IReadOnlyCollection<string>? roleNames);
}
