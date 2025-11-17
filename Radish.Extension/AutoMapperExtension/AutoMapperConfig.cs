using AutoMapper;

namespace Radish.Extension.AutoMapperExtension;

/// <summary>集中注册 AutoMapper 配置。</summary>
public static class AutoMapperConfig
{
    public static void RegisterProfiles(IMapperConfigurationExpression cfg)
    {
        // cfg.AddProfile<CustomProfile>();
        cfg.AddProfile<RoleProfile>();
        cfg.AddProfile<UserProfile>();
    }
}
