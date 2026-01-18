namespace Radish.Model.ViewModels;

/// <summary>
/// 租户添加测试结果视图模型
/// </summary>
public class TenantAddTestResultVo
{
    /// <summary>
    /// 添加结果（返回的ID或影响行数）
    /// </summary>
    public long VoResult { get; set; }

    /// <summary>
    /// 租户ID
    /// </summary>
    public long VoTenantId { get; set; }
}