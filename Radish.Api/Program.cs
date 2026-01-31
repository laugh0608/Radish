using System.Text;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.Common.HttpContextTool;
using Radish.Extension;
using Radish.Extension.AutofacExtension;
using Radish.Extension.AutoMapperExtension;
using Radish.Extension.PermissionExtension;
using Radish.Extension.RedisExtension;
using Radish.Extension.Log;
using Radish.Extension.SqlSugarExtension;
using Radish.Extension.OpenApiExtension;
using Radish.Extension.RateLimitExtension;
using Radish.Extension.AuditLogExtension;
using Radish.Extension.ExperienceExtension;
using Radish.Infrastructure.FileStorage;
using Radish.Infrastructure.ImageProcessing;
using Radish.IRepository;
using Radish.IService;
using Radish.Repository;
using Radish.Service;
using Serilog;
using SqlSugar;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Hangfire;
using Hangfire.Storage.SQLite;
using Radish.Service.Jobs;
using Radish.Api.Filters;
using Radish.Api.Hubs;
using Microsoft.IdentityModel.JsonWebTokens;
using Radish.IRepository.Base;
using Radish.IService.Base;
using Radish.Repository.Base;
using Radish.Service.Base;

// -------------- 容器构建阶段 ---------------
var builder = WebApplication.CreateBuilder(args);
// -------------- 容器构建阶段 ---------------

// 🔧 禁用 JWT 默认的 claim type 映射，保持 OIDC 标准 claims（sub, name, role 等）原样
// 这样避免 "sub" 被映射为 "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();

// 使用 Autofac 配置 Host 与容器
builder.Host
    .UseServiceProviderFactory(new AutofacServiceProviderFactory())
    .ConfigureContainer<ContainerBuilder>(containerBuilder =>
    {
        containerBuilder.RegisterModule(new AutofacModuleRegister());
        // 使用 Assembly.GetExecutingAssembly() 避免与 Radish.Auth 的 Program 类冲突
        containerBuilder.RegisterModule(new AutofacPropertyModuleReg(System.Reflection.Assembly.GetExecutingAssembly()));
    }).ConfigureAppConfiguration((hostingContext, config) =>
    {
        hostingContext.Configuration.ConfigureApplication(); // 1. 绑定 InternalApp 扩展中的配置

        // 配置文件统一从输出目录（AppContext.BaseDirectory）读取，避免受工作目录影响
        var basePath = AppContext.BaseDirectory;

        config.Sources.Clear();
        config.AddJsonFile(Path.Combine(basePath, "appsettings.json"), optional: true, reloadOnChange: false);
        config.AddJsonFile(Path.Combine(basePath, $"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json"),
            optional: true, reloadOnChange: false);
        config.AddJsonFile(Path.Combine(basePath, "appsettings.Local.json"), optional: true, reloadOnChange: false);
        // config.AddConfigurationApollo("appsettings.apollo.json");
    });
// 2. 绑定 InternalApp 扩展中的环境变量
builder.ConfigureApplication();
// 激活 Autofac 影响的 IControllerActivator 控制器激活器，这一行的意义就是把 Controller 类也就是控制器注册为 Service 服务
builder.Services.Replace(ServiceDescriptor.Transient<IControllerActivator, ServiceBasedControllerActivator>());
// 注册跨域规则
const string corsPolicyName = "FrontendCors";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policyBuilder =>
    {
        if (allowedOrigins.Length > 0)
        {
            policyBuilder.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();  // SignalR 需要 AllowCredentials
        }
        else
        {
            // 开发环境：允许任意来源但不能同时使用 AllowCredentials
            // 生产环境必须配置 Cors:AllowedOrigins
            policyBuilder.SetIsOriginAllowed(_ => true)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

// 本地化配置：统一使用 zh / en，与前端保持一致
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

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

    // 优先从 Accept-Language 读取语言
    options.RequestCultureProviders.Insert(0, new AcceptLanguageHeaderRequestCultureProvider());
});

// 注册 Controller 控制器
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // 🚀 配置 JSON 序列化使用 camelCase 命名策略（前端期望 authorName，而不是 AuthorName）
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new Int64ToStringConverter());
        options.JsonSerializerOptions.Converters.Add(new NullableInt64ToStringConverter());
    });
// 注册健康检查
builder.Services.AddHealthChecks();
// 配置 API 版本控制
builder.Services.AddApiVersioning(options =>
{
    // 报告支持的 API 版本
    options.ReportApiVersions = true;
    // 当客户端未指定版本时，使用默认版本
    options.AssumeDefaultVersionWhenUnspecified = true;
    // 设置默认版本为 1
    options.DefaultApiVersion = new ApiVersion(1);
    // 使用 URL 路径版本控制（推荐）
    // 例如：/api/v1/User/GetUserList
})
.AddMvc() // 添加 MVC 支持
.AddApiExplorer(options =>
{
    // API 版本在 URL 路径中的格式：'v'major[.minor][-status]
    options.GroupNameFormat = "'v'VVV";
    // 自动替换 Controller 路由中的版本占位符
    options.SubstituteApiVersionInUrl = true;
});
// 配置 OpenAPI 和 Scalar 文档
builder.Services.AddScalarSetup();
// 注册 AddAutoMapper 服务
builder.Services.AddAutoMapperSetup(builder.Configuration);
// builder.Services.AddAutoMapper(typeof(AutoMapperConfig));
// AutoMapperConfig.RegisterMappings();
// 注册 AppSetting 自定义扩展服务
builder.Services.AddSingleton(new AppSettingsTool(builder.Configuration));
// 注册 AppSetting 自定义扩展的扩展 ConfigurableOptions 服务
builder.Services.AddAllOptionRegister();
// 注册文件存储和图片处理服务
// 根据 FileStorage:Type 动态选择存储后端（Local/MinIO/OSS）
builder.Services.AddSingleton<IFileStorage>(sp => FileStorageFactory.Create(sp));
// 图片处理：根据 ImageProcessing:UseRustExtension 动态选择实现（C# ImageSharp / Rust）
builder.Services.AddSingleton<ImageProcessorFactory>();
builder.Services.AddScoped<IImageProcessor>(sp =>
{
    var factory = sp.GetRequiredService<ImageProcessorFactory>();
    return factory.Create();
});
// 注册缓存相关服务
builder.Services.AddCacheSetup();
// 注册速率限制服务
builder.Services.AddRateLimitSetup();
// 注册审计日志服务
builder.Services.AddAuditLogSetup();
// 注册经验值计算器服务
builder.Services.AddExperienceCalculator(builder.Configuration);

// 注册 SignalR 服务
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = 32 * 1024; // 32 KB
}).AddJsonProtocol(options =>
{
    // 与 Controller 的 JSON 策略保持一致：
    // - long / long? 以字符串形式传输，避免 JS 精度丢失
    // - 允许从字符串读取 long 参数（客户端 invoke 时常以 string 传入）
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.PayloadSerializerOptions.Converters.Add(new Int64ToStringConverter());
    options.PayloadSerializerOptions.Converters.Add(new NullableInt64ToStringConverter());
});

// 注册通知推送服务（基于 SignalR）
builder.Services.AddScoped<INotificationPushService, Radish.Api.Services.NotificationPushService>();

// 注册 SqlSugar 服务
builder.Services.AddSqlSugarSetup();
// 增强 SqlSugar 的雪花 ID 算法，防止重复
var snowflakeSection = builder.Configuration.GetSection("Snowflake");
SnowFlakeSingle.WorkId = snowflakeSection.GetValue<int>("WorkId");
SnowFlakeSingle.DatacenterId = snowflakeSection.GetValue<int>("DataCenterId");
// 注册泛型仓储与服务，AddScoped() 汇报模式，每次请求的时候注入
builder.Services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
builder.Services.AddScoped(typeof(IBaseService<,>), typeof(BaseService<,>));

// 注册 OpenIddict DbContext（用于客户端管理 API，与 Auth 项目共享数据库）
var openIddictConnectionString = builder.Configuration.GetConnectionString("OpenIddict");
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
    Directory.CreateDirectory(dbDirectory);
    var dbPath = Path.Combine(dbDirectory, "Radish.OpenIddict.db");
    openIddictConnectionString = $"Data Source={dbPath}";
}
builder.Services.AddDbContext<Radish.Auth.OpenIddict.AuthOpenIddictDbContext>(options =>
{
    options.UseSqlite(openIddictConnectionString);
});

// 注册 OpenIddict Core（仅用于客户端管理，不启用 Server）
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<Radish.Auth.OpenIddict.AuthOpenIddictDbContext>();
    });

// 注册 JWT 认证服务（使用 Radish.Auth 作为 OIDC 授权服务器）
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Authority 指向 Gateway 对外暴露的认证服务器地址
        options.Authority = "http://localhost:5200";
        //options.Audience = "radish-api";
        options.RequireHttpsMetadata = false;

        // SignalR (WebSocket) 在浏览器端无法稳定携带 Authorization Header，
        // 会把 token 放在 query string 的 access_token 上；这里需要显式取出来。
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"].ToString();
                var path = context.HttpContext.Request.Path;

                Log.Information("[JWT] OnMessageReceived - Path: {Path}, HasToken: {HasToken}",
                    path, !string.IsNullOrWhiteSpace(accessToken));

                if (!string.IsNullOrWhiteSpace(accessToken)
                    && path.StartsWithSegments("/hub/notification"))
                {
                    context.Token = accessToken;
                    Log.Information("[JWT] 从 query string 提取 token 成功");
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var path = context.HttpContext.Request.Path;
                var userId = context.Principal?.FindFirst("sub")?.Value;
                Log.Information("[JWT] OnTokenValidated - Path: {Path}, UserId: {UserId}", path, userId);
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                var path = context.HttpContext.Request.Path;
                Log.Error(context.Exception, "[JWT] OnAuthenticationFailed - Path: {Path}, Error: {Error}",
                    path, context.Exception.Message);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var path = context.HttpContext.Request.Path;
                Log.Warning("[JWT] OnChallenge - Path: {Path}, Error: {Error}, ErrorDescription: {ErrorDescription}",
                    path, context.Error, context.ErrorDescription);
                return Task.CompletedTask;
            }
        };

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            // 先关闭 Audience 校验，确认 token 其余部分没问题
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = System.TimeSpan.Zero,
            // 指定 NameClaimType 为 OIDC 标准的 "sub"（用于用户标识）
            NameClaimType = "sub",
            // 指定 role claim 类型为 OIDC 标准的 "role"
            RoleClaimType = "role"
        };
    });
// 注册 JWT 授权方案，核心是通过解析请求头中的 JWT Token，然后匹配策略中的 key 和字段值
builder.Services.AddAuthorizationBuilder()
           // Client 授权方案，基于 scope 控制访问 radish-api
           // OpenIddict 默认会把多个 scope 以空格拼成一个字符串（例如："openid profile radish-api"），因此这里需要按空格拆分判断
           .AddPolicy("Client", policy => policy.RequireAssertion(ctx =>
           {
               // 【调试】输出所有 claims，用于诊断授权失败问题
               var allClaims = ctx.User.Claims.Select(c => $"{c.Type}={c.Value}").ToArray();
               Log.Information("[Client Policy] 所有 Claims: {Claims}", string.Join(", ", allClaims));

               var scopeClaims = ctx.User.FindAll("scope").ToList();
               Log.Information("[Client Policy] 找到 {Count} 个 scope claims", scopeClaims.Count);

               foreach (var claim in scopeClaims)
               {
                   Log.Information("[Client Policy] scope claim value: {Value}", claim.Value);

                   if (string.IsNullOrWhiteSpace(claim.Value))
                       continue;

                   var scopes = claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                   foreach (var s in scopes)
                   {
                       Log.Information("[Client Policy] 检查 scope: {Scope}", s);
                       if (string.Equals(s, "radish-api", StringComparison.Ordinal))
                       {
                           Log.Information("[Client Policy] ✓ 找到 radish-api scope，授权成功");
                           return true;
                       }
                   }
               }

               Log.Warning("[Client Policy] ✗ 未找到 radish-api scope，授权失败");
               return false;
           }).Build())
           // System 授权方案，RequireRole 方式
           .AddPolicy("System", policy => policy.RequireRole("System").Build())
           // SystemOrAdmin 授权方案，RequireRole 方式
           .AddPolicy("SystemOrAdmin", policy => policy.RequireRole("System", "Admin").Build())
           // 自定义授权策略
           .AddPolicy("RadishAuthPolicy", policy => policy.Requirements.Add(new PermissionRequirement()));
// 注册自定义授权策略中间件
builder.Services.AddScoped<IAuthorizationHandler, PermissionRequirementHandler>();
// 注册 PermissionRequirement 鉴权类
builder.Services.AddSingleton(new PermissionRequirement());
// 注册 HttpContext 上下文服务
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
// 注册 HttpContext 获取用户信息服务
builder.Services.AddScoped<IHttpContextUser, HttpContextUser>();

// 注册 Hangfire 服务
var hangfireConnectionString = builder.Configuration["Hangfire:ConnectionString"] ?? "Data Source=DataBases/Radish.Hangfire.db";
// 提取数据库文件路径（Hangfire.Storage.SQLite 需要文件路径,不是连接字符串）
var hangfireDbPath = hangfireConnectionString.Replace("Data Source=", "").Trim();

// 如果是相对路径,转换为绝对路径
if (!Path.IsPathRooted(hangfireDbPath))
{
    // 查找解决方案根目录
    var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
    while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "Radish.slnx")))
    {
        currentDir = currentDir.Parent;
    }
    var solutionRoot = currentDir?.FullName ?? AppContext.BaseDirectory;
    hangfireDbPath = Path.Combine(solutionRoot, hangfireDbPath);
}

// 确保数据库目录存在
var hangfireDbDirectory = Path.GetDirectoryName(hangfireDbPath);
if (!string.IsNullOrEmpty(hangfireDbDirectory) && !Directory.Exists(hangfireDbDirectory))
{
    Directory.CreateDirectory(hangfireDbDirectory);
}

builder.Services.AddHangfire(config =>
{
    // Hangfire.Storage.SQLite 使用文件路径,不是连接字符串
    config.UseSQLiteStorage(hangfireDbPath);
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180);
    config.UseSimpleAssemblyNameTypeSerializer();
    config.UseRecommendedSerializerSettings();
});

builder.Services.AddHangfireServer();

// 注册 Job 类
builder.Services.AddScoped<FileCleanupJob>();
builder.Services.AddScoped<CommentHighlightJob>();
builder.Services.AddScoped<RetentionRewardJob>();
builder.Services.AddScoped<ShopJob>();

// 注册 Serilog 服务
builder.Host.AddSerilogSetup();

// -------------- App 初始化阶段 ---------------
var app = builder.Build();
// -------------- App 初始化阶段 ---------------

// 3. 绑定 InternalApp 扩展中的服务
app.ConfigureApplication();
// 4. 启动 InternalApp 扩展中的 App
app.UseApplicationSetup();
app.UseDefaultFiles();
app.MapStaticAssets();
app.UseStaticFiles();

// 配置上传文件的静态文件服务
var uploadsPath = builder.Configuration.GetSection("FileStorage:Local:BasePath").Value ?? "DataBases/Uploads";
var uploadsUrl = builder.Configuration.GetSection("FileStorage:Local:BaseUrl").Value ?? "/uploads";
var uploadsFullPath = Path.IsPathRooted(uploadsPath)
    ? uploadsPath
    : Path.Combine(app.Environment.ContentRootPath, uploadsPath);

// 确保 uploads 目录存在
Directory.CreateDirectory(uploadsFullPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsFullPath),
    RequestPath = uploadsUrl
});

// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
// }
app.UseCors(corsPolicyName);

// 配置请求本地化（根据 Accept-Language 设置 Culture）
var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(localizationOptions.Value);

// 配置 Hangfire Dashboard
var dashboardEnabled = builder.Configuration.GetValue<bool>("Hangfire:Dashboard:Enable", true);
if (dashboardEnabled)
{
    var routePrefix = builder.Configuration["Hangfire:Dashboard:RoutePrefix"] ?? "/hangfire";
    app.UseHangfireDashboard(routePrefix, new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() }
    });
}

// 配置 Scalar UI
app.UseScalarUI("/scalar");

// 将旧路径 /api/docs 永久重定向到新的 /scalar
app.MapGet("/api/docs", () => Results.Redirect("/scalar", permanent: true)).ExcludeFromDescription();
app.MapGet("/api/docs/{**catchAll}", () => Results.Redirect("/scalar", permanent: true)).ExcludeFromDescription();

// 将 API 根路径重定向到 Scalar 文档
app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();
// 先认证，再授权
app.UseAuthentication();
app.UseAuthorization();
// 启用速率限制中间件（在授权之后，路由之前）
app.UseRateLimitSetup();
// 启用审计日志中间件（在授权之后，速率限制之后）
app.UseAuditLogSetup();

// 映射 SignalR Hub 端点
app.MapHub<NotificationHub>("/hub/notification");

app.MapControllers();
// 映射健康检查端点
app.MapHealthChecks("/health");
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/api/health");

// 输出项目启动标识（使用 Serilog，与 Gateway 风格统一）
app.Lifetime.ApplicationStarted.Register(() =>
{
    var urls = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : "未配置";

    Log.Information("====================================");
    Log.Information("   ____           _ _     _");
    Log.Information("  |  _ \\ __ _  __| (_)___| |__");
    Log.Information("  | |_) / _` |/ _` | / __| '_ \\");
    Log.Information("  |  _ < (_| | (_| | \\__ \\ | | |");
    Log.Information("  |_| \\\\__,_|\\__,_|_|___/_| |_|");
    Log.Information("        Radish.Api --by luobo");
    Log.Information("====================================");
    Log.Information("环境: {Environment}", app.Environment.EnvironmentName);
    Log.Information("监听地址: {Urls}", urls);
    Log.Information("CORS 允许来源: {Origins}", string.Join(", ", allowedOrigins));
});

// 注册 Hangfire 定时任务
var fileCleanupConfig = builder.Configuration.GetSection("Hangfire:FileCleanup");

// 1. 软删除文件清理任务
if (fileCleanupConfig.GetValue<bool>("DeletedFiles:Enable", true))
{
    var retentionDays = fileCleanupConfig.GetValue<int>("DeletedFiles:RetentionDays", 30);
    var schedule = fileCleanupConfig["DeletedFiles:Schedule"] ?? "0 3 * * *";

    RecurringJob.AddOrUpdate<FileCleanupJob>(
        "cleanup-deleted-files",
        job => job.CleanupDeletedFilesAsync(retentionDays),
        schedule,
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Local
        });

    Log.Information("[Hangfire] 已注册定时任务: cleanup-deleted-files (保留 {Days} 天, 计划: {Schedule})", retentionDays, schedule);
}

// 2. 临时文件清理任务
if (fileCleanupConfig.GetValue<bool>("TempFiles:Enable", true))
{
    var retentionHours = fileCleanupConfig.GetValue<int>("TempFiles:RetentionHours", 2);
    var schedule = fileCleanupConfig["TempFiles:Schedule"] ?? "0 * * * *";

    RecurringJob.AddOrUpdate<FileCleanupJob>(
        "cleanup-temp-files",
        job => job.CleanupTempFilesAsync(retentionHours),
        schedule,
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Local
        });

    Log.Information("[Hangfire] 已注册定时任务: cleanup-temp-files (保留 {Hours} 小时, 计划: {Schedule})", retentionHours, schedule);
}

// 3. 回收站清理任务
if (fileCleanupConfig.GetValue<bool>("RecycleBin:Enable", true))
{
    var retentionDays = fileCleanupConfig.GetValue<int>("RecycleBin:RetentionDays", 90);
    var schedule = fileCleanupConfig["RecycleBin:Schedule"] ?? "0 4 * * *";

    RecurringJob.AddOrUpdate<FileCleanupJob>(
        "cleanup-recycle-bin",
        job => job.CleanupRecycleBinAsync(retentionDays),
        schedule,
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Local
        });

    Log.Information("[Hangfire] 已注册定时任务: cleanup-recycle-bin (保留 {Days} 天, 计划: {Schedule})", retentionDays, schedule);
}

// 4. 孤立附件清理任务
if (fileCleanupConfig.GetValue<bool>("OrphanAttachments:Enable", true))
{
    var retentionHours = fileCleanupConfig.GetValue<int>("OrphanAttachments:RetentionHours", 24);
    var schedule = fileCleanupConfig["OrphanAttachments:Schedule"] ?? "0 5 * * *";

    RecurringJob.AddOrUpdate<FileCleanupJob>(
        "cleanup-orphan-attachments",
        job => job.CleanupOrphanAttachmentsAsync(retentionHours),
        schedule,
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Local
        });

    Log.Information("[Hangfire] 已注册定时任务: cleanup-orphan-attachments (保留 {Hours} 小时, 计划: {Schedule})", retentionHours, schedule);
}

// 注册分片上传会话清理任务
{
    var schedule = "0 6 * * *"; // 每天凌晨 6 点

    RecurringJob.AddOrUpdate<IChunkedUploadService>(
        "cleanup-expired-upload-sessions",
        service => service.CleanupExpiredSessionsAsync(),
        schedule,
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Local
        });

    Log.Information("[Hangfire] 已注册定时任务: cleanup-expired-upload-sessions (计划: {Schedule})", schedule);
}

// 注册文件访问令牌清理任务
{
    var schedule = "0 7 * * *"; // 每天凌晨 7 点

    RecurringJob.AddOrUpdate<IFileAccessTokenService>(
        "cleanup-expired-access-tokens",
        service => service.CleanupExpiredTokensAsync(),
        schedule,
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Local
        });

    Log.Information("[Hangfire] 已注册定时任务: cleanup-expired-access-tokens (计划: {Schedule})", schedule);
}

// 神评/沙发统计任务
var commentHighlightConfig = builder.Configuration.GetSection("Hangfire:CommentHighlight");
if (commentHighlightConfig.GetValue<bool>("Enable", true))
{
    var schedule = commentHighlightConfig["Schedule"] ?? "0 1 * * *";

    RecurringJob.AddOrUpdate<CommentHighlightJob>(
        "comment-highlight-stat",
        job => job.ExecuteAsync(null),
        schedule,
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Local
        });

    Log.Information("[Hangfire] 已注册定时任务: comment-highlight-stat (计划: {Schedule})", schedule);
}

// 保留奖励任务（神评/沙发每周奖励）
var retentionRewardConfig = builder.Configuration.GetSection("Hangfire:RetentionReward");
if (retentionRewardConfig.GetValue<bool>("Enable", true))
{
    var schedule = retentionRewardConfig["Schedule"] ?? "0 2 * * 0";

    RecurringJob.AddOrUpdate<RetentionRewardJob>(
        "retention-reward",
        job => job.ExecuteAsync(),
        schedule,
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Local
        });

    Log.Information("[Hangfire] 已注册定时任务: retention-reward (计划: {Schedule})", schedule);
}

// 商城订单超时取消任务
var shopConfig = builder.Configuration.GetSection("Hangfire:Shop");
if (shopConfig.GetValue<bool>("TimeoutOrders:Enable", true))
{
    var timeoutMinutes = shopConfig.GetValue<int>("TimeoutOrders:TimeoutMinutes", 30);
    var schedule = shopConfig["TimeoutOrders:Schedule"] ?? "*/10 * * * *"; // 默认每 10 分钟执行一次

    RecurringJob.AddOrUpdate<ShopJob>(
        "shop-cancel-timeout-orders",
        job => job.CancelTimeoutOrdersAsync(timeoutMinutes),
        schedule,
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Local
        });

    Log.Information("[Hangfire] 已注册定时任务: shop-cancel-timeout-orders (超时: {Timeout} 分钟, 计划: {Schedule})", timeoutMinutes, schedule);
}

// 商城权益过期处理任务
if (shopConfig.GetValue<bool>("ExpiredBenefits:Enable", true))
{
    var schedule = shopConfig["ExpiredBenefits:Schedule"] ?? "0 0 * * *"; // 默认每天 00:00 执行

    RecurringJob.AddOrUpdate<ShopJob>(
        "shop-mark-expired-benefits",
        job => job.MarkExpiredBenefitsAsync(),
        schedule,
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Local
        });

    Log.Information("[Hangfire] 已注册定时任务: shop-mark-expired-benefits (计划: {Schedule})", schedule);
}

// 商城每日统计任务
if (shopConfig.GetValue<bool>("DailyStats:Enable", false))
{
    var schedule = shopConfig["DailyStats:Schedule"] ?? "0 1 * * *"; // 默认每天 01:00 执行

    RecurringJob.AddOrUpdate<ShopJob>(
        "shop-daily-stats",
        job => job.GenerateDailyStatsAsync(),
        schedule,
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Local
        });

    Log.Information("[Hangfire] 已注册定时任务: shop-daily-stats (计划: {Schedule})", schedule);
}

// -------------- App 运行阶段 ---------------
app.Run();
// -------------- App 运行阶段 ---------------

public sealed class Int64ToStringConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (long.TryParse(s, out var value))
            {
                return value;
            }

            throw new JsonException($"无法将字符串 \"{s}\" 解析为 long。");
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt64();
        }

        throw new JsonException($"不支持的 JSON 标记类型 {reader.TokenType}，期望 string 或 number。");
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public sealed class NullableInt64ToStringConverter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrEmpty(s))
            {
                return null;
            }

            if (long.TryParse(s, out var value))
            {
                return value;
            }

            throw new JsonException($"无法将字符串 \"{s}\" 解析为 long?.");
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt64();
        }

        throw new JsonException($"不支持的 JSON 标记类型 {reader.TokenType}，期望 string、number 或 null。");
    }

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value.ToString());
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
