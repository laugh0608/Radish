using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model.OpenIddict;

/// <summary>OIDC 授权记录表</summary>
/// <remarks>用于存储用户授权给客户端应用的记录</remarks>
public class RadishAuthorization : RootEntityTKey<long>
{
    /// <summary>初始化默认授权记录实例</summary>
    public RadishAuthorization()
    {
        InitializeDefaults();
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        ApplicationId = 0;
        Subject = string.Empty;
        Type = string.Empty;
        Status = string.Empty;
        Scopes = string.Empty;
        Properties = string.Empty;
        CreateTime = DateTime.Now;
    }


    /// <summary>关联的应用 Id</summary>
    /// <remarks>不可为空，关联到 RadishApplication 表</remarks>
    [SugarColumn(IsNullable = false)]
    public long ApplicationId { get; set; } = 0;

    /// <summary>授权主体（用户 Id）</summary>
    /// <remarks>不可为空，最大 200 字符，通常为用户 Id</remarks>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>授权类型</summary>
    /// <remarks>
    /// <para>可空，最大 50 字符</para>
    /// <para>如：authorization_code, client_credentials, refresh_token 等</para>
    /// </remarks>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string Type { get; set; } = string.Empty;

    /// <summary>授权状态</summary>
    /// <remarks>
    /// <para>可空，最大 50 字符</para>
    /// <para>Valid：���效</para>
    /// <para>Revoked：已撤销</para>
    /// </remarks>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string Status { get; set; } = string.Empty;

    /// <summary>授权的作用域列表</summary>
    /// <remarks>可空，最大长度 4000，JSON 格式存储</remarks>
    [SugarColumn(Length = 4000, IsNullable = true, ColumnDataType = "text")]
    public string Scopes { get; set; } = string.Empty;

    /// <summary>自定义属性</summary>
    /// <remarks>可空，最大长度 8000，JSON 格式存储</remarks>
    [SugarColumn(Length = 8000, IsNullable = true, ColumnDataType = "text")]
    public string Properties { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    /// <remarks>不可为空，默认为当前时间</remarks>
    [SugarColumn(IsNullable = false)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;
}
