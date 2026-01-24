using AutoMapper;
using Radish.Model.Models;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>
/// 支付密码映射配置
/// </summary>
public class PaymentPasswordProfile : Profile
{
    public PaymentPasswordProfile()
    {
        // 配置前缀识别
        RecognizeDestinationPrefixes("Vo");

        // UserPaymentPassword -> UserPaymentPasswordVo
        CreateMap<UserPaymentPassword, UserPaymentPasswordVo>()
            .ForMember(dest => dest.VoIsLocked, opt => opt.MapFrom(src =>
                src.LockedUntil.HasValue && src.LockedUntil.Value > DateTime.Now))
            .ForMember(dest => dest.VoLockedRemainingMinutes, opt => opt.MapFrom(src =>
                src.LockedUntil.HasValue && src.LockedUntil.Value > DateTime.Now
                    ? (int)(src.LockedUntil.Value - DateTime.Now).TotalMinutes
                    : 0))
            .ForMember(dest => dest.VoHasPaymentPassword, opt => opt.MapFrom(src =>
                !string.IsNullOrEmpty(src.PasswordHash)))
            .ForMember(dest => dest.VoLastUsedTimeDisplay, opt => opt.Ignore())
            .ForMember(dest => dest.VoLastModifiedTimeDisplay, opt => opt.Ignore())
            .ForMember(dest => dest.VoStrengthLevelDisplay, opt => opt.Ignore())
            .ForMember(dest => dest.VoSecurityStatus, opt => opt.Ignore())
            .ForMember(dest => dest.VoSecuritySuggestions, opt => opt.Ignore())
            .ForMember(dest => dest.VoCreatedAtDisplay, opt => opt.Ignore());
    }
}