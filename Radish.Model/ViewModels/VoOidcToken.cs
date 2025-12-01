using System;

namespace Radish.Model.ViewModels;

/// <summary>OIDC Token 视图模型</summary>
public class VoOidcToken
{
    /// <summary>Token Id</summary>
    public long Id { get; set; }

    /// <summary>关联的应用 Id</summary>
    public long ApplicationId { get; set; }

    /// <summary>关联的授权 Id</summary>
    public long? AuthorizationId { get; set; }

    /// <summary>Token 主体（用户 Id）</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Token 类型</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Token 状态</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>引用 Id</summary>
    public string? ReferenceId { get; set; }

    /// <summary>Token 载荷</summary>
    /// <remarks>安全考虑，不返回完整 Payload，仅用于调试</remarks>
    public string? PayloadPreview { get; set; }

    /// <summary>自定义属性</summary>
    public string Properties { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>过期时间</summary>
    public DateTime? ExpirationTime { get; set; }

    /// <summary>兑换时间</summary>
    public DateTime? RedemptionTime { get; set; }

    /// <summary>是否已过期</summary>
    public bool IsExpired => ExpirationTime.HasValue && ExpirationTime.Value < DateTime.Now;

    /// <summary>是否已兑换</summary>
    public bool IsRedeemed => RedemptionTime.HasValue;
}
