namespace Radish.Model.ViewModels;

/// <summary>
/// 租户缓存查询结果视图模型
/// </summary>
public class TenantCacheResultVo
{
    /// <summary>
    /// 租户列表
    /// </summary>
    public List<TenantVo> VoTenants { get; set; } = new();
}