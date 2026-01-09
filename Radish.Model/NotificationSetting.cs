using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Model.Tenants;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户通知偏好设置实体</summary>
/// <remarks>
/// 记录用户对各类通知的偏好配置，支持多租户隔离，主键为 Id，类型为 long
/// 每个用户对每种通知类型可以单独配置是否启用、推送渠道等
/// </remarks>
[SugarTable("NotificationSetting")]
public class NotificationSetting : RootEntityTKey<long>, ITenantEntity
{
    /// <summary>初始化默认通知设置实例</summary>
    public NotificationSetting()
    {
        InitializeDefaults();
    }

    /// <summary>通过初始化选项批量构造通知设置</summary>
    /// <param name="options">初始化选项</param>
    public NotificationSetting(NotificationSettingInitializationOptions options)
    {
        options = options ?? throw new ArgumentNullException(nameof(options));

        InitializeDefaults();
        ApplyBasicInformation(options);
        ApplyChannelInformation(options);
        ApplyTenantInformation(options);
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        UserId = 0;
        NotificationType = string.Empty;
        IsEnabled = true;
        EnableInApp = true;
        EnableEmail = false;
        EnableSound = true;
        TenantId = 0;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    /// <summary>处理基础信息</summary>
    private void ApplyBasicInformation(NotificationSettingInitializationOptions options)
    {
        if (options.UserId.HasValue)
        {
            UserId = options.UserId.Value;
        }

        NotificationType = NormalizeRequired(options.NotificationType, nameof(options.NotificationType));

        if (options.IsEnabled.HasValue)
        {
            IsEnabled = options.IsEnabled.Value;
        }
    }

    /// <summary>处理推送渠道信息</summary>
    private void ApplyChannelInformation(NotificationSettingInitializationOptions options)
    {
        if (options.EnableInApp.HasValue)
        {
            EnableInApp = options.EnableInApp.Value;
        }

        if (options.EnableEmail.HasValue)
        {
            EnableEmail = options.EnableEmail.Value;
        }

        if (options.EnableSound.HasValue)
        {
            EnableSound = options.EnableSound.Value;
        }
    }

    /// <summary>处理租户信息</summary>
    private void ApplyTenantInformation(NotificationSettingInitializationOptions options)
    {
        if (options.TenantId.HasValue)
        {
            TenantId = options.TenantId.Value;
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

    #region 基础信息

    /// <summary>用户 ID</summary>
    /// <remarks>不可为空，通知设置所属的用户 ID</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "用户ID")]
    public long UserId { get; set; } = 0;

    /// <summary>通知类型</summary>
    /// <remarks>
    /// 不可为空，最大 50 字符
    /// 例如：CommentReplied, PostLiked, CommentLiked, Mentioned, GodComment, Sofa, CoinBalanceChanged, SystemAnnouncement
    /// </remarks>
    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "通知类型")]
    public string NotificationType { get; set; } = string.Empty;

    /// <summary>是否启用</summary>
    /// <remarks>不可为空，默认为 true（启用）</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "是否启用")]
    public bool IsEnabled { get; set; } = true;

    #endregion

    #region 推送渠道配置

    /// <summary>是否启用站内推送</summary>
    /// <remarks>不可为空，默认为 true（启用）</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "是否启用站内推送")]
    public bool EnableInApp { get; set; } = true;

    /// <summary>是否启用邮件推送</summary>
    /// <remarks>不可为空，默认为 false（禁用）</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "是否启用邮件推送")]
    public bool EnableEmail { get; set; } = false;

    /// <summary>是否启用声音提示</summary>
    /// <remarks>不可为空，默认为 true（启用）</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "是否启用声音提示")]
    public bool EnableSound { get; set; } = true;

    #endregion

    #region 租户信息

    /// <summary>租户 ID</summary>
    /// <remarks>不可为空，默认为 0，支持多租户隔离</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "租户ID")]
    public long TenantId { get; set; } = 0;

    #endregion

    #region 审计信息

    /// <summary>创建时间</summary>
    /// <remarks>不可为空，默认为当前时间，更新时忽略该列</remarks>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>创建者名称</summary>
    /// <remarks>不可为空，最大 50 字符，默认为 System</remarks>
    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "创建者名称")]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建者 ID</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "创建者ID")]
    public long CreateId { get; set; } = 0;

    /// <summary>修改时间</summary>
    /// <remarks>可空，用户更新设置的时间</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>修改者名称</summary>
    /// <remarks>可空，最大 50 字符</remarks>
    [SugarColumn(Length = 50, IsNullable = true, ColumnDescription = "修改者名称")]
    public string? ModifyBy { get; set; }

    /// <summary>修改者 ID</summary>
    /// <remarks>可空</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改者ID")]
    public long? ModifyId { get; set; }

    #endregion
}

/// <summary>通知设置初始化选项</summary>
public sealed class NotificationSettingInitializationOptions
{
    /// <summary>必填项构造</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="notificationType">通知类型</param>
    public NotificationSettingInitializationOptions(long userId, string notificationType)
    {
        if (userId <= 0)
        {
            throw new ArgumentException("用户 ID 必须大于 0。", nameof(userId));
        }

        UserId = userId;
        NotificationType = notificationType ?? throw new ArgumentNullException(nameof(notificationType));
    }

    /// <summary>用户 ID</summary>
    public long? UserId { get; }

    /// <summary>通知类型</summary>
    public string NotificationType { get; }

    /// <summary>是否启用</summary>
    public bool? IsEnabled { get; set; }

    /// <summary>是否启用站内推送</summary>
    public bool? EnableInApp { get; set; }

    /// <summary>是否启用邮件推送</summary>
    public bool? EnableEmail { get; set; }

    /// <summary>是否启用声音提示</summary>
    public bool? EnableSound { get; set; }

    /// <summary>租户 ID</summary>
    public long? TenantId { get; set; }
}
