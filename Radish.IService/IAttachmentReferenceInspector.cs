namespace Radish.IService;

/// <summary>
/// 统一检查附件是否仍被业务引用，避免孤立清理误伤间接引用的附件。
/// </summary>
public interface IAttachmentReferenceInspector
{
    /// <summary>
    /// 获取当前候选附件中仍被业务引用的附件 Id 集合。
    /// </summary>
    Task<HashSet<long>> GetReferencedAttachmentIdsAsync(IReadOnlyCollection<long> attachmentIds);

    /// <summary>
    /// 判断单个附件是否仍被业务引用。
    /// </summary>
    Task<bool> IsReferencedAsync(long attachmentId);
}
