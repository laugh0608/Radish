namespace Radish.Extension.PermissionExtension;

/// <summary>
/// 用户或角色或其他凭据实体
/// </summary>
/// <remarks>目前的策略是：角色-URL </remarks>
public class PermissionItem
{
    /// <summary>
    /// 用户角色
    /// </summary>
    /// <remarks>默认为 Visitor</remarks>
    public virtual string Role { get; set; } = "Visitor";

    /// <summary>
    /// 用户访问的请求 Url
    /// </summary>
    /// <remarks>默认为 api/</remarks>
    public virtual string Url { get; set; } = "api/";
}