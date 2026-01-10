using AutoMapper;
using Radish.Extension.AutoMapperExtension.CustomProfiles;

namespace Radish.Extension.AutoMapperExtension;

/// <summary>集中注册 AutoMapper 配置</summary>
public class AutoMapperConfig
{
    public static void RegisterCustomProfile(IMapperConfigurationExpression cfg)
    {
        cfg.AddProfile<CustomProfile>();
    }
    public static void RegisterProfiles(IMapperConfigurationExpression cfg)
    {
        cfg.AddProfile<RoleProfile>();
        cfg.AddProfile<UserProfile>();
        cfg.AddProfile<AuditSqlLogProfile>();
        cfg.AddProfile<OidcProfile>();
        cfg.AddProfile<ForumProfile>();
        cfg.AddProfile<AttachmentProfile>();
        cfg.AddProfile<CoinProfile>();
        cfg.AddProfile<NotificationProfile>();
    }

    #region 已弃用

    // private ILoggerFactory _loggerFactory;
    // private ILogger _logger;
    // public AutoMapperConfig(ILoggerFactory loggerFactory, ILogger logger)
    // {
    //     _loggerFactory = loggerFactory;
    //     _logger = logger;
    // }
    // public MapperConfiguration RegisterMappings()
    // {
    //     var licenseKey = AppSettingsTool.RadishApp(new string[] {"AutoMapper", "LicenseKey"}).ObjToString();
    //     if (string.IsNullOrWhiteSpace(licenseKey))
    //     {
    //         _logger.LogDebug("AutoMapper LicenseKey is null or empty");
    //     }
    //     return new MapperConfiguration(cfg =>
    //     {
    //         cfg.LicenseKey = licenseKey;
    //         cfg.AddProfile<CustomProfile>();
    //         cfg.AddProfile<RoleProfile>();
    //         cfg.AddProfile<UserProfile>();
    //     }, _loggerFactory);
    // }

    #endregion
}
