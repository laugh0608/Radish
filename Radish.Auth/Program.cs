using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.Auth.OpenIddict;
using Radish.Extension;
using Radish.Extension.AutofacExtension;
using Radish.Extension.AutoMapperExtension;
using Radish.Extension.RedisExtension;
using Radish.Extension.SerilogExtension;
using Radish.Extension.SqlSugarExtension;
using Serilog;
using SqlSugar;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using System.Security.Cryptography.X509Certificates;

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

// SqlSugar（业务数据仍使用 SqlSugar）
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

// 本地化配置：统一使用 zh / en，与前端保持一致
// 不设置 ResourcesPath，让它在类型相同的目录查找资源文件
builder.Services.AddLocalization();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("zh"),
        new CultureInfo("en")
    };

    options.DefaultRequestCulture = new RequestCulture("zh");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    // 语言提供者优先级：Query String > Cookie > Accept-Language
    options.RequestCultureProviders.Clear();
    options.RequestCultureProviders.Add(new QueryStringRequestCultureProvider());
    options.RequestCultureProviders.Add(new CookieRequestCultureProvider());
    options.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());
});

// 配置强类型 Options
builder.Services.AddAllOptionRegister();

// 配置 ForwardedHeaders，让 Auth Server 能识别通过 Gateway 转发的原始请求信息
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                               ForwardedHeaders.XForwardedProto |
                               ForwardedHeaders.XForwardedHost;
    // 信任所有代理（仅开发环境）
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// 添加控制器 + 视图（用于登录页）
builder.Services.AddControllersWithViews();

// OpenIddict 初始化种子数据（使用 EF Core 存储）
builder.Services.AddHostedService<OpenIddictSeedHostedService>();

// 添加认证：Cookie（用于登录页面会话）
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
    });

// OpenIddict 所用 EF Core DbContext（仅承载 OpenIddict 实体）
var openIddictConnectionString = builder.Configuration.GetConnectionString("OpenIddict");

// 如果未配置连接字符串，使用解决方案根目录下的 DataBases 文件夹（与 API 项目共享）
if (string.IsNullOrEmpty(openIddictConnectionString))
{
    // 查找解决方案根目录（包含 Radish.slnx 的目录）
    var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
    while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "Radish.slnx")))
    {
        currentDir = currentDir.Parent;
    }
    var solutionRoot = currentDir?.FullName ?? AppContext.BaseDirectory;
    var dbDirectory = Path.Combine(solutionRoot, "DataBases");
    Directory.CreateDirectory(dbDirectory); // 确保目录存在
    var dbPath = Path.Combine(dbDirectory, "RadishAuth.OpenIddict.db");
    openIddictConnectionString = $"Data Source={dbPath}";
}

builder.Services.AddDbContext<AuthOpenIddictDbContext>(options =>
{
    options.UseSqlite(openIddictConnectionString);
});

var openIddictCertificateSection = builder.Configuration.GetSection("OpenIddict:Encryption");

string ResolveCertificatePath(string? relativePath)
{
    if (string.IsNullOrWhiteSpace(relativePath))
    {
        return string.Empty;
    }

    if (Path.IsPathRooted(relativePath))
    {
        return relativePath;
    }

    return Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, relativePath));
}

X509Certificate2 LoadOpenIddictCertificate(string certificateType)
{
    var certificatePath =
        ResolveCertificatePath(openIddictCertificateSection.GetValue<string>($"{certificateType}CertificatePath"));
    var certificatePassword = openIddictCertificateSection.GetValue<string>($"{certificateType}CertificatePassword");

    if (string.IsNullOrWhiteSpace(certificatePath) || string.IsNullOrWhiteSpace(certificatePassword))
    {
        throw new InvalidOperationException($"OpenIddict {certificateType} 证书配置缺失，请检查 appsettings.");
    }

    if (!File.Exists(certificatePath))
    {
        throw new FileNotFoundException($"未找到 {certificateType} 证书文件：{certificatePath}", certificatePath);
    }

    return X509CertificateLoader.LoadPkcs12FromFile(certificatePath, certificatePassword);
}

// OpenIddict 配置
builder.Services.AddOpenIddict()
    // 注册 OpenIddict Core 服务（使用 EF Core 存储）
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<AuthOpenIddictDbContext>();
    })
    // 注册 OpenIddict Server 服务
    .AddServer(options =>
    {
        // 显式设置 Issuer 为配置中的地址
        var issuer = builder.Configuration.GetValue<string>("OpenIddict:Server:Issuer");
        if (!string.IsNullOrEmpty(issuer))
        {
            options.SetIssuer(new Uri(issuer));
        }

        // 启用 OIDC 端点
        options.SetAuthorizationEndpointUris("/connect/authorize")
               .SetTokenEndpointUris("/connect/token")
               .SetLogoutEndpointUris("/connect/endsession")
               .SetUserInfoEndpointUris("/connect/userinfo")
               .SetIntrospectionEndpointUris("/connect/introspect")
               .SetRevocationEndpointUris("/connect/revoke");

        // 启用授权流程
        options.AllowAuthorizationCodeFlow()
               .AllowRefreshTokenFlow()
               .AllowClientCredentialsFlow();

        // 注册允许的 Scopes
        options.RegisterScopes("openid", "profile", "email", "offline_access", "radish-api");

        // 配置加密和签名证书（默认使用 certs/dev-auth-cert.pfx）
        var useDevelopmentKeys = openIddictCertificateSection.GetValue<bool?>("UseDevelopmentKeys") ?? false;
        if (useDevelopmentKeys)
        {
            options.AddDevelopmentEncryptionCertificate()
                   .AddDevelopmentSigningCertificate();
        }
        else
        {
            options.AddSigningCertificate(LoadOpenIddictCertificate("Signing"))
                   .AddEncryptionCertificate(LoadOpenIddictCertificate("Encryption"));
        }

        // 重要：禁用 access_token 加密，只生成签名 JWT，方便 Api 直接用 JwtBearer 验签
        options.DisableAccessTokenEncryption();

        // 注册 ASP.NET Core 宿主
        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableLogoutEndpointPassthrough()
               .EnableUserInfoEndpointPassthrough()
               .DisableTransportSecurityRequirement(); // 允许 HTTP（仅开发环境）
    });

#endregion

// -------------- App 初始化阶段 ---------------
var app = builder.Build();
// -------------- App 初始化阶段 ---------------

// 确保 OpenIddict 所在的 EF Core 数据库已创建
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthOpenIddictDbContext>();
    db.Database.EnsureCreated();
}

#region 中间件管道

// 3. 绑定 InternalApp 扩展中的服务
app.ConfigureApplication();
// 4. 启动 InternalApp 扩展中的 App
app.UseApplicationSetup();

// ForwardedHeaders 必须在其他中间件之前
app.UseForwardedHeaders();

// HTTPS 重定向（由 Gateway 处理，Auth 服务本身不需要）
// app.UseHttpsRedirection();

// 静态文件
app.UseStaticFiles();

// 配置请求本地化（必须在 UseRouting 之前，确保在路由和控制器执行前设置 Culture）
var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(localizationOptions.Value);

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
