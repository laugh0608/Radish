namespace Radish.Model.ViewModels;

/// <summary>
/// 用户提及视图模型
/// 用于@提及功能的用户搜索结果
/// </summary>
public class UserMentionVo
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 用户名（登录名）
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称（昵称）
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 用户头像URL
    /// </summary>
    public string? Avatar { get; set; }
}
