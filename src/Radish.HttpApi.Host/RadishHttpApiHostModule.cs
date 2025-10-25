using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
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
using System.Threading.Tasks;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Asp.Versioning.ApplicationModels;
using Microsoft.Extensions.Options;
using Radish.Extensions.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
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

        #region API Version Init

        PreConfigure<AbpAspNetCoreMvcOptions>(options =>
        {
            // API 2.0 Version
            options.ConventionalControllers.Create(typeof(RadishHttpApiModule).Assembly, opts =>
            {
                opts.TypePredicate = t =>
                    t.Namespace == typeof(Controllers.ConventionalControllers.V2.TodoAppService).Namespace;
                opts.ApiVersions.Add(new ApiVersion(2, 0));
            });

            // API 1.0 Version
            options.ConventionalControllers.Create(typeof(RadishHttpApiModule).Assembly, opts =>
            {
                opts.TypePredicate = t =>
                    t.Namespace == typeof(Controllers.ConventionalControllers.V1.TodoAppService).Namespace;
                opts.ApiVersions.Add(new ApiVersion(1, 0));
            });
        });

        #endregion
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

        ConfigureAuthentication(context);
        ConfigureUrls(configuration);
        ConfigureBundles();
        ConfigureConventionalControllers();
        ConfigureHealthChecks(context);
        // ConfigureSwagger(context, configuration); // Swagger 配置，已弃用
        // ConfigureScalar(context, configuration); // Scalar 配置，已弃用
        ConfigureVirtualFileSystem(context);
        ConfigureCors(context, configuration);

        #region API Version

        var preActions = 
            context.Services.GetPreConfigureActions<AbpAspNetCoreMvcOptions>();
        Configure<AbpAspNetCoreMvcOptions>(options => { preActions.Configure(options); });

        // Show neutral/versionless APIs.
        context.Services.AddTransient<IApiControllerFilter, NoControllerFilter>();
        context.Services.AddAbpApiVersioning(options =>
            {
                options.ReportApiVersions = true;
                options.AssumeDefaultVersionWhenUnspecified = true;

                // options.ApiVersionReader = new HeaderApiVersionReader("api-version"); //Supports header too
                // options.ApiVersionReader = new MediaTypeApiVersionReader(); //Supports accept header too
            }, options => { options.ConfigureAbp(preActions.Configure()); })
            .AddApiExplorer(options =>
            {
                // 添加 API Explorer
                // add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
                // note: the specified format code will format the version as "'v'major[.minor][-status]"
                options.GroupNameFormat = "'v'VVV";

                // note: this option is only necessary when versioning by url segment. the SubstitutionFormat
                // can also be used to control the format of the API version in route templates
                options.SubstituteApiVersionInUrl = true;
            });
        context.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
        // Swagger 配置
        context.Services.AddAbpSwaggerGenWithOidc(
            configuration["AuthServer:Authority"]!,
            ["Radish"],
            [AbpSwaggerOidcFlows.AuthorizationCode],
            null,
            options =>
            {
                // add a custom operation filter which sets default values
                options.OperationFilter<SwaggerDefaultValues>();
                options.CustomSchemaIds(type => type.FullName);
                options.DocumentFilter<Radish.Extensions.Swagger.OnlyProjectApisDocumentFilter>();
                // 包含各层 XML 注释到 Swagger（控制器、应用服务接口与DTO注释）
                var xmlTypes = new[]
                {
                    typeof(RadishHttpApiModule),
                    typeof(RadishApplicationModule),
                    typeof(RadishApplicationContractsModule),
                    typeof(RadishHttpApiHostModule)
                };
                foreach (var t in xmlTypes)
                {
                    var xml = Path.Combine(AppContext.BaseDirectory, $"{t.Assembly.GetName().Name}.xml");
                    if (File.Exists(xml))
                    {
                        options.IncludeXmlComments(xml, includeControllerXmlComments: true);
                    }
                }
                // options.SwaggerDoc("v1", new OpenApiInfo { Title = "Radish API", Version = "v1" });
                // options.DocInclusionPredicate((docName, description) => true); // 这个配置已经不兼容了，不能开，否则报错
                // options.HideAbpEndpoints(); // 隐藏 ABP 的默认端点，这个不要用，会隐藏所有 Controller 的 API 节点
            });
        // context.Services.AddAbpSwaggerGen(options =>
        // {
        //     // add a custom operation filter which sets default values
        //     options.OperationFilter<SwaggerDefaultValues>();
        //     options.CustomSchemaIds(type => type.FullName);
        //     // Hides ABP Related endpoints on Swagger UI
        //     options.HideAbpEndpoints();
        // });
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ChangeControllerModelApiExplorerGroupName = false;
        });

        #endregion
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

    #region 文档单独配置类 - 已弃用

    /// <summary>Swagger 配置</summary>
    /// <param name="context">ServiceConfigurationContext</param>
    /// <param name="configuration">IConfiguration</param>
    private static void ConfigureSwagger(ServiceConfigurationContext context, IConfiguration configuration)
    {
        // @luobo 2025.10.8 更改为使用默认的 ApiVersions 来生成 openapi 文档
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

    /// <summary>Scalar 扩展服务配置</summary>
    /// <param name="context">ServiceConfigurationContext</param>
    /// <param name="configuration">IConfiguration</param>
    /// <remarks>Scalar 的主配置在 Program.cs 中</remarks>
    private static void ConfigureScalar(ServiceConfigurationContext context, IConfiguration configuration)
    {
        // @luobo 2025.10.7 更改为使用默认的 ApiVersions 来生成 openapi 文档
        // 下方已经弃用 ↓
        // 对应 Scalar 的多个 API 文档
        // 创建 V1 文档 json
        context.Services.AddOpenApi("V1", options =>
        {
            options.AddDocumentTransformer((document, transformerContext, cancellationToken) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "V1 API",
                    Version = "V1",
                    Description = "Radish V1 API"
                };
                return Task.CompletedTask;
            });
        });
        // 创建 V2 文档 json
        context.Services.AddOpenApi("V2", options =>
        {
            options.AddDocumentTransformer((document, transformerContext, cancellationToken) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "V1Beta API",
                    Version = "v1beta",
                    Description = "Radish V1Beta API"
                };
                return Task.CompletedTask;
            });
        });
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
        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        // app.UseHttpsRedirection(); // 配置 HTTP 重定向中间件，强制使用 HTTPS

        // 保护 Swagger/Scalar 文档页：未登录则触发登录（使用 Cookie 方案）
        app.Use(async (ctx, next) =>
        {
            var path = ctx.Request.Path;
            if (path.StartsWithSegments("/swagger") || path.StartsWithSegments("/scalar"))
            {
                if (!(ctx.User?.Identity?.IsAuthenticated ?? false))
                {
                    // 指定使用 Identity.Application（Cookie）触发交互式登录，避免返回 401 导致前端解析出错
                    await ctx.ChallengeAsync("Identity.Application");
                    return;
                }
            }

            await next();
        });

        #region Swagger 配置

        app.UseSwagger();
        // app.UseAbpSwaggerUI(options =>
        // {
        //     options.SwaggerEndpoint("/swagger/v1/swagger.json", "Radish API");
        //
        //     var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
        //     options.OAuthClientId(configuration["AuthServer:SwaggerClientId"]);
        //     // options.OAuthClientSecret("1q2w3e*");  // 密码
        // });
        app.UseSwaggerUI(options =>
        {
            var provider = app.ApplicationServices.GetRequiredService<IApiVersionDescriptionProvider>();

            var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();

            // OAuth 配置
            options.OAuthClientId(configuration["AuthServer:SwaggerClientId"]);
            // options.OAuthClientSecret("1q2w3e*");  // 密码
            options.OAuthScopes("Radish");
            options.OAuthUsePkce();

            // UI 配置
            options.ConfigObject.DisplayRequestDuration = true;
            options.ConfigObject.ShowExtensions = true;
            options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
            options.EnableDeepLinking();
            options.DisplayOperationId();

            // Console.WriteLine("=== 开始 Swagger UI 配置 ===");
            // Console.WriteLine($"Provider 版本数量: {provider.ApiVersionDescriptions.Count}");
            // 清除默认的文档（如果有）
            // options.ConfigObject.Urls = null;
            // 设置 URLs
            // var urlList = new List<Swashbuckle.AspNetCore.SwaggerUI.UrlDescriptor>();
            // foreach (var desc in provider.ApiVersionDescriptions)
            // {
            //     var urlDesc = new Swashbuckle.AspNetCore.SwaggerUI.UrlDescriptor
            //     {
            //         Name = desc.GroupName.ToUpperInvariant(),
            //         Url = $"/swagger/{desc.GroupName}/swagger.json"
            //     };
            //     urlList.Add(urlDesc);
            //     Console.WriteLine($"创建 UrlDescriptor: {urlDesc.Name} -> {urlDesc.Url}");
            // }
            //
            // options.ConfigObject.Urls = urlList;
            // Console.WriteLine($"设置后 URLs 数量: {options.ConfigObject.Urls?.Count() ?? 0}");
            // Console.WriteLine("=== 结束 Swagger UI 配置 ===");

            // build a swagger endpoint for each discovered API version
            foreach (var description in provider.ApiVersionDescriptions)
            {
                options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json",
                    description.GroupName.ToUpperInvariant());
            }
        });

        #endregion

        #region Scalar 配置

        // if (env.IsDevelopment()) { }
        // 注入 OpenAPI
        // ((WebApplication)app).MapOpenApi();

        // 注入 Scalar，Api Web UI 管理
        // 文档地址：https://guides.scalar.com/scalar/scalar-api-references/integrations/net-aspnet-core/integration
        // 默认路由地址为：/scalar ，自定义路由地址为：/api-docs
        // 自定义路由方法：app.MapScalarApiReference("/api-docs", options => {});
        ((WebApplication)app).MapScalarApiReference(options =>
        {
            options
                // 多个文档时，这里的标题不起作用，需要单独在 context.Services.AddOpenApi() 中配置
                // .WithTitle("Radish API") // 标题
                .WithTheme(ScalarTheme.BluePlanet) // 主题
                // .WithSidebar(false) // 侧边栏
                .WithDarkMode(false) // 黑暗模式
                .WithDefaultOpenAllTags(false); // 是否展开所有标签栏
            // 自定义查找 Open API 文档地址
            // options.WithOpenApiRoutePattern("/openapi/{documentName}.json");
            // 设置默认的 Http 客户端
            options.WithDefaultHttpClient(ScalarTarget.Node, ScalarClient.Axios);

            // 自定义多个版本 API 文档集合，对应 ConfigureScalar 中的文档名称
            // options
            //     .AddDocument("v1", "V1", "swagger/v1/swagger.json", isDefault: true) // 测试版 V1
            //     .AddDocument("v2", "V2", "swagger/v2/swagger.json"); // 发布版 V2
            var provider = app.ApplicationServices.GetRequiredService<IApiVersionDescriptionProvider>();
            // build a swagger endpoint for each discovered API version
            foreach (var description in provider.ApiVersionDescriptions)
            {
                options.AddDocument(description.GroupName.ToUpperInvariant(), description.GroupName.ToUpperInvariant(),
                    $"swagger/{description.GroupName}/swagger.json");
            }
        }).RequireAuthorization();

        #endregion

        app.UseConfiguredEndpoints(); // 配置 abp 中间件
    }
}
