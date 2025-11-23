using System.Text;
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
using Radish.Extension.SqlSugarExtension;
using Radish.IRepository;
using Radish.IService;
using Radish.Repository;
using Radish.Service;
using Scalar.AspNetCore;
using SqlSugar;

// -------------- 容器构建阶段 ---------------
var builder = WebApplication.CreateBuilder(args);
// -------------- 容器构建阶段 ---------------

// 使用 Autofac 配置 Host 与容器
builder.Host
    .UseServiceProviderFactory(new AutofacServiceProviderFactory())
    .ConfigureContainer<ContainerBuilder>(containerBuilder =>
    {
        containerBuilder.RegisterModule(new AutofacModuleRegister());
        containerBuilder.RegisterModule(new AutofacPropertyModuleReg(typeof(Program).Assembly));
    }).ConfigureAppConfiguration((hostingContext, config) =>
    {
        hostingContext.Configuration.ConfigureApplication(); // 1. 绑定 InternalApp 扩展中的配置
        config.Sources.Clear();
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        config.AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json",
            optional: true, reloadOnChange: false);
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
                .AllowAnyMethod();
        }
        else
        {
            policyBuilder.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});
// 注册 Controller 控制器
builder.Services.AddControllers();
// 注册 RazorPages 解析
builder.Services.AddRazorPages();
// 通过多份 OpenAPI 文档承载 Scalar 的版本切换，文档：https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi("v1"); // Scalar 默认读取 openapi/v1.json
builder.Services.AddOpenApi("v2"); // 预留第二份文档，方便演进期并行发布
builder.Services.AddOpenApi(options =>
{
    // Scalar 扩展：启用主题、左侧目录等 Transformer
    options.AddScalarTransformers();
});
// 注册 AddAutoMapper 服务
builder.Services.AddAutoMapperSetup(builder.Configuration);
// builder.Services.AddAutoMapper(typeof(AutoMapperConfig));
// AutoMapperConfig.RegisterMappings();
// 注册 AppSetting 自定义扩展服务
builder.Services.AddSingleton(new AppSettingsTool(builder.Configuration));
// 注册 AppSetting 自定义扩展的扩展 ConfigurableOptions 服务
builder.Services.AddAllOptionRegister();
// 注册缓存相关服务
builder.Services.AddCacheSetup();
// 注册 SqlSugar 服务
builder.Services.AddSqlSugarSetup();
// 增强 SqlSugar 的雪花 ID 算法，防止重复
var snowflakeSection = builder.Configuration.GetSection("Snowflake");
SnowFlakeSingle.WorkId = snowflakeSection.GetValue<int>("WorkId");
SnowFlakeSingle.DatacenterId = snowflakeSection.GetValue<int>("DataCenterId");
// 注册泛型仓储与服务，AddScoped() 汇报模式，每次请求的时候注入
builder.Services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
builder.Services.AddScoped(typeof(IBaseService<,>), typeof(BaseService<,>));
// 注册 JWT 认证服务
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, // 订阅者
        ValidateAudience = true, // 发布者
        ValidateLifetime = true, // 生命周期
        ValidateIssuerSigningKey = true, // 密码校验
        // 安全校验，在请求认证的时候会将 Token 进行解析，然后校验下面这三个参数
        ValidIssuer = "Radish", // 颁发者，发行人
        ValidAudience = "luobo", // 使用者
        // 加密密钥
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("wpH7A1jQRPuDDTyWv5ZDpCuAtwvMwmjzeKOMgBtvBe3ghDlfO3FhKx6vmZPAIazM"))
    };
});
// 注册 JWT 授权方案，核心是通过解析请求头中的 JWT Token，然后匹配策略中的 key 和字段值
builder.Services.AddAuthorizationBuilder()
           // Client 授权方案，RequireClaim 方式
           .AddPolicy("Client", policy => policy.RequireClaim("iss", "Radish").Build())
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
// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
// }
app.UseCors(corsPolicyName);
app.MapOpenApi();
// 将 Scalar UI 固定映射到 /api/docs，方便与前端门户共存
app.MapScalarApiReference("/api/docs", options =>
{
    options.WithTitle("Radish API Documentation")
        // 统一主题/外观，保持与桌面化前端一致
        .WithTheme(ScalarTheme.BluePlanet)
        .HideClientButton()
        .ForceDarkMode()
        .HideDarkModeToggle();
    //.EnableDarkMode()
    //.WithClassicLayout()
    //.HideSearch()
    //.ShowOperationId()
    //.ExpandAllTags()
    //.SortTagsAlphabetically()
    //.SortOperationsByMethod()
    //.PreserveSchemaPropertyOrder()
    // .WithProxy("https://localhost:7110")
    // .AddServer("https://api.radish.icu", "Production");
    // 设置默认的 Http 客户端
    options.WithDefaultHttpClient(ScalarTarget.Node, ScalarClient.Axios);
    // 自定义多个版本 API 文档集合
    options
        .AddDocument("v1", "v1", "/openapi/v1.json", isDefault: true) // 默认文档
        .AddDocument("v2", "v2", "/openapi/v2.json"); // 双版本切换入口
    // // Custom local path
    // options.WithOpenApiRoutePattern("/api-spec/{documentName}.json");
    // // External URL
    // options.WithOpenApiRoutePattern("https://api.example.com/openapi/{documentName}.json");
    // // Static external URL (no placeholder)
    // options.WithOpenApiRoutePattern("https://registry.scalar.com/@scalar/apis/galaxy?format=json");
});
// 配置自定义扩展 js 文件
// 放置路径：/wwwroot/scalar/config.js
// app.MapScalarApiReference(options =>
// {
//     options.WithJavaScriptConfiguration("/scalar/config.js");
// });
// // Access HttpContext for dynamic configuration
// app.MapScalarApiReference((options, httpContext) =>
// {
//     var isAdmin = httpContext.User.IsInRole("Admin");
//     options.WithTitle(isAdmin ? "Admin API" : "Public API");
// });
// // Custom route with HttpContext access
// app.MapScalarApiReference("/docs", (options, httpContext) =>
// {
//     options.WithTitle($"API for {httpContext.User.Identity?.Name}");
// });
// app.MapScalarApiReference(options =>
// {
//     options.WithBundleUrl("https://cdn.jsdelivr.net/npm/@scalar/api-reference");
// });
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers();
app.MapFallbackToPage("/Index");

// 输出项目启动标识
Console.WriteLine(@"
====================================
   ____           _ _     _     
  |  _ \ __ _  __| (_)___| |__  
  | |_) / _` |/ _` | / __| '_ \ 
  |  _ < (_| | (_| | \__ \ | | |
  |_| \_\__,_|\__,_|_|___/_| |_|
        Radish  --by luobo
====================================
");

// -------------- App 运行阶段 ---------------
app.Run();
// -------------- App 运行阶段 ---------------
