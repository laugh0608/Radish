namespace Radish.Shared.CustomEnum;

/// <summary>
/// 权限范围，基于 Role.AuthorityScope 历史注释整理。
/// </summary>
public enum AuthorityScopeKindEnum
{
    /// <summary>
    /// 无任何权限，对应 -1。
    /// </summary>
    None = -1,

    /// <summary>
    /// 自定义权限，对应 1。
    /// </summary>
    Custom = 1,

    /// <summary>
    /// 本部门，对应 2。
    /// </summary>
    Department = 2,

    /// <summary>
    /// 本部门及以下，对应 3。
    /// </summary>
    DepartmentAndChildren = 3,

    /// <summary>
    /// 仅自己，对应 4。
    /// </summary>
    Self = 4,

    /// <summary>
    /// 全部，对应 9。
    /// </summary>
    All = 9,
}
