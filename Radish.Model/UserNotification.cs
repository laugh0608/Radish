using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Model.Tenants;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户通知关系实体</summary>
/// <remarks>
/// 记录用户与通知的关联关系，支持多租户隔离，主键为 Id，类型为 long
/// 一条通知可以发送给多个用户，每个用户有独立的已读状态和推送状态
/// 存储位置：独立的 Message 数据库，与业务数据隔离
/// </remarks>
[Tenant(configId: "Message")]
[SugarTable("UserNotification")]
public class UserNotification : RootEntityTKey<long>, ITenantEntity
{
    /// <summary>初始化默认用户通知实例</summary>
    public UserNotification()
    {
        InitializeDefaults();
    }

    /// <summary>通过初始化选项批量构造用户通知</summary>
    /// <param name="options">初始化选项</param>
    public UserNotification(UserNotificationInitializationOptions options)
    {
        options = options ?? throw new ArgumentNullException(nameof(options));

        InitializeDefaults();
        ApplyBasicInformation(options);
        ApplyStatusInformation(options);
        ApplyDeliveryInformation(options);
        ApplyTenantInformation(options);
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        UserId = 0;
        NotificationId = 0;
        IsRead = false;
        ReadAt = null;
        DeliveryStatus = "Created";
        DeliveredAt = null;
        RetryCount = 0;
        LastRetryAt = null;
        IsDeleted = false;
        DeletedAt = null;
        TenantId = 0;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    /// <summary>处理基础信息</summary>
    private void ApplyBasicInformation(UserNotificationInitializationOptions options)
    {
        if (options.UserId.HasValue)
        {
            UserId = options.UserId.Value;
        }

        if (options.NotificationId.HasValue)
        {
            NotificationId = options.NotificationId.Value;
        }
    }

    /// <summary>处理状态信息</summary>
    private void ApplyStatusInformation(UserNotificationInitializationOptions options)
    {
        if (options.IsRead.HasValue)
        {
            IsRead = options.IsRead.Value;
        }

        if (options.ReadAt.HasValue)
        {
            ReadAt = options.ReadAt.Value;
        }

        if (options.IsDeleted.HasValue)
        {
            IsDeleted = options.IsDeleted.Value;
        }

        if (options.DeletedAt.HasValue)
        {
            DeletedAt = options.DeletedAt.Value;
        }
    }

    /// <summary>处理推送信息</summary>
    private void ApplyDeliveryInformation(UserNotificationInitializationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.DeliveryStatus))
        {
            DeliveryStatus = options.DeliveryStatus.Trim();
        }

        if (options.DeliveredAt.HasValue)
        {
            DeliveredAt = options.DeliveredAt.Value;
        }

        if (options.RetryCount.HasValue)
        {
            RetryCount = options.RetryCount.Value;
        }

        if (options.LastRetryAt.HasValue)
        {
            LastRetryAt = options.LastRetryAt.Value;
        }
    }

    /// <summary>处理租户信息</summary>
    private void ApplyTenantInformation(UserNotificationInitializationOptions options)
    {
        if (options.TenantId.HasValue)
        {
            TenantId = options.TenantId.Value;
        }
    }

    #region 基础信息

    /// <summary>用户 ID</summary>
    /// <remarks>不可为空，通知接收者的用户 ID</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "用户ID")]
    public long UserId { get; set; } = 0;

    /// <summary>通知 ID</summary>
    /// <remarks>不可为空，关联到 Notification 表</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "通知ID")]
    public long NotificationId { get; set; } = 0;

    #endregion

    #region 已读状态

    /// <summary>是否已读</summary>
    /// <remarks>不可为空，默认为 false（未读）</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "是否已读")]
    public bool IsRead { get; set; } = false;

    /// <summary>已读时间</summary>
    /// <remarks>可空，用户标记已读的时间</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "已读时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ReadAt { get; set; }

    #endregion

    #region 推送状态

    /// <summary>推送状态</summary>
    /// <remarks>
    /// 不可为空，最大 20 字符，默认为 Created
    /// Created - 已创建，等待推送
    /// Delivered - 已送达
    /// Failed - 推送失败
    /// Abandoned - 已放弃（重试次数超限）
    /// </remarks>
    [SugarColumn(Length = 20, IsNullable = false, ColumnDescription = "推送状态")]
    public string DeliveryStatus { get; set; } = "Created";

    /// <summary>推送时间</summary>
    /// <remarks>可空，成功推送到客户端的时间</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "推送时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DeliveredAt { get; set; }

    /// <summary>重试次数</summary>
    /// <remarks>不可为空，默认为 0，推送失败时递增</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "重试次数")]
    public int RetryCount { get; set; } = 0;

    /// <summary>最后重试时间</summary>
    /// <remarks>可空，最后一次重试推送的时间</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "最后重试时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? LastRetryAt { get; set; }

    #endregion

    #region 删除状态

    /// <summary>是否已删除</summary>
    /// <remarks>不可为空，默认为 false（软删除）</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "是否已删除")]
    public bool IsDeleted { get; set; } = false;

    /// <summary>删除时间</summary>
    /// <remarks>可空，用户删除通知的时间</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "删除时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DeletedAt { get; set; }

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

/// <summary>用户通知初始化选项</summary>
public sealed class UserNotificationInitializationOptions
{
    /// <summary>必填项构造</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="notificationId">通知 ID</param>
    public UserNotificationInitializationOptions(long userId, long notificationId)
    {
        if (userId <= 0)
        {
            throw new ArgumentException("用户 ID 必须大于 0。", nameof(userId));
        }

        if (notificationId <= 0)
        {
            throw new ArgumentException("通知 ID 必须大于 0。", nameof(notificationId));
        }

        UserId = userId;
        NotificationId = notificationId;
    }

    /// <summary>用户 ID</summary>
    public long? UserId { get; }

    /// <summary>通知 ID</summary>
    public long? NotificationId { get; }

    /// <summary>是否已读</summary>
    public bool? IsRead { get; set; }

    /// <summary>已读时间</summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>推送状态</summary>
    public string? DeliveryStatus { get; set; }

    /// <summary>推送时间</summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>重试次数</summary>
    public int? RetryCount { get; set; }

    /// <summary>最后重试时间</summary>
    public DateTime? LastRetryAt { get; set; }

    /// <summary>是否已删除</summary>
    public bool? IsDeleted { get; set; }

    /// <summary>删除时间</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>租户 ID</summary>
    public long? TenantId { get; set; }
}
