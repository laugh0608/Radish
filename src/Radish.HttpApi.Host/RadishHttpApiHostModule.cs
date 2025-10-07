using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using Radish.HealthChecks;
using Radish.MongoDB;
using Radish.MultiTenancy;
using Scalar.AspNetCore;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Asp.Versioning;
using Asp.Versioning.Routing;
using Microsoft.AspNetCore.Routing;
using Radish.Extensions;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
using Volo.Abp.Security.Claims;
using Volo.Abp.Studio.Client.AspNetCore;
using Volo.Abp.Swashbuckle;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;

namespace Radish;

[DependsOn(
    typeof(RadishHttpApiModule),
    typeof(AbpStudioClientAspNetCoreModule),
    typeof(AbpAspNetCoreMvcUiLeptonXLiteThemeModule),
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreMultiTenancyModule),
    typeof(RadishApplicationModule),
    typeof(RadishMongoDbModule),
    typeof(AbpAccountWebOpenIddictModule),
    typeof(AbpSwashbuckleModule),
    typeof(AbpAspNetCoreSerilogModule)
)]
public class RadishHttpApiHostModule : AbpModule // 这里不能设置为 abstract 抽象类
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        PreConfigure<OpenIddictBuilder>(builder =>
        {
            builder.AddValidation(options =>
            {
                options.AddAudiences("Radish");
                options.UseLocalServer();
                options.UseAspNetCore();
            });
        });

        if (!hostingEnvironment.IsDevelopment())
        {
            PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
            {
                options.AddDevelopmentEncryptionAndSigningCertificate = false;
            });

            PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
            {
                serverBuilder.AddProductionEncryptionAndSigningCertificate("openiddict.pfx",
                    configuration["AuthServer:CertificatePassPhrase"]!);
                serverBuilder.SetIssuer(new Uri(configuration["AuthServer:Authority"]!));
            });
        }
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        if (!configuration.GetValue<bool>("App:DisablePII"))
        {
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.LogCompleteSecurityArtifact = true;
        }

        if (!configuration.GetValue<bool>("AuthServer:RequireHttpsMetadata"))
        {
            Configure<OpenIddictServerAspNetCoreOptions>(options =>
            {
                options.DisableTransportSecurityRequirement = true;
            });

            Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
            });
        }
        
        // 生成多 API 版本文档
        var apiVersions = new CustomApiVersion.AutoApiVersions();
        // AutoApiVersions 尽量只能整数版本号，例如 V1，V2
        foreach (FieldInfo field in apiVersions.GetType().GetFields())
        {
            var apiVersion = field.GetValue(apiVersions)!.ToString()!;
            context.Services.AddOpenApi(apiVersion, options =>
            {
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    // 设置文档的标题、版本和描述
                    document.Info = new()
                    {
                        Title = $"Radish API | {apiVersion}", // 文档标题
                        Version = apiVersion, // 文档版本
                        Description = apiVersion switch
                        {
                            "V1" => "版本 1.0，只包含基础功能，部分在测试中。",
                            "V2" => "版本 2.0，正式发布和生产使用。",
                            _ => "Radish API 文档。"
                        }
                    };
                    return Task.CompletedTask;
                });
            });
        }
        // 启用 API 版本控制
        context.Services.AddAbpApiVersioning(options =>
        {
            options.ReportApiVersions = true; // 报告 API 版本
            options.AssumeDefaultVersionWhenUnspecified = true; // 当未指定版本时使用默认版本
            options.DefaultApiVersion = new ApiVersion(1, 0); // 默认 API 版本，第一个参数为大版本，第二个为小版本，小版本号为 0 时自动忽略
            // options.DefaultApiVersion = new ApiVersion("1beta"); // 不知道为什么，这里不支持字符串了，实际上应该是支持的
            options.ApiVersionReader = new UrlSegmentApiVersionReader(); // 使用 URL 段作为版本读取器
            // options.ApiVersionReader = new QueryStringApiVersionReader("api-version"); // 使用查询字符串作为版本读取器
            // options.ApiVersionReader = new HeaderApiVersionReader("api-version"); // 使用请求头作为版本读取器
        }).AddApiExplorer(options =>
        {
            // 添加版本化的 API 探索器，这也会注册 IApiVersionDescriptionProvider 服务
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true; // 当使用 URL 段版本控制时，此选项是必要的
        });
        // 自定义路由约束，让 API 版本变为连字符，例如：[ApiVersion(2.1)] => /api/v2-1/xxx
        // 当 ApiVersion 定义为 double 的时候使用，如果是 string 则会报错
        context.Services.Configure<RouteOptions>(options =>
        {
            options.ConstraintMap.Add("apiVersion", typeof(ApiVersionRouteConstraint));
        });
        // 配置自动 API 控制器
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            // options.ConventionalControllers.Create(typeof(RadishApplicationModule).Assembly, opts =>
            // {
            //     opts.RootPath = "api/v{version:apiVersion}";
            //     opts.UseV3UrlStyle = true; // 重要：使用 URL 版本控制
            //     // opts.UrlControllerNameNormalizer = pContext => pContext.ControllerName;
            // });
            // 过滤手动控制器，只显示自动 API 控制器
            //options.ControllersToRemove.Add(typeof(ProductController));
            // 移除控制器分组，只显示 v 开头的版本分组
            options.ChangeControllerModelApiExplorerGroupName = false;
        });

        ConfigureAuthentication(context);
        ConfigureUrls(configuration);
        ConfigureBundles();
        ConfigureConventionalControllers();
        ConfigureHealthChecks(context);
        ConfigureSwagger(context, configuration); // Swagger 配置
        ConfigureScalar(context, configuration); // Scalar 配置
        ConfigureVirtualFileSystem(context);
        ConfigureCors(context, configuration);
    }

    private void ConfigureAuthentication(ServiceConfigurationContext context)
    {
        context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults
            .AuthenticationScheme);
        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
        {
            options.IsDynamicClaimsEnabled = true;
        });
    }

    private void ConfigureUrls(IConfiguration configuration)
    {
        Configure<AppUrlOptions>(options =>
        {
            options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
            options.Applications["Angular"].RootUrl = configuration["App:AngularUrl"];
            options.Applications["Angular"].Urls[AccountUrlNames.PasswordReset] = "account/reset-password";
            options.RedirectAllowedUrls.AddRange(configuration["App:RedirectAllowedUrls"]?.Split(',') ??
                                                 Array.Empty<string>());
        });
    }

    private void ConfigureBundles()
    {
        Configure<AbpBundlingOptions>(options =>
        {
            options.StyleBundles.Configure(
                LeptonXLiteThemeBundles.Styles.Global,
                bundle => { bundle.AddFiles("/global-styles.css"); }
            );

            options.ScriptBundles.Configure(
                LeptonXLiteThemeBundles.Scripts.Global,
                bundle => { bundle.AddFiles("/global-scripts.js"); }
            );
        });
    }


    private void ConfigureVirtualFileSystem(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        if (hostingEnvironment.IsDevelopment())
        {
            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                options.FileSets.ReplaceEmbeddedByPhysical<RadishDomainSharedModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath,
                        $"..{Path.DirectorySeparatorChar}Radish.Domain.Shared"));
                options.FileSets.ReplaceEmbeddedByPhysical<RadishDomainModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}Radish.Domain"));
                options.FileSets.ReplaceEmbeddedByPhysical<RadishApplicationContractsModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath,
                        $"..{Path.DirectorySeparatorChar}Radish.Application.Contracts"));
                options.FileSets.ReplaceEmbeddedByPhysical<RadishApplicationModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath,
                        $"..{Path.DirectorySeparatorChar}Radish.Application"));
            });
        }
    }

    private void ConfigureConventionalControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(RadishApplicationModule).Assembly);
        });
    }

    /// <summary>Swagger 配置</summary>
    /// <param name="context">ServiceConfigurationContext</param>
    /// <param name="configuration">IConfiguration</param>
    private static void ConfigureSwagger(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddAbpSwaggerGenWithOidc(
            configuration["AuthServer:Authority"]!,
            ["Radish"],
            [AbpSwaggerOidcFlows.AuthorizationCode],
            null,
            options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "Radish API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
                options.HideAbpEndpoints(); // 隐藏 ABP 的默认端点
            });
    }

    #region Scalar 配置

    /// <summary>Scalar 扩展服务配置</summary>
    /// <param name="context">ServiceConfigurationContext</param>
    /// <param name="configuration">IConfiguration</param>
    /// <remarks>Scalar 的主配置在 Program.cs 中</remarks>
    private static void ConfigureScalar(ServiceConfigurationContext context, IConfiguration configuration)
    {
        // @luobo 2025.10.7 更改为使用默认的 ApiVersions 来生成 openapi 文档
        // 下方已经弃用 ↓
        // 对应 Scalar 的多个 API 文档
        // // 创建 V1 文档 json
        // context.Services.AddOpenApi("V1", options =>
        // {
        //     options.AddDocumentTransformer((document, transformerContext, cancellationToken) =>
        //     {
        //         document.Info = new OpenApiInfo
        //         {
        //             Title = "V1 API",
        //             Version = "V1",
        //             Description = "Radish V1 API"
        //         };
        //         return Task.CompletedTask;
        //     });
        // });
        // // 创建 V2 文档 json
        // context.Services.AddOpenApi("V2", options =>
        // {
        //     options.AddDocumentTransformer((document, transformerContext, cancellationToken) =>
        //     {
        //         document.Info = new OpenApiInfo
        //         {
        //             Title = "V1Beta API",
        //             Version = "v1beta",
        //             Description = "Radish V1Beta API"
        //         };
        //         return Task.CompletedTask;
        //     });
        // });
        // 开启 Scalar 扩展
        context.Services.AddOpenApi(options =>
        {
            options.AddScalarTransformers();
            // ABP 官方 issue 给出的配置，参考：https://github.com/abpframework/abp/discussions/22926
            // 但是我实测不生效，会报错 Document 'v1' could not be loaded
            // options.ShouldInclude = (_) => true;
        });
    }

    #endregion

    /// <summary>跨域请求配置</summary>
    /// <param name="context">ServiceConfigurationContext</param>
    /// <param name="configuration">IConfiguration</param>
    private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder
                    .WithOrigins(
                        configuration["App:CorsOrigins"]?
                            .Split(",", StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.Trim().RemovePostFix("/"))
                            .ToArray() ?? Array.Empty<string>()
                    )
                    .WithAbpExposedHeaders()
                    .SetIsOriginAllowedToAllowWildcardSubdomains()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }

    private void ConfigureHealthChecks(ServiceConfigurationContext context)
    {
        context.Services.AddRadishHealthChecks();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        app.UseForwardedHeaders();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAbpRequestLocalization();

        if (!env.IsDevelopment())
        {
            app.UseErrorPage();
        }

        app.UseRouting(); // 配置 abp 路由，必须放在 UseEndpoints 之前
        app.MapAbpStaticAssets();
        app.UseAbpStudioLink();
        app.UseAbpSecurityHeaders();
        app.UseCors(); // 允许跨域请求，必须在 UseRouting 和 UseAuthentication 之间
        app.UseAuthentication(); // 配置身份认证和权限验证中间件，必须放在 UseRouting 和 UseEndpoints 之间
        app.UseAbpOpenIddictValidation();

        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }

        app.UseUnitOfWork();
        app.UseDynamicClaims();
        app.UseAuthorization(); // 配置身份认证和权限验证中间件，必须放在 UseRouting 和 UseEndpoints 之间

        #region Swagger 配置

        app.UseSwagger();
        app.UseAbpSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Radish API");

            var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
            options.OAuthClientId(configuration["AuthServer:SwaggerClientId"]);
            // options.OAuthClientSecret("1q2w3e*");  // 密码
        });

        #endregion

        #region Scalar 配置

        // if (env.IsDevelopment()) { }
        // 注入 OpenAPI
        ((WebApplication)app).MapOpenApi();
        
        // 注入 Scalar，Api Web UI 管理
        // 文档地址：https://guides.scalar.com/scalar/scalar-api-references/integrations/net-aspnet-core/integration
        // 默认路由地址为：/scalar ，自定义路由地址为：/api-docs
        // 自定义路由方法：app.MapScalarApiReference("/api-docs", options => {});
        ((WebApplication)app).MapScalarApiReference(options =>
        {
            options
                // 多个文档时，这里的标题不起作用，需要单独在 context.Services.AddOpenApi() 中配置
                .WithTitle("Radish API") // 标题
                .WithTheme(ScalarTheme.BluePlanet) // 主题
                // .WithSidebar(false) // 侧边栏
                .WithDarkMode(false) // 黑暗模式
                .WithDefaultOpenAllTags(false); // 是否展开所有标签栏
            // 自定义查找 Open API 文档地址
            // options.WithOpenApiRoutePattern("/openapi/{documentName}.json");
            // 设置默认的 Http 客户端
            options.WithDefaultHttpClient(ScalarTarget.Node, ScalarClient.HttpClient);
        
            // 自定义多个版本 API 文档集合，对应 ConfigureScalar 中的文档名称
            options
                .AddDocument("v1", "V1", "openapi/v1.json", isDefault: true) // 测试版 V1
                .AddDocument("v2", "V2", "openapi/v2.json"); // 发布版 V2
        });

        #endregion

        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        // app.UseHttpsRedirection(); // 配置 HTTP 重定向中间件，强制使用 HTTPS
        app.UseConfiguredEndpoints(); // 配置 abp 中间件
    }
}