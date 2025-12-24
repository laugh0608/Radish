namespace Radish.Model.ViewModels;

/// <summary>
/// 文件访问令牌 ViewModel
/// </summary>
public class FileAccessTokenVo
{
    /// <summary>
    /// 令牌
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 附件ID
    /// </summary>
    public long AttachmentId { get; set; }

    /// <summary>
    /// 访问URL
    /// </summary>
    public string AccessUrl { get; set; } = string.Empty;

    /// <summary>
    /// 最大访问次数
    /// </summary>
    public int MaxAccessCount { get; set; }

    /// <summary>
    /// 已访问次数
    /// </summary>
    public int AccessCount { get; set; }

    /// <summary>
    /// 剩余访问次数
    /// </summary>
    public int RemainingAccessCount => MaxAccessCount == 0 ? int.MaxValue : MaxAccessCount - AccessCount;

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 是否已过期
    /// </summary>
    public bool IsExpired => DateTime.Now > ExpiresAt;

    /// <summary>
    /// 是否已撤销
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 创建文件访问令牌请求
/// </summary>
public class CreateFileAccessTokenDto
{
    /// <summary>
    /// 附件ID
    /// </summary>
    public long AttachmentId { get; set; }

    /// <summary>
    /// 授权用户ID（可选，为空表示任何人可访问）
    /// </summary>
    public long? AuthorizedUserId { get; set; }

    /// <summary>
    /// 最大访问次数（0表示无限制，默认1次）
    /// </summary>
    public int MaxAccessCount { get; set; } = 1;

    /// <summary>
    /// 有效期（小时，默认1小时）
    /// </summary>
    public int ValidHours { get; set; } = 1;
}
