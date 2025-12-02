using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model.OpenIddict;

/// <summary>OIDC 客户端应用表</summary>
/// <remarks>用于存储已注册的 OAuth 2.0 / OIDC 客户端应用</remarks>
public class RadishApplication : RootEntityTKey<long>
{
    /// <summary>初始化默认应用实例</summary>
    public RadishApplication()
    {
        InitializeDefaults();
    }

    /// <summary>通过客户端 Id 和显示名称初始化应用</summary>
    /// <param name="clientId">客户端 Id</param>
    /// <param name="displayName">显示名称</param>
    public RadishApplication(string clientId, string displayName)
        : this(new RadishApplicationInitializationOptions(clientId, displayName))
    {
    }

    /// <summary>通过初始化选项批量构造应用</summary>
    /// <param name="options">初始化选项</param>
    public RadishApplication(RadishApplicationInitializationOptions options)
    {
        options = options ?? throw new ArgumentNullException(nameof(options));

        InitializeDefaults();
        ApplyBasicInformation(options);
        ApplyOAuthInformation(options);
        ApplyExtendedInformation(options);
        ApplyStatusInformation(options);
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        ClientId = string.Empty;
        ClientSecret = null;
        ConsentType = string.Empty;
        DisplayName = string.Empty;
        Type = string.Empty;
        RedirectUris = string.Empty;
        PostLogoutRedirectUris = string.Empty;
        Permissions = string.Empty;
        Requirements = string.Empty;
        Properties = string.Empty;

        // 扩展字段
        Logo = null;
        Description = null;
        DeveloperName = null;
        DeveloperEmail = null;
        Status = ApplicationStatus.Active;
        AppType = ApplicationType.Internal;

        CreateTime = DateTime.Now;
        CreateBy = "System";
        ModifyTime = null;
        ModifyBy = null;
    }

    /// <summary>处理基础信息</summary>
    private void ApplyBasicInformation(RadishApplicationInitializationOptions options)
    {
        ClientId = NormalizeRequired(options.ClientId, nameof(options.ClientId));
        DisplayName = NormalizeRequired(options.DisplayName, nameof(options.DisplayName));

        if (!string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            ClientSecret = options.ClientSecret.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.Type))
        {
            Type = options.Type.Trim();
        }
    }

    /// <summary>处理 OAuth/OIDC 信息</summary>
    private void ApplyOAuthInformation(RadishApplicationInitializationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConsentType))
        {
            ConsentType = options.ConsentType.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.RedirectUris))
        {
            RedirectUris = options.RedirectUris.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.PostLogoutRedirectUris))
        {
            PostLogoutRedirectUris = options.PostLogoutRedirectUris.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.Permissions))
        {
            Permissions = options.Permissions.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.Requirements))
        {
            Requirements = options.Requirements.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.Properties))
        {
            Properties = options.Properties.Trim();
        }
    }

    /// <summary>处理扩展信息</summary>
    private void ApplyExtendedInformation(RadishApplicationInitializationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Logo))
        {
            Logo = options.Logo.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.Description))
        {
            Description = options.Description.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.DeveloperName))
        {
            DeveloperName = options.DeveloperName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.DeveloperEmail))
        {
            DeveloperEmail = options.DeveloperEmail.Trim();
        }

        if (options.AppType.HasValue)
        {
            AppType = options.AppType.Value;
        }
    }

    /// <summary>处理状态信息</summary>
    private void ApplyStatusInformation(RadishApplicationInitializationOptions options)
    {
        if (options.Status.HasValue)
        {
            Status = options.Status.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.CreateBy))
        {
            CreateBy = options.CreateBy.Trim();
        }
    }

    private static string NormalizeRequired(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} 不能为空。", paramName);
        }

        return value.Trim();
    }


    #region OpenIddict 标准字段

    /// <summary>客户端 Id</summary>
    /// <remarks>不可为空，最大 100 字符，唯一标识客户端应用</remarks>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>客户端密钥</summary>
    /// <remarks>可空，最大 500 字符，机密客户端需要提供</remarks>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ClientSecret { get; set; }

    /// <summary>同意类型</summary>
    /// <remarks>
    /// <para>可空，最大 50 字符</para>
    /// <para>Explicit：需要用户明确同意</para>
    /// <para>Implicit：隐式同意，不显示授权确认页</para>
    /// <para>External：外部同意</para>
    /// <para>Systematic：系统同意</para>
    /// </remarks>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string ConsentType { get; set; } = string.Empty;

    /// <summary>显示名称</summary>
    /// <remarks>不可为空，最大 200 字符，向用户展示的应用名称</remarks>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>客户端类型</summary>
    /// <remarks>
    /// <para>可空，最大 50 字符</para>
    /// <para>confidential：机密客户端（有 ClientSecret）</para>
    /// <para>public：公开客户端（无 ClientSecret，如 SPA）</para>
    /// </remarks>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string Type { get; set; } = string.Empty;

    /// <summary>重定向 URI 列表</summary>
    /// <remarks>可空，最大长度 4000，JSON 格式存储，授权成功后的回调地址</remarks>
    [SugarColumn(Length = 4000, IsNullable = true, ColumnDataType = "text")]
    public string RedirectUris { get; set; } = string.Empty;

    /// <summary>登出后重定向 URI 列表</summary>
    /// <remarks>可空，最大长度 4000，JSON 格式存储，用户登出后的回调地址</remarks>
    [SugarColumn(Length = 4000, IsNullable = true, ColumnDataType = "text")]
    public string PostLogoutRedirectUris { get; set; } = string.Empty;

    /// <summary>权限列表</summary>
    /// <remarks>
    /// <para>可空，最大长度 8000，JSON 格式存储</para>
    /// <para>包括：授权类型（authorization_code/client_credentials 等）、响应类型（code/token 等）、作用域（openid/profile 等）</para>
    /// </remarks>
    [SugarColumn(Length = 8000, IsNullable = true, ColumnDataType = "text")]
    public string Permissions { get; set; } = string.Empty;

    /// <summary>要求列表</summary>
    /// <remarks>可空，最大长度 2000，JSON 格式存储，如是否强制 PKCE</remarks>
    [SugarColumn(Length = 2000, IsNullable = true, ColumnDataType = "text")]
    public string Requirements { get; set; } = string.Empty;

    /// <summary>自定义属性</summary>
    /// <remarks>可空，最大长度 8000，JSON 格式存储，存储额外的元数据</remarks>
    [SugarColumn(Length = 8000, IsNullable = true, ColumnDataType = "text")]
    public string Properties { get; set; } = string.Empty;

    #endregion

    #region 扩展字段

    /// <summary>应用图标 URL</summary>
    /// <remarks>可空，最大 500 字符</remarks>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Logo { get; set; }

    /// <summary>应用描述</summary>
    /// <remarks>可空，最大 1000 字符</remarks>
    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? Description { get; set; }

    /// <summary>开发者名称</summary>
    /// <remarks>可空，最大 100 字符</remarks>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? DeveloperName { get; set; }

    /// <summary>开发者邮箱</summary>
    /// <remarks>可空，最大 200 字符</remarks>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string? DeveloperEmail { get; set; }

    /// <summary>应用状态</summary>
    /// <remarks>不可为空，默认为 Active（激活）</remarks>
    [SugarColumn(IsNullable = false)]
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Active;

    /// <summary>应用类型</summary>
    /// <remarks>不可为空，默认为 Internal（内部应用）</remarks>
    [SugarColumn(IsNullable = false)]
    public ApplicationType AppType { get; set; } = ApplicationType.Internal;

    #endregion

    #region 审计字段

    /// <summary>创建时间</summary>
    /// <remarks>不可为空，默认为当前时间，更新时忽略该列</remarks>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>创建者名称</summary>
    /// <remarks>不可为空，最大 50 字符，默认为 System</remarks>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    /// <summary>更新时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>更新者名称</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    #endregion
}

/// <summary>应用状态枚举</summary>
public enum ApplicationStatus
{
    /// <summary>激活</summary>
    Active = 0,

    /// <summary>禁用</summary>
    Disabled = 1,

    /// <summary>待审核（第三方应用）</summary>
    PendingReview = 2
}

/// <summary>应用类型枚举</summary>
public enum ApplicationType
{
    /// <summary>内部应用</summary>
    Internal = 0,

    /// <summary>第三方应用</summary>
    ThirdParty = 1
}

/// <summary>应用初始化选项</summary>
public sealed class RadishApplicationInitializationOptions
{
    /// <summary>必填项构造</summary>
    /// <param name="clientId">客户端 Id</param>
    /// <param name="displayName">显示名称</param>
    public RadishApplicationInitializationOptions(string clientId, string displayName)
    {
        ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
    }

    /// <summary>客户端 Id</summary>
    public string ClientId { get; }

    /// <summary>显示名称</summary>
    public string DisplayName { get; }

    /// <summary>客户端密钥</summary>
    public string? ClientSecret { get; set; }

    /// <summary>同意类型</summary>
    public string? ConsentType { get; set; }

    /// <summary>客户端类型</summary>
    public string? Type { get; set; }

    /// <summary>重定向 URI 列表</summary>
    public string? RedirectUris { get; set; }

    /// <summary>登出后重定向 URI 列表</summary>
    public string? PostLogoutRedirectUris { get; set; }

    /// <summary>权限列表</summary>
    public string? Permissions { get; set; }

    /// <summary>要求列表</summary>
    public string? Requirements { get; set; }

    /// <summary>自定义属性</summary>
    public string? Properties { get; set; }

    /// <summary>应用图标 URL</summary>
    public string? Logo { get; set; }

    /// <summary>应用描述</summary>
    public string? Description { get; set; }

    /// <summary>开发者名称</summary>
    public string? DeveloperName { get; set; }

    /// <summary>开发者邮箱</summary>
    public string? DeveloperEmail { get; set; }

    /// <summary>应用状态</summary>
    public ApplicationStatus? Status { get; set; }

    /// <summary>应用类型</summary>
    public ApplicationType? AppType { get; set; }

    /// <summary>创建者名称</summary>
    public string? CreateBy { get; set; }
}
