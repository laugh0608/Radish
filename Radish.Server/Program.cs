using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Radish.Common;
using Radish.Common.Core;
using Radish.Extension;
using Radish.IRepository;
using Radish.IService;
using Radish.Repository;
using Radish.Service;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

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
        // config.AddConfigurationApollo("appsettings.apollo.json");
    });
// 2. 绑定 InternalApp 扩展中的环境变量
builder.ConfigureApplication();
// 激活 Autofac 影响的 IControllerActivator
builder.Services.Replace(ServiceDescriptor.Transient<IControllerActivator, ServiceBasedControllerActivator>());
builder.Services.AddControllers();
builder.Services.AddRazorPages();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
// 通过多份 OpenAPI 文档承载 Scalar 的版本切换
builder.Services.AddOpenApi("v1"); // Scalar 默认读取 openapi/v1.json
builder.Services.AddOpenApi("v2"); // 预留第二份文档，方便演进期并行发布
builder.Services.AddOpenApi(options =>
{
    // Scalar 扩展：启用主题、左侧目录等 Transformer
    options.AddScalarTransformers();
});

// 注册 AddAutoMapper 服务
builder.Services.AddAutoMapperSetup(builder.Configuration);
// 注册 AppSetting 自定义扩展服务
builder.Services.AddSingleton(new AppSettings(builder.Configuration));
// 注册 AppSetting 自定义扩展的扩展 ConfigurableOptions 服务
builder.Services.AddAllOptionRegister();
// 注册泛型仓储与服务，AddScoped() 汇报模式，每次请求的时候注入
builder.Services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
builder.Services.AddScoped(typeof(IBaseService<,>), typeof(BaseService<,>));

var app = builder.Build();

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
app.MapOpenApi();
// 将 Scalar UI 固定映射到 /api/docs，方便与前端门户共存
app.MapScalarApiReference("/api/docs", options =>
{
    options.WithTitle("Radish API Documentation")
        // 统一主题/外观，保持与桌面化前端一致
        .WithTheme(ScalarTheme.BluePlanet)
        .HideClientButton()
        //.EnableDarkMode()
        .ForceDarkMode()
        .HideDarkModeToggle();
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

app.Run();
