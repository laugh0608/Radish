namespace Radish.Shared.CustomEnum;

/// <summary>
/// 用户状态码，替代 -1 / 0 等魔术数字。
/// </summary>
public enum UserStatusCodeEnum
{
    /// <summary>
    /// 未初始化或未知状态，对应历史代码中的 -1。
    /// </summary>
    Unknown = -1,

    /// <summary>
    /// 正常启用，对应历史代码中的 0。
    /// </summary>
    Normal = 0,
}
