using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Model.Tenants;
using SqlSugar;

namespace Radish.Model;

/// <summary>通知实体</summary>
/// <remarks>
/// 支持按月分表和多租户隔离，主键为 Id，类型为 long
/// 分表格式：Notification_YYYYMM（例如：Notification_202601）
/// </remarks>
[SugarTable("Notification_{year}{month}")]
[SplitTable(SplitType.Month)]
public class Notification : RootEntityTKey<long>, ITenantEntity
{
    /// <summary>初始化默认通知实例</summary>
    public Notification()
    {
        InitializeDefaults();
    }

    /// <summary>通过初始化选项批量构造通知</summary>
    /// <param name="options">初始化选项</param>
    public Notification(NotificationInitializationOptions options)
    {
        options = options ?? throw new ArgumentNullException(nameof(options));

        InitializeDefaults();
        ApplyBasicInformation(options);
        ApplyBusinessInformation(options);
        ApplyTriggerInformation(options);
        ApplyTenantInformation(options);
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        Type = string.Empty;
        Priority = 2; // 默认普通优先级
        Title = string.Empty;
        Content = string.Empty;
        BusinessType = null;
        BusinessId = null;
        TriggerId = null;
        TriggerName = null;
        TriggerAvatar = null;
        ExtData = null;
        TenantId = 0;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    /// <summary>处理基础信息</summary>
    private void ApplyBasicInformation(NotificationInitializationOptions options)
    {
        Type = NormalizeRequired(options.Type, nameof(options.Type));
        Title = NormalizeRequired(options.Title, nameof(options.Title));
        Content = options.Content?.Trim() ?? string.Empty;

        if (options.Priority.HasValue)
        {
            Priority = Math.Max(1, Math.Min(4, options.Priority.Value));
        }
    }

    /// <summary>处理业务信息</summary>
    private void ApplyBusinessInformation(NotificationInitializationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.BusinessType))
        {
            BusinessType = options.BusinessType.Trim();
        }

        if (options.BusinessId.HasValue)
        {
            BusinessId = options.BusinessId.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.ExtData))
        {
            ExtData = options.ExtData;
        }
    }

    /// <summary>处理触发者信息</summary>
    private void ApplyTriggerInformation(NotificationInitializationOptions options)
    {
        if (options.TriggerId.HasValue)
        {
            TriggerId = options.TriggerId.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.TriggerName))
        {
            TriggerName = options.TriggerName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.TriggerAvatar))
        {
            TriggerAvatar = options.TriggerAvatar.Trim();
        }
    }

    /// <summary>处理租户信息</summary>
    private void ApplyTenantInformation(NotificationInitializationOptions options)
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

    /// <summary>通知类型</summary>
    /// <remarks>
    /// 不可为空，最大 50 字符
    /// 例如：CommentReplied, PostLiked, CommentLiked, Mentioned, GodComment, Sofa, CoinBalanceChanged, SystemAnnouncement
    /// </remarks>
    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "通知类型")]
    public string Type { get; set; } = string.Empty;

    /// <summary>通知优先级</summary>
    /// <remarks>
    /// 不可为空，默认为 2（普通）
    /// 1-低（点赞），2-普通（评论回复），3-高（@提及、神评/沙发），4-紧急（系统公告、账号安全）
    /// </remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "通知优先级")]
    public int Priority { get; set; } = 2;

    /// <summary>通知标题</summary>
    /// <remarks>不可为空，最大 200 字符</remarks>
    [SugarColumn(Length = 200, IsNullable = false, ColumnDescription = "通知标题")]
    public string Title { get; set; } = string.Empty;

    /// <summary>通知内容</summary>
    /// <remarks>不可为空，最大 1000 字符</remarks>
    [SugarColumn(Length = 1000, IsNullable = false, ColumnDescription = "通知内容")]
    public string Content { get; set; } = string.Empty;

    #endregion

    #region 业务关联信息

    /// <summary>业务类型</summary>
    /// <remarks>
    /// 可空，最大 50 字符
    /// 例如：Post, Comment, User, System
    /// </remarks>
    [SugarColumn(Length = 50, IsNullable = true, ColumnDescription = "业务类型")]
    public string? BusinessType { get; set; }

    /// <summary>业务 ID</summary>
    /// <remarks>
    /// 可空，关联的业务实体 ID
    /// 例如：帖子 ID、评论 ID 等
    /// </remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "业务ID")]
    public long? BusinessId { get; set; }

    #endregion

    #region 触发者信息

    /// <summary>触发者 ID</summary>
    /// <remarks>
    /// 可空，谁触发了这个通知
    /// 例如：点赞者、评论者的用户 ID
    /// </remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "触发者ID")]
    public long? TriggerId { get; set; }

    /// <summary>触发者名称</summary>
    /// <remarks>
    /// 可空，最大 100 字符
    /// 冗余字段，避免 JOIN 查询
    /// </remarks>
    [SugarColumn(Length = 100, IsNullable = true, ColumnDescription = "触发者名称")]
    public string? TriggerName { get; set; }

    /// <summary>触发者头像</summary>
    /// <remarks>
    /// 可空，最大 500 字符
    /// 冗余字段，避免 JOIN 查询
    /// </remarks>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "触发者头像")]
    public string? TriggerAvatar { get; set; }

    #endregion

    #region 扩展信息

    /// <summary>扩展数据</summary>
    /// <remarks>
    /// 可空，JSON 格式
    /// 用于存储额外的业务数据
    /// </remarks>
    [SugarColumn(ColumnDataType = "text", IsNullable = true, ColumnDescription = "扩展数据")]
    public string? ExtData { get; set; }

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

    #endregion
}

/// <summary>通知初始化选项</summary>
public sealed class NotificationInitializationOptions
{
    /// <summary>必填项构造</summary>
    /// <param name="type">通知类型</param>
    /// <param name="title">通知标题</param>
    public NotificationInitializationOptions(string type, string title)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Title = title ?? throw new ArgumentNullException(nameof(title));
    }

    /// <summary>通知类型</summary>
    public string Type { get; }

    /// <summary>通知标题</summary>
    public string Title { get; }

    /// <summary>通知内容</summary>
    public string? Content { get; set; }

    /// <summary>通知优先级</summary>
    public int? Priority { get; set; }

    /// <summary>业务类型</summary>
    public string? BusinessType { get; set; }

    /// <summary>业务 ID</summary>
    public long? BusinessId { get; set; }

    /// <summary>触发者 ID</summary>
    public long? TriggerId { get; set; }

    /// <summary>触发者名称</summary>
    public string? TriggerName { get; set; }

    /// <summary>触发者头像</summary>
    public string? TriggerAvatar { get; set; }

    /// <summary>扩展数据</summary>
    public string? ExtData { get; set; }

    /// <summary>租户 ID</summary>
    public long? TenantId { get; set; }
}
