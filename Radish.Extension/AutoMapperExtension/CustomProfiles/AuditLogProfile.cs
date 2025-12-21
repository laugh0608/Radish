using AutoMapper;
using Radish.Model.LogModels;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>
/// 审计日志 AutoMapper 配置
/// </summary>
public class AuditLogProfile : Profile
{
    public AuditLogProfile()
    {
        // AuditLog -> AuditLogVo
        CreateMap<AuditLog, AuditLogVo>();

        // CreateAuditLogDto -> AuditLog
        CreateMap<CreateAuditLogDto, AuditLog>()
            .ForMember(dest => dest.DateTime, opt => opt.MapFrom(src => DateTime.Now))
            .ForMember(dest => dest.Level, opt => opt.MapFrom(src => "Information"));
    }
}
