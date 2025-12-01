using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model.OpenIddict;

/// <summary>OIDC Token 表</summary>
/// <remarks>用于存储 Access Token、Refresh Token、Authorization Code 等</remarks>
public class RadishToken : RootEntityTKey<long>
{
    /// <summary>初始化默认 Token 实例</summary>
    public RadishToken()
    {
        InitializeDefaults();
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        ApplicationId = 0;
        AuthorizationId = null;
        Subject = string.Empty;
        Type = string.Empty;
        Status = string.Empty;
        ReferenceId = null;
        Payload = string.Empty;
        Properties = string.Empty;
        CreateTime = DateTime.Now;
        ExpirationTime = null;
        RedemptionTime = null;
    }


    /// <summary>关联的应用 Id</summary>
    /// <remarks>不可为空，关联到 RadishApplication 表</remarks>
    [SugarColumn(IsNullable = false)]
    public long ApplicationId { get; set; } = 0;

    /// <summary>关联的授权 Id</summary>
    /// <remarks>可空，关联到 RadishAuthorization 表</remarks>
    [SugarColumn(IsNullable = true)]
    public long? AuthorizationId { get; set; }

    /// <summary>Token 主体（用户 Id）</summary>
    /// <remarks>不可为空，最大 200 字符，通常为用户 Id</remarks>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>Token 类型</summary>
    /// <remarks>
    /// <para>可空，最大 50 字符</para>
    /// <para>authorization_code：授权码</para>
    /// <para>access_token：访问令牌</para>
    /// <para>refresh_token：刷新令牌</para>
    /// <para>id_token：身份令牌</para>
    /// </remarks>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string Type { get; set; } = string.Empty;

    /// <summary>Token 状态</summary>
    /// <remarks>
    /// <para>可空，最大 50 字符</para>
    /// <para>Valid：有效</para>
    /// <para>Inactive：失活</para>
    /// <para>Revoked：已撤销</para>
    /// <para>Redeemed：已兑换（授权码使用后）</para>
    /// </remarks>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string Status { get; set; } = string.Empty;

    /// <summary>引用 Id</summary>
    /// <remarks>可空，最大 100 字符，用于引用令牌（Reference Token）</remarks>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? ReferenceId { get; set; }

    /// <summary>Token 载荷</summary>
    /// <remarks>可空，最大长度不限，存储 JWT 或其他格式的 Token ��容</remarks>
    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string Payload { get; set; } = string.Empty;

    /// <summary>自定义属性</summary>
    /// <remarks>可空，最大长度 8000，JSON 格式存储</remarks>
    [SugarColumn(Length = 8000, IsNullable = true, ColumnDataType = "text")]
    public string Properties { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    /// <remarks>不可为空，默认为当前时间</remarks>
    [SugarColumn(IsNullable = false)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>过期时间</summary>
    /// <remarks>可空，Token 失效的时间</remarks>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ExpirationTime { get; set; }

    /// <summary>兑换时间</summary>
    /// <remarks>可空，授权码被兑换为 Token 的时间</remarks>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? RedemptionTime { get; set; }
}
