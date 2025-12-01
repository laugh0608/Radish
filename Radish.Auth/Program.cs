using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.Extension;
using Radish.Extension.AutofacExtension;
using Radish.Extension.AutoMapperExtension;
using Radish.Extension.RedisExtension;
using Radish.Extension.SerilogExtension;
using Radish.Extension.SqlSugarExtension;
using Serilog;
using SqlSugar;

// -------------- 容器构建阶段 ---------------
var builder = WebApplication.CreateBuilder(args);
// -------------- 容器构建阶段 ---------------

#region 配置加载

// 使用 Autofac 配置 Host 与容器
builder.Host
    .UseServiceProviderFactory(new AutofacServiceProviderFactory())
    .ConfigureContainer<ContainerBuilder>(containerBuilder =>
    {
        containerBuilder.RegisterModule(new AutofacModuleRegister());
        containerBuilder.RegisterModule(new AutofacPropertyModuleReg(typeof(Program).Assembly));
    })
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        hostingContext.Configuration.ConfigureApplication(); // 1. 绑定 InternalApp 扩展中的配置
        config.Sources.Clear();
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        config.AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json",
            optional: true, reloadOnChange: true);
        config.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
        config.AddEnvironmentVariables();
    });

// 2. 绑定 InternalApp 扩展中的环境变量
builder.ConfigureApplication();

#endregion

#region Serilog 日志

// 注册 AppSettingsTool（Serilog 依赖此配置）
builder.Services.AddSingleton(new AppSettingsTool(builder.Configuration));

builder.Host.AddSerilogSetup();

#endregion

#region 服务注册

// AutoMapper
builder.Services.AddAutoMapperSetup(builder.Configuration);

// SqlSugar
builder.Services.AddSqlSugarSetup();

// 配置 Snowflake ID
var snowflakeSection = builder.Configuration.GetSection("Snowflake");
SnowFlakeSingle.WorkId = snowflakeSection.GetValue<int>("WorkId");
SnowFlakeSingle.DatacenterId = snowflakeSection.GetValue<int>("DataCenterId");

// Redis / 内存缓存
builder.Services.AddCacheSetup();

// CORS
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// 配置强类型 Options
builder.Services.AddAllOptionRegister();

// 添加控制器
builder.Services.AddControllers();

// 添加认证：Cookie（用于登录页面会话）
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
    });

// OpenIddict 配置
builder.Services.AddOpenIddict()
    // 注册 OpenIddict Core 服务
    .AddCore(options =>
    {
        // 配置 OpenIddict 使用 SqlSugar（通过自定义 Store）
        // TODO: 实现自定义 Store，暂时使用内存存储
        // options.UseRadishSqlSugarStores();
    })
    // 注册 OpenIddict Server 服务
    .AddServer(options =>
    {
        // 启用 OIDC 授权端点
        options.SetAuthorizationEndpointUris("/connect/authorize")
               .SetTokenEndpointUris("/connect/token")
               .SetUserInfoEndpointUris("/connect/userinfo")
               .SetIntrospectionEndpointUris("/connect/introspect")
               .SetRevocationEndpointUris("/connect/revoke");

        // 启用授权流程
        options.AllowAuthorizationCodeFlow()
               .AllowRefreshTokenFlow()
               .AllowClientCredentialsFlow();

        // 配置加密和签名密钥
        var useDevelopmentKeys = builder.Configuration.GetValue<bool>("OpenIddict:Encryption:UseDevelopmentKeys");
        if (useDevelopmentKeys)
        {
            // 开发环境：使用临时密钥
            options.AddDevelopmentEncryptionCertificate()
                   .AddDevelopmentSigningCertificate();
        }
        else
        {
            // 生产环境：使用固定密钥（从配置读取）
            // TODO: 实现生产环境密钥加载
            throw new InvalidOperationException("生产环境必须配置固定的加密和签名密钥");
        }

        // 注册 ASP.NET Core 宿主
        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough()
               .EnableUserInfoEndpointPassthrough()
               .DisableTransportSecurityRequirement(); // 允许 HTTP（仅开发环境）
    });

#endregion

// -------------- App 初始化阶段 ---------------
var app = builder.Build();
// -------------- App 初始化阶段 ---------------

#region 中间件管道

// 3. 绑定 InternalApp 扩展中的服务
app.ConfigureApplication();
// 4. 启动 InternalApp 扩展中的 App
app.UseApplicationSetup();

// HTTPS 重定向（由 Gateway 处理，Auth 服务本身不需要）
// app.UseHttpsRedirection();

// 静态文件
app.UseStaticFiles();

// 路由
app.UseRouting();

// CORS
app.UseCors();

// 认证
app.UseAuthentication();

// 授权
app.UseAuthorization();

// 控制器路由
app.MapControllers();

// 启动提示（使用 Serilog，与 Gateway/API 风格统一）
app.Lifetime.ApplicationStarted.Register(() =>
{
    var urls = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : "未配置";

    Log.Information("====================================");
    Log.Information("  ____            _ _     _          _         _   _     ");
    Log.Information(" |  _ \\ __ _  __| (_)___| |__      / \\  _   _| |_| |__  ");
    Log.Information(" | |_) / _` |/ _` | / __| '_ \\    / _ \\| | | | __| '_ \\ ");
    Log.Information(" |  _ < (_| | (_| | \\__ \\ | | |  / ___ \\ |_| | |_| | | |");
    Log.Information(" |_| \\_\\__,_|\\__,_|_|___/_| |_| /_/   \\_\\__,_|\\__|_| |_|");
    Log.Information("");
    Log.Information("  OIDC Authentication Server --by luobo");
    Log.Information("====================================");
    Log.Information("环境: {Environment}", app.Environment.EnvironmentName);
    Log.Information("监听地址: {Urls}", urls);
    Log.Information("CORS 允许来源: {Origins}", string.Join(", ", corsOrigins));
});

#endregion

// -------------- App 运行阶段 ---------------
app.Run();
// -------------- App 运行阶段 ---------------
