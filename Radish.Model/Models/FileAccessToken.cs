using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model.Models;

/// <summary>
/// 临时授权访问令牌
/// </summary>
[SugarTable("FileAccessToken")]
public class FileAccessToken : RootEntityTKey<long>
{
    /// <summary>
    /// 令牌（GUID）
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = false, UniqueGroupNameList = new[] { "UK_Token" })]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 附件ID
    /// </summary>
    public long AttachmentId { get; set; }

    /// <summary>
    /// 授权用户ID（可选，为空表示任何人可访问）
    /// </summary>
    public long? AuthorizedUserId { get; set; }

    /// <summary>
    /// 授权IP地址（可选）
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? AuthorizedIp { get; set; }

    /// <summary>
    /// 最大访问次数（0表示无限制）
    /// </summary>
    public int MaxAccessCount { get; set; } = 1;

    /// <summary>
    /// 已访问次数
    /// </summary>
    public int AccessCount { get; set; } = 0;

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 创建用户ID
    /// </summary>
    public long CreatedBy { get; set; }

    /// <summary>
    /// 是否已撤销
    /// </summary>
    public bool IsRevoked { get; set; } = false;

    /// <summary>
    /// 撤销时间
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// 最后访问时间
    /// </summary>
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 修改时间
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? ModifyTime { get; set; }
}
