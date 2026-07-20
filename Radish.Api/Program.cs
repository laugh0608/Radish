using System.Text;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.Common.DbTool;
using Radish.Common.HttpContextTool;
using Radish.Common.OptionTool;
using Radish.Common.HealthTool;
using Microsoft.Extensions.FileProviders;
using Radish.Common.TimeTool;
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
using Hangfire.PostgreSql;
using Hangfire.Storage.SQLite;
using System.Security.Cryptography.X509Certificates;
using Radish.Common.DocumentTool;
using Radish.Service.Jobs;
using Radish.Api.Filters;
using Radish.Api.HealthChecks;
using Radish.Api.Hubs;
using Radish.Api.Services;
using Radish.Api.Security;
using Radish.Api.ErrorHandling;
using Radish.Model;
using Radish.Shared.Constants;
using Microsoft.IdentityModel.JsonWebTokens;
using Radish.IRepository.Base;
using Radish.IService.Base;
using Radish.Repository.Base;
using Radish.Service.Base;

// -------------- 容器构建阶段 ---------------
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<BusinessCalendar>();
// -------------- 容器构建阶段 ---------------

static string ResolveSharedConfigPath(string basePath, string contentRootPath)
{
    var candidates = new[]
    {
        Path.Combine(basePath, "appsettings.Shared.json"),
        Path.Combine(contentRootPath, "appsettings.Shared.json")
    };

    foreach (var candidate in candidates)
    {
        if (File.Exists(candidate))
        {
            return candidate;
        }
    }

    var currentDir = new DirectoryInfo(contentRootPath);
    while (currentDir != null)
    {
        var candidate = Path.Combine(currentDir.FullName, "appsettings.Shared.json");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        currentDir = currentDir.Parent;
    }

    return Path.Combine(contentRootPath, "appsettings.Shared.json");
}

static X509Certificate2 LoadJwtSigningCertificate(
    IConfiguration configuration,
    string basePath,
    string contentRootPath,
    bool waitForCertificate)
{
    var configuredPath = configuration["OpenIddict:Encryption:SigningCertificatePath"];
    var configuredPassword = configuration["OpenIddict:Encryption:SigningCertificatePassword"];

    if (string.IsNullOrWhiteSpace(configuredPath) || string.IsNullOrWhiteSpace(configuredPassword))
    {
        throw new InvalidOperationException(
            "JWT 签名证书配置缺失，请检查 OpenIddict:Encryption:SigningCertificatePath / SigningCertificatePassword。");
    }

    var resolvedPath = ApiJwtRuntimeProfile.ResolveSigningCertificatePath(configuration, basePath, contentRootPath);

    if (waitForCertificate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!File.Exists(resolvedPath) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(500);
        }
    }

    if (!File.Exists(resolvedPath))
    {
        throw new InvalidOperationException($"JWT 签名证书文件不存在: {resolvedPath}");
    }

    return X509CertificateLoader.LoadPkcs12FromFile(resolvedPath, configuredPassword);
}

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
        var sharedConfigPath = ResolveSharedConfigPath(basePath, hostingContext.HostingEnvironment.ContentRootPath);

        config.Sources.Clear();
        config.AddJsonFile(sharedConfigPath, optional: true, reloadOnChange: false);
        config.AddJsonFile(Path.Combine(basePath, "appsettings.json"), optional: true, reloadOnChange: false);
        config.AddJsonFile(Path.Combine(basePath, "appsettings.Local.json"), optional: true, reloadOnChange: false);
        config.AddEnvironmentVariables();
        // config.AddConfigurationApollo("appsettings.apollo.json");
    });
// 2. 绑定 InternalApp 扩展中的环境变量
builder.ConfigureApplication();
// 激活 Autofac 影响的 IControllerActivator 控制器激活器，这一行的意义就是把 Controller 类也就是控制器注册为 Service 服务
builder.Services.Replace(ServiceDescriptor.Transient<IControllerActivator, ServiceBasedControllerActivator>());
// 注册跨域规则
const string corsPolicyName = "FrontendCors";
var allowedOrigins = CorsOriginResolver.ResolveAllowedOrigins(builder.Configuration);
var configuredTimeOptions = builder.Configuration.GetSection("Time").Get<TimeOptions>() ?? new TimeOptions();
var appDefaultTimeZone = TimeZoneResolver.ResolveOrUtc(configuredTimeOptions.DefaultTimeZoneId);
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
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeJsonConverter());
    });
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var fieldErrors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "输入值格式不正确"
                        : error.ErrorMessage)
                    .ToArray());

        return ApiErrorResultFactory.Create(
            context.HttpContext,
            StatusCodes.Status400BadRequest,
            "请求参数验证失败",
            ApiErrorCodes.ValidationFailed,
            "error.common.validation_failed",
            responseData: fieldErrors);
    };
});
builder.Services.AddSingleton<ApiExceptionHandler>();
// 注册健康检查
var apiHealthCheckTags = ApiHostHealthChecks.Tags;
builder.Services.AddApiHostHealthChecks();
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
builder.Services.AddSingleton<IAttachmentUrlResolver, AttachmentUrlResolver>();
builder.Services.AddScoped<IAttachmentReferenceInspector, AttachmentReferenceInspector>();
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
    options.PayloadSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
    options.PayloadSerializerOptions.Converters.Add(new NullableUtcDateTimeJsonConverter());
});

// 注册通知推送服务（基于 SignalR）
builder.Services.AddScoped<INotificationPushService, Radish.Api.Services.NotificationPushService>();
builder.Services.AddScoped<CommentRealtimePushService>();

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
Radish.Auth.OpenIddict.AuthOpenIddictPersistence.AddAuthOpenIddictDbContext(
    builder.Services,
    builder.Configuration);

// 注册 OpenIddict Core（仅用于客户端管理，不启用 Server）
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<Radish.Auth.OpenIddict.AuthOpenIddictDbContext>();
    });

var jwtIssuer = ApiJwtRuntimeProfile.ResolveJwtIssuer(builder.Configuration);
var deploymentJwtValidationEnabled = ApiJwtRuntimeProfile.IsDeploymentJwtValidationEnabled(builder.Configuration);
var jwtValidationMode = "authority";
var jwtValidationTarget = ApiJwtRuntimeProfile.LocalAuthority;
X509Certificate2? jwtSigningCertificate = null;

if (deploymentJwtValidationEnabled)
{
    if (string.IsNullOrWhiteSpace(jwtIssuer))
    {
        throw new InvalidOperationException("部署态 JWT 验签缺少 Issuer，请检查 RADISH_PUBLIC_URL 或 OpenIddict:Server:Issuer。");
    }

    jwtSigningCertificate = LoadJwtSigningCertificate(
        builder.Configuration,
        AppContext.BaseDirectory,
        builder.Environment.ContentRootPath,
        waitForCertificate: true);
    jwtValidationMode = "local-certificate";
    jwtValidationTarget = $"{jwtIssuer} | {jwtSigningCertificate.Subject}";
}

// 注册 JWT 认证服务（使用 Radish.Auth 作为 OIDC 授权服务器）
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (jwtSigningCertificate != null && !string.IsNullOrWhiteSpace(jwtIssuer))
        {
            options.RequireHttpsMetadata = false;
        }
        else
        {
            // IDE 本地开发时回退到旧的 Authority 模式；部署态统一改为共享签名证书本地验签。
            options.Authority = "http://localhost:5200";
            options.RequireHttpsMetadata = false;
        }

        // SignalR (WebSocket) 在浏览器端无法稳定携带 Authorization Header，
        // 会把 token 放在 query string 的 access_token 上；这里需要显式取出来。
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"].ToString();
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrWhiteSpace(accessToken)
                    && (path.StartsWithSegments("/hub/notification") ||
                        path.StartsWithSegments("/hub/chat") ||
                        path.StartsWithSegments("/hub/comment")))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                var path = context.HttpContext.Request.Path;
                Log.Warning(
                    "[JWT] AuthenticationFailed - Path: {Path}, ErrorType: {ErrorType}, TraceId: {TraceId}",
                    path,
                    context.Exception.GetType().Name,
                    context.HttpContext.TraceIdentifier);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var path = context.HttpContext.Request.Path;
                Log.Warning(
                    "[JWT] Challenge - Path: {Path}, Error: {Error}, TraceId: {TraceId}",
                    path,
                    context.Error,
                    context.HttpContext.TraceIdentifier);

                if (!ApiErrorResultFactory.IsMessageModelApiRequest(context.HttpContext))
                {
                    return Task.CompletedTask;
                }

                context.HandleResponse();
                return ApiErrorResultFactory.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    "请先登录后再继续操作",
                    ApiErrorCodes.Unauthorized,
                    "error.auth.unauthorized",
                    cancellationToken: context.HttpContext.RequestAborted);
            },
            OnForbidden = context =>
            {
                if (!ApiErrorResultFactory.IsMessageModelApiRequest(context.HttpContext))
                {
                    return Task.CompletedTask;
                }

                return ApiErrorResultFactory.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status403Forbidden,
                    "当前账号无权执行此操作",
                    ApiErrorCodes.Forbidden,
                    "error.auth.forbidden",
                    cancellationToken: context.HttpContext.RequestAborted);
            }
        };

        options.TokenValidationParameters = ApiJwtValidationPolicy.Create(
            jwtIssuer,
            jwtSigningCertificate != null ? new X509SecurityKey(jwtSigningCertificate) : null);
    });
// 注册 JWT 授权方案，核心是通过解析请求头中的 JWT Token，然后匹配策略中的 key 和字段值
builder.Services.AddAuthorizationBuilder()
           // Client 授权方案，基于 scope 控制访问 radish-api
           // OpenIddict 默认会把多个 scope 以空格拼成一个字符串（例如："openid profile radish-api"），因此这里需要按空格拆分判断
           .AddPolicy(AuthorizationPolicies.Client, policy => policy.RequireAssertion(ctx =>
           {
               return UserClaimReader.HasScope(ctx.User, UserScopes.RadishApi);
           }).Build())
           // System 授权方案，RequireRole 方式
           .AddPolicy(AuthorizationPolicies.System, policy => policy.RequireRole(UserRoles.System).Build())
           // SystemOrAdmin 授权方案，RequireRole 方式
           .AddPolicy(AuthorizationPolicies.SystemOrAdmin, policy => policy.RequireRole(UserRoles.System, UserRoles.Admin).Build())
           // 自定义授权策略
           .AddPolicy(AuthorizationPolicies.RadishAuthPolicy, policy => policy.Requirements.Add(new PermissionRequirement()));
// 注册自定义授权策略中间件
builder.Services.AddScoped<IAuthorizationHandler, PermissionRequirementHandler>();
// 注册 PermissionRequirement 鉴权类
builder.Services.AddSingleton(new PermissionRequirement());
// 注册 HttpContext 上下文服务
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
// 注册 HttpContext 获取用户信息服务
builder.Services.AddScoped<IClaimsPrincipalNormalizer, ClaimsPrincipalNormalizer>();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddScoped<IHttpContextUser, HttpContextUser>();

// 注册 Hangfire 服务
var hangfireDatabase = RuntimeDatabaseConfigResolver.Resolve(
    builder.Configuration,
    "Hangfire",
    null,
    "Radish.Hangfire.db");

builder.Services.AddHangfire(config =>
{
    if (hangfireDatabase.DbType == DataBaseType.PostgreSql)
    {
        config.UsePostgreSqlStorage(options =>
            options.UseNpgsqlConnection(hangfireDatabase.ConnectionString));
    }
    else
    {
        var hangfireDbPath = RuntimeDatabaseConfigResolver.ResolveSqliteFilePath(
            hangfireDatabase.ConnectionString,
            "Radish.Hangfire.db");
        config.UseSQLiteStorage(hangfireDbPath);
    }

    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180);
    config.UseSimpleAssemblyNameTypeSerializer();
    config.UseRecommendedSerializerSettings();
});

var configuredHangfireWorkerCount = builder.Configuration.GetValue<int?>("Hangfire:Server:WorkerCount");
builder.Services.AddHangfireServer(options =>
{
    if (configuredHangfireWorkerCount is > 0)
    {
        options.WorkerCount = configuredHangfireWorkerCount.Value;
    }
    else if (hangfireDatabase.DbType == DataBaseType.Sqlite)
    {
        options.WorkerCount = 1;
    }
});

// 注册 Job 类
builder.Services.AddScoped<FileCleanupJob>();
builder.Services.AddScoped<CommentHighlightJob>();
builder.Services.AddScoped<RetentionRewardJob>();
builder.Services.AddScoped<ShopJob>();
builder.Services.AddScoped<Radish.Api.Services.ReliableOutboxDispatcherJob>();
builder.Services.AddScoped<Radish.Api.Services.ReliableOutboxExecutionJob>();
builder.Services.AddScoped<NotificationInboxCleanupJob>();
builder.Services.AddScoped<ChatMessageReactionOperationCleanupJob>();

// 注册 Serilog 服务
builder.Host.AddSerilogSetup();

// -------------- App 初始化阶段 ---------------
var app = builder.Build();
// -------------- App 初始化阶段 ---------------

Log.Information("[JWT] 初始化验签模式: {Mode}, 目标: {Target}", jwtValidationMode, jwtValidationTarget);

// 3. 绑定 InternalApp 扩展中的服务
app.ConfigureApplication();
// 4. 启动 InternalApp 扩展中的 App
app.UseApplicationSetup();
app.UseDefaultFiles();
app.MapStaticAssets();
app.UseStaticFiles();

// 用户附件只允许通过 /_assets/attachments/* 的受控下载链访问。
// /uploads 仅保留版本内置的可信默认图标，不再暴露整个用户上传根目录。
var uploadsPath = builder.Configuration.GetSection("FileStorage:Local:BasePath").Value ?? "DataBases/Uploads";
var uploadsUrl = builder.Configuration.GetSection("FileStorage:Local:BaseUrl").Value ?? "/uploads";
var uploadsFullPath = Path.IsPathRooted(uploadsPath)
    ? Path.GetFullPath(uploadsPath)
    : Path.GetFullPath(Path.Combine(Radish.Common.CoreTool.AppPathTool.GetSolutionRootOrBasePath(), uploadsPath));
var trustedDefaultIconPath = Path.Combine(uploadsFullPath, "DefaultIco");
var normalizedUploadsUrl = uploadsUrl.Trim().Trim('/');
var trustedDefaultIconRequestPath = string.IsNullOrWhiteSpace(normalizedUploadsUrl)
    ? "/DefaultIco"
    : $"/{normalizedUploadsUrl}/DefaultIco";

Directory.CreateDirectory(trustedDefaultIconPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(trustedDefaultIconPath),
    RequestPath = trustedDefaultIconRequestPath
});

var documentOptions = app.Services.GetRequiredService<IOptions<DocumentOptions>>().Value;
if (documentOptions.ShowBuiltInDocs)
{
    var builtInDocsRoot = Path.IsPathRooted(documentOptions.BuiltInDocsPath)
        ? documentOptions.BuiltInDocsPath
        : Path.Combine(Radish.Common.CoreTool.AppPathTool.GetSolutionRootOrBasePath(), documentOptions.BuiltInDocsPath);

    if (Directory.Exists(builtInDocsRoot))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new BuiltInDocumentStaticAssetFileProvider(new PhysicalFileProvider(builtInDocsRoot)),
            RequestPath = documentOptions.StaticAssetsRequestPath
        });
    }
}

// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
// }
app.UseCors(corsPolicyName);

// 配置请求本地化（根据 Accept-Language 设置 Culture）
var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(localizationOptions.Value);

// 只处理正式 API JSON 请求；协议端点、文件流、Hub 和宿主页面保持各自响应契约。
app.UseApiExceptionHandler();

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
app.MapHub<ChatHub>("/hub/chat");
app.MapHub<CommentHub>("/hub/comment");

app.MapControllers();
// 映射健康检查端点
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = ApiHostHealthChecks.IsMinimal,
});
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResponseWriter = (context, report) => StructuredHealthCheckResponseWriter.WriteJsonAsync(context, report, apiHealthCheckTags),
});
app.MapHealthChecks("/api/health", new HealthCheckOptions
{
    Predicate = ApiHostHealthChecks.IsMinimal,
});

// 输出项目启动标识（使用 Serilog，与 Gateway 风格统一）
app.Lifetime.ApplicationStarted.Register(() =>
{
    var urls = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : "未配置";
    var jwtRuntimeSummary = ApiJwtRuntimeProfile.BuildStartupSummary(
        builder.Configuration,
        AppContext.BaseDirectory,
        builder.Environment.ContentRootPath);

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
    Log.Information("默认时区: {TimeZone}", TimeZoneResolver.NormalizeToDisplayId(configuredTimeOptions.DefaultTimeZoneId));
    Log.Information("JWT 验签模式: {Mode}", jwtRuntimeSummary.ValidationMode);
    Log.Information("JWT 验签目标: {Target}", jwtRuntimeSummary.ValidationTarget);
    Log.Information("JWT Issuer 解析: {Issuer}", jwtRuntimeSummary.IssuerSummary);
    Log.Information("JWT signing 证书: {Certificate}", jwtRuntimeSummary.SigningCertificateSummary);
});

// 注册 Hangfire 定时任务
RecurringJob.AddOrUpdate<Radish.Api.Services.ReliableOutboxDispatcherJob>(
    "reliable-outbox-dispatch",
    job => job.DispatchAsync(50),
    "*/1 * * * *",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc
    });

Log.Information("[Hangfire] 已注册可靠 Outbox 分派任务: reliable-outbox-dispatch");

RecurringJob.AddOrUpdate<ChatMessageReactionOperationCleanupJob>(
    "cleanup-chat-reaction-operations",
    job => job.ExecuteAsync(500),
    "15 4 * * *",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc
    });

Log.Information("[Hangfire] 已注册 Chat 消息回应幂等事实清理任务: cleanup-chat-reaction-operations");

var notificationCleanupConfig = builder.Configuration.GetSection("Hangfire:NotificationInboxCleanup");
if (notificationCleanupConfig.GetValue<bool>("Enable", true))
{
    var schedule = notificationCleanupConfig["Schedule"] ?? "30 3 * * *";
    var batchSize = notificationCleanupConfig.GetValue<int>("BatchSize", 200);
    var softRelationLimitPerUser = notificationCleanupConfig.GetValue<int>("SoftRelationLimitPerUser", 5000);
    RecurringJob.AddOrUpdate<NotificationInboxCleanupJob>(
        "cleanup-notification-inbox",
        job => job.ExecuteAsync(batchSize, softRelationLimitPerUser),
        schedule,
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Utc
        });

    Log.Information(
        "[Hangfire] 已注册通知收件箱清理任务: cleanup-notification-inbox (批次: {BatchSize}, 软上限: {SoftLimit}, 计划: {Schedule})",
        batchSize,
        softRelationLimitPerUser,
        schedule);
}

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
            TimeZone = appDefaultTimeZone
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
            TimeZone = appDefaultTimeZone
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
            TimeZone = appDefaultTimeZone
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
            TimeZone = appDefaultTimeZone
        });

    Log.Information("[Hangfire] 已注册定时任务: cleanup-orphan-attachments (保留 {Hours} 小时, 计划: {Schedule})", retentionHours, schedule);
}

// 注册分片上传会话清理任务；即使禁用新上传，也要继续回收历史会话。
{
    // 必须短于会话接近到期时仍可剩余的 30 分钟配额宽限，
    // 让终态会话的幂等配额结算在预留自然过期前获得重试机会并保留调度抖动余量。
    var schedule = "*/15 * * * *"; // 每 15 分钟

    RecurringJob.AddOrUpdate<IChunkedUploadService>(
        "cleanup-expired-upload-sessions",
        service => service.CleanupExpiredSessionsAsync(),
        schedule,
        new RecurringJobOptions
        {
            TimeZone = appDefaultTimeZone
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
            TimeZone = appDefaultTimeZone
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
            TimeZone = appDefaultTimeZone
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
            TimeZone = appDefaultTimeZone
        });

    Log.Information("[Hangfire] 已注册定时任务: retention-reward (计划: {Schedule})", schedule);
}

// 论坛抽奖自动开奖任务
var postLotteryConfig = builder.Configuration.GetSection("Hangfire:PostLottery");
if (postLotteryConfig.GetValue<bool>("Enable", true))
{
    var schedule = postLotteryConfig["Schedule"] ?? "*/1 * * * *";
    var batchSize = postLotteryConfig.GetValue<int>("BatchSize", 20);

    RecurringJob.AddOrUpdate<PostLotteryJob>(
        "forum-post-lottery-auto-draw",
        job => job.ExecuteAutoDrawAsync(batchSize),
        schedule,
        new RecurringJobOptions
        {
            TimeZone = appDefaultTimeZone
        });

    Log.Information("[Hangfire] 已注册定时任务: forum-post-lottery-auto-draw (批次: {BatchSize}, 计划: {Schedule})", batchSize, schedule);
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
            TimeZone = appDefaultTimeZone
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
            TimeZone = appDefaultTimeZone
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
            TimeZone = appDefaultTimeZone
        });

    Log.Information("[Hangfire] 已注册定时任务: shop-daily-stats (计划: {Schedule})", schedule);
}

try
{
    using var documentSyncScope = app.Services.CreateScope();
    var sqlSugarClient = documentSyncScope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
    var wikiDocumentTableName = sqlSugarClient.EntityMaintenance.GetEntityInfo<WikiDocument>().DbTableName;
    var wikiDocumentRevisionTableName = sqlSugarClient.EntityMaintenance.GetEntityInfo<WikiDocumentRevision>().DbTableName;
    var builtInSyncSkipReason = WikiBuiltInSyncStartupGuard.GetSkipReason(
        documentOptions.ShowBuiltInDocs,
        sqlSugarClient.DbMaintenance.IsAnyTable(wikiDocumentTableName, false),
        sqlSugarClient.DbMaintenance.IsAnyTable(wikiDocumentRevisionTableName, false));

    if (!string.IsNullOrWhiteSpace(builtInSyncSkipReason))
    {
        Log.Information("固定文档启动同步已跳过: {Reason}", builtInSyncSkipReason);
    }
    else
    {
        var wikiDocumentService = documentSyncScope.ServiceProvider.GetRequiredService<IWikiDocumentService>();
        var builtInSyncSummary = await wikiDocumentService.SyncBuiltInDocumentsAsync();

        if (builtInSyncSummary.IsSkipped)
        {
            Log.Information("固定文档启动同步已跳过: {Reason}", builtInSyncSummary.SkipReason ?? "未提供原因");
        }
        else
        {
            Log.Information(
                "固定文档启动同步完成: Markdown {MarkdownFileCount}, 描述符 {DescriptorCount}, 生成目录 {GeneratedNodeCount}, 同步 {SyncedCount}, 新增 {CreatedCount}, 更新 {UpdatedCount}, 恢复 {RestoredCount}, 父子调整 {ParentAdjustedCount}, 软删除 {SoftDeletedCount}, 跳过 {SkippedCount}",
                builtInSyncSummary.MarkdownFileCount,
                builtInSyncSummary.DescriptorCount,
                builtInSyncSummary.GeneratedNodeCount,
                builtInSyncSummary.SyncedCount,
                builtInSyncSummary.CreatedCount,
                builtInSyncSummary.UpdatedCount,
                builtInSyncSummary.RestoredCount,
                builtInSyncSummary.ParentAdjustedCount,
                builtInSyncSummary.SoftDeletedCount,
                builtInSyncSummary.SkippedCount);
        }
    }
}
catch (Exception ex)
{
    Log.Error(ex, "固定文档启动同步失败");
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

public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public UtcDateTimeJsonConverter()
    {
    }

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = reader.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return default;
            }

            if (DateOnly.TryParseExact(
                    raw,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dateOnly))
            {
                return dateOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            }

            if (!HasExplicitOffset(raw))
            {
                throw new JsonException("绝对时间必须使用带 Z 或 offset 的 ISO 8601 格式。");
            }

            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
            {
                return dto.UtcDateTime;
            }

            throw new JsonException($"无法将字符串 \"{raw}\" 解析为 DateTime。");
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var epoch))
        {
            var dto = epoch > 9999999999
                ? DateTimeOffset.FromUnixTimeMilliseconds(epoch)
                : DateTimeOffset.FromUnixTimeSeconds(epoch);

            return dto.UtcDateTime;
        }

        throw new JsonException($"不支持的 JSON 标记类型 {reader.TokenType}，期望 string 或 number。");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = NormalizeToUtc(value);
        writer.WriteStringValue(utc.ToString("O", CultureInfo.InvariantCulture));
    }

    private DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            // 数据库存储统一为 UTC，SQLite 读取常为 Unspecified，这里按 UTC 解释以避免重复时区换算
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static bool HasExplicitOffset(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.EndsWith('Z') || trimmed.EndsWith('z'))
        {
            return true;
        }

        if (trimmed.Length < 6)
        {
            return false;
        }

        var offsetStart = trimmed.Length - 6;
        return (trimmed[offsetStart] == '+' || trimmed[offsetStart] == '-') &&
               trimmed[offsetStart + 3] == ':' &&
               char.IsDigit(trimmed[offsetStart + 1]) &&
               char.IsDigit(trimmed[offsetStart + 2]) &&
               char.IsDigit(trimmed[offsetStart + 4]) &&
               char.IsDigit(trimmed[offsetStart + 5]);
    }
}

public sealed class NullableUtcDateTimeJsonConverter : JsonConverter<DateTime?>
{
    private readonly UtcDateTimeJsonConverter _inner;

    public NullableUtcDateTimeJsonConverter()
    {
        _inner = new UtcDateTimeJsonConverter();
    }

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        return _inner.Read(ref reader, typeof(DateTime), options);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        _inner.Write(writer, value.Value, options);
    }
}
