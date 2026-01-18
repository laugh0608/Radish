using System;

namespace Radish.Model.ViewModels;

/// <summary>OIDC Token 视图模型</summary>
public class OidcTokenVo
{
    /// <summary>Token Id</summary>
    public long VoId { get; set; }

    /// <summary>关联的应用 Id</summary>
    public long VoApplicationId { get; set; }

    /// <summary>关联的授权 Id</summary>
    public long? VoAuthorizationId { get; set; }

    /// <summary>Token 主体（用户 Id）</summary>
    public string VoSubject { get; set; } = string.Empty;

    /// <summary>Token 类型</summary>
    public string VoType { get; set; } = string.Empty;

    /// <summary>Token 状态</summary>
    public string VoStatus { get; set; } = string.Empty;

    /// <summary>引用 Id</summary>
    public string? VoReferenceId { get; set; }

    /// <summary>Token 载荷</summary>
    /// <remarks>安全考虑，不返回完整 Payload，仅用于调试</remarks>
    public string? VoPayloadPreview { get; set; }

    /// <summary>自定义属性</summary>
    public string VoProperties { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime VoCreateTime { get; set; }

    /// <summary>过期时间</summary>
    public DateTime? VoExpirationTime { get; set; }

    /// <summary>兑换时间</summary>
    public DateTime? VoRedemptionTime { get; set; }

    /// <summary>是否已过期</summary>
    public bool VoIsExpired => VoExpirationTime.HasValue && VoExpirationTime.Value < DateTime.Now;

    /// <summary>是否已兑换</summary>
    public bool VoIsRedeemed => VoRedemptionTime.HasValue;
}
