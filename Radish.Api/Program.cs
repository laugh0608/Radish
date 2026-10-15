using Microsoft.AspNetCore.DataProtection;
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
return await RuntimeProcess.RunAsync("api", async () =>
{
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
    using var runtimeLogging = new RuntimeLoggingSession(builder.Configuration, builder.Environment.EnvironmentName, "api");
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
    builder.Services.AddScoped<IUserInteractionRealtimeNotifier, Radish.Api.Services.UserInteractionRealtimeNotifier>();
    builder.Services.AddScoped<IContentModerationRealtimeNotifier, Radish.Api.Services.ContentModerationRealtimeNotifier>();
    builder.Services.AddScoped<CommentRealtimePushService>();
    builder.Services.AddScoped<
        Radish.Api.Services.IClientApplicationQueryService,
        Radish.Api.Services.ClientApplicationQueryService>();

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
    }

    builder.Services.AddScoped<IHangfireDashboardSessionService, HangfireDashboardSessionService>();
    var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
    if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
    {
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(dataProtectionKeysPath)));
    }

    builder.Services.AddAuthentication()
        .AddCookie(HangfireDashboardAuthentication.Scheme, options =>
            HangfireDashboardAuthentication.Configure(options,
                builder.Configuration["Hangfire:Dashboard:RoutePrefix"] ?? "/hangfire"));

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

            options.Events = new ApiJwtBearerEvents();

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
        HangfireRuntimeLogging.Configure(config);
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
    builder.Services.AddScoped<WikiDraftPayloadCleanupJob>();

    // 注册 Serilog 服务
    builder.Host.AddSerilogSetup(runtimeLogging);

    // -------------- App 初始化阶段 ---------------
    var app = builder.Build();
    runtimeLogging.AttachLifetime(app.Lifetime);
    // -------------- App 初始化阶段 ---------------

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

    var startupLog = HostStartupLog.Api(builder.Environment.EnvironmentName, allowedOrigins.Length,
        localSigningCertificateLoaded: jwtSigningCertificate != null);
    using var startupSummaryRegistration = startupLog.Register(app.Lifetime, app.Urls, Log.Logger);

    ApiRecurringJobRegistration.Register(builder.Configuration, appDefaultTimeZone);
    await WikiBuiltInDocumentStartupSync.RunAsync(app.Services, documentOptions, Log.Logger);

    // -------------- App 运行阶段 ---------------
    await app.RunAsync();
    // -------------- App 运行阶段 ---------------
});

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
