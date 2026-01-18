using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>通知实体和视图模型对象映射配置</summary>
public class NotificationProfile : Profile
{
    public NotificationProfile()
    {
        // Notification -> NotificationVo (使用前缀识别自动映射)
        RecognizeDestinationPrefixes("Vo");
        CreateMap<Notification, NotificationVo>();
        RecognizePrefixes("Vo");
        CreateMap<NotificationVo, Notification>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore()); // 避免从 VO 覆盖租户 ID

        // UserNotification -> UserNotificationVo (使用前缀识别 + 手动配置特殊字段)
        RecognizeDestinationPrefixes("Vo");
        CreateMap<UserNotification, UserNotificationVo>()
            .ForMember(dest => dest.VoNotification, opt => opt.Ignore()); // 需要在 Service 中手动填充关联的通知对象
        RecognizePrefixes("Vo");
        CreateMap<UserNotificationVo, UserNotification>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore()); // 避免从 VO 覆盖租户 ID

        // NotificationSetting -> NotificationSettingVo (使用前缀识别自动映射)
        RecognizeDestinationPrefixes("Vo");
        CreateMap<NotificationSetting, NotificationSettingVo>();
        RecognizePrefixes("Vo");
        CreateMap<NotificationSettingVo, NotificationSetting>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore()); // 避免从 VO 覆盖租户 ID
    }
}
