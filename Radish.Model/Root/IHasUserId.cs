namespace Radish.Model.Root;

/// <summary>
/// 标记实体归属某个用户
/// </summary>
public interface IHasUserId
{
    /// <summary>
    /// 用户 Id
    /// </summary>
    public long UserId { get; set; }
}
