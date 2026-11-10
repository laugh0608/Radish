using System.Diagnostics.CodeAnalysis;
using Radish.Model;
using Radish.Model.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>业务映射；字段忽略项保留服务补全、写入保护和既有默认值。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, UseDeepCloning = true,
    ThrowOnMappingNullMismatch = false, AutoUserMappings = false)]
internal static partial class NotificationMapper
{
    // Notification → NotificationVo
    [MapProperty(nameof(Notification.Id), nameof(NotificationVo.VoId))]
    [MapProperty(nameof(Notification.Type), nameof(NotificationVo.VoType))]
    [MapProperty(nameof(Notification.Priority), nameof(NotificationVo.VoPriority))]
    [MapProperty(nameof(Notification.Title), nameof(NotificationVo.VoTitle))]
    [MapProperty(nameof(Notification.Content), nameof(NotificationVo.VoContent))]
    [MapProperty(nameof(Notification.BusinessType), nameof(NotificationVo.VoBusinessType))]
    [MapProperty(nameof(Notification.BusinessId), nameof(NotificationVo.VoBusinessId))]
    [MapProperty(nameof(Notification.TriggerId), nameof(NotificationVo.VoTriggerId))]
    [MapProperty(nameof(Notification.TriggerName), nameof(NotificationVo.VoTriggerName))]
    [MapProperty(nameof(Notification.TriggerAvatar), nameof(NotificationVo.VoTriggerAvatar))]
    [MapProperty(nameof(Notification.ExtData), nameof(NotificationVo.VoExtData))]
    [MapProperty(nameof(Notification.CreateTime), nameof(NotificationVo.VoCreateTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial NotificationVo? ToNotificationVo(Notification? source);

    public static partial List<NotificationVo> ToNotificationVoList(IEnumerable<Notification>? source);

    // UserNotification → UserNotificationVo
    [MapProperty(nameof(UserNotification.Id), nameof(UserNotificationVo.VoId))]
    [MapProperty(nameof(UserNotification.UserId), nameof(UserNotificationVo.VoUserId))]
    [MapProperty(nameof(UserNotification.NotificationId), nameof(UserNotificationVo.VoNotificationId))]
    [MapProperty(nameof(UserNotification.IsRead), nameof(UserNotificationVo.VoIsRead))]
    [MapProperty(nameof(UserNotification.ReadAt), nameof(UserNotificationVo.VoReadAt))]
    [MapProperty(nameof(UserNotification.CreateTime), nameof(UserNotificationVo.VoCreateTime))]
    [MapperIgnoreTarget(nameof(UserNotificationVo.VoNotification))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial UserNotificationVo? ToUserNotificationVo(UserNotification? source);

    public static partial List<UserNotificationVo> ToUserNotificationVoList(IEnumerable<UserNotification>? source);

}
