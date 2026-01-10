using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>通知实体和视图模型对象映射配置</summary>
public class NotificationProfile : Profile
{
    public NotificationProfile()
    {
        // Notification -> NotificationVo
        CreateMap<Notification, NotificationVo>();
        CreateMap<NotificationVo, Notification>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore()); // 避免从 VO 覆盖租户 ID

        // UserNotification -> UserNotificationVo
        CreateMap<UserNotification, UserNotificationVo>()
            .ForMember(dest => dest.Notification, opt => opt.Ignore()); // 需要在 Service 中手动填充关联的通知对象
        CreateMap<UserNotificationVo, UserNotification>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore()); // 避免从 VO 覆盖租户 ID

        // NotificationSetting -> NotificationSettingVo
        CreateMap<NotificationSetting, NotificationSettingVo>();
        CreateMap<NotificationSettingVo, NotificationSetting>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore()); // 避免从 VO 覆盖租户 ID
    }
}
