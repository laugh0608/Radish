namespace Radish.Model.ViewModels;

/// <summary>
/// 文件访问令牌 ViewModel
/// </summary>
public class FileAccessTokenSummaryVo
{
    /// <summary>
    /// 令牌记录 ID
    /// </summary>
    public long VoId { get; set; }

    /// <summary>
    /// 不可用于访问的令牌摘要
    /// </summary>
    public string VoTokenPreview { get; set; } = string.Empty;

    /// <summary>
    /// 附件ID
    /// </summary>
    public long VoAttachmentId { get; set; }

    /// <summary>
    /// 创建用户 ID
    /// </summary>
    public long VoCreatedBy { get; set; }

    /// <summary>
    /// 最大访问次数
    /// </summary>
    public int VoMaxAccessCount { get; set; }

    /// <summary>
    /// 已访问次数
    /// </summary>
    public int VoAccessCount { get; set; }

    /// <summary>
    /// 剩余访问次数
    /// </summary>
    public int VoRemainingAccessCount => VoMaxAccessCount == 0
        ? int.MaxValue
        : Math.Max(0, VoMaxAccessCount - VoAccessCount);

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime VoExpiresAt { get; set; }

    /// <summary>
    /// 是否已过期
    /// </summary>
    public bool VoIsExpired { get; set; }

    /// <summary>
    /// 是否已撤销
    /// </summary>
    public bool VoIsRevoked { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime VoCreateTime { get; set; }
}

/// <summary>
/// 创建文件访问令牌时的一次性返回模型。
/// </summary>
public sealed class FileAccessTokenCreatedVo : FileAccessTokenSummaryVo
{
    /// <summary>
    /// 原始令牌，仅在创建成功时返回一次。
    /// </summary>
    public string VoToken { get; set; } = string.Empty;

    /// <summary>
    /// 包含原始令牌的访问 URL，仅在创建成功时返回一次。
    /// </summary>
    public string VoAccessUrl { get; set; } = string.Empty;
}

/// <summary>
/// 按记录 ID 撤销文件访问令牌。
/// </summary>
public sealed class RevokeFileAccessTokenDto
{
    public long TokenId { get; set; }
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
    /// 授权 IP 地址（可选，为空表示不限 IP）
    /// </summary>
    public string? AuthorizedIp { get; set; }

    /// <summary>
    /// 最大访问次数（0表示无限制，默认1次）
    /// </summary>
    public int MaxAccessCount { get; set; } = 1;

    /// <summary>
    /// 有效期（小时，默认1小时）
    /// </summary>
    public int ValidHours { get; set; } = 1;
}
