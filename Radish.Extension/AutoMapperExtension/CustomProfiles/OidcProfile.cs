using AutoMapper;
using Radish.Model;
using Radish.Model.OpenIddict;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>OIDC 实体映射配置</summary>
public class OidcProfile : Profile
{
    public OidcProfile()
    {
        // UserClaim 映射
        RecognizeDestinationPrefixes("Vo");
        CreateMap<UserClaim, UserClaimVo>();
        RecognizePrefixes("Vo");
        CreateMap<UserClaimVo, UserClaim>();

        // RadishApplication 映射
        RecognizeDestinationPrefixes("Vo");
        CreateMap<RadishApplication, OidcAppVo>()
            .ForMember(dest => dest.ClientSecret, opt => opt.Ignore()); // 安全考虑：默认不返回密钥
        RecognizePrefixes("Vo");
        CreateMap<OidcAppVo, RadishApplication>();

        // RadishAuthorization 映射
        RecognizeDestinationPrefixes("Vo");
        CreateMap<RadishAuthorization, OidcAuthVo>();
        RecognizePrefixes("Vo");
        CreateMap<OidcAuthVo, RadishAuthorization>();

        // RadishScope 映射
        RecognizeDestinationPrefixes("Vo");
        CreateMap<RadishScope, OidcScopeVo>();
        RecognizePrefixes("Vo");
        CreateMap<OidcScopeVo, RadishScope>();

        // RadishToken 映射
        RecognizeDestinationPrefixes("Vo");
        CreateMap<RadishToken, OidcTokenVo>()
            .ForMember(dest => dest.PayloadPreview, opt => opt.MapFrom(src =>
                src.Payload.Length > 100 ? src.Payload.Substring(0, 100) + "..." : src.Payload)); // 只返回前 100 个字符
        RecognizePrefixes("Vo");
        CreateMap<OidcTokenVo, RadishToken>()
            .ForMember(dest => dest.Payload, opt => opt.Ignore()); // 不允许从 Vo 反向映射 Payload
    }
}
