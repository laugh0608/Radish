using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.Extension.Log;
using Serilog;
using Yarp.ReverseProxy;

var builder = WebApplication.CreateBuilder(args);

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

static void RemoveHttpSysDelegationRegistrations(IServiceCollection services)
{
    for (var index = services.Count - 1; index >= 0; index--)
    {
        var descriptor = services[index];
        var serviceTypeName = descriptor.ServiceType.FullName ?? string.Empty;
        var implementationTypeName = descriptor.ImplementationType?.FullName ?? string.Empty;
        var implementationFactoryMethodName = descriptor.ImplementationFactory?.Method.Name ?? string.Empty;
        var implementationFactoryReturnTypeName = descriptor.ImplementationFactory?.Method.ReturnType.FullName ?? string.Empty;

        if (serviceTypeName.Contains("Yarp.ReverseProxy.Delegation", StringComparison.Ordinal) ||
            implementationTypeName.Contains("Yarp.ReverseProxy.Delegation", StringComparison.Ordinal) ||
            implementationFactoryMethodName.Contains("HttpSysDelegation", StringComparison.Ordinal) ||
            implementationFactoryReturnTypeName.Contains("Yarp.ReverseProxy.Delegation", StringComparison.Ordinal))
        {
            services.RemoveAt(index);
        }
    }
}

// ===== 配置管理 =====
builder.Host.ConfigureAppConfiguration((hostingContext, config) =>
{
    hostingContext.Configuration.ConfigureApplication();
    var basePath = AppContext.BaseDirectory;
    var sharedConfigPath = ResolveSharedConfigPath(basePath, hostingContext.HostingEnvironment.ContentRootPath);
    config.Sources.Clear();
    config.AddJsonFile(sharedConfigPath, optional: true, reloadOnChange: false);
    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
    config.AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json",
        optional: true, reloadOnChange: false);
    config.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
});

// 绑定 InternalApp 扩展中的环境变量
builder.ConfigureApplication();

// ===== Razor Pages 配置 =====
builder.Services.AddRazorPages();

// ===== CORS 配置 =====
var corsSection = builder.Configuration.GetSection("Cors");
var allowedOrigins = corsSection.GetSection("AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("GatewayCorsPolicy", policyBuilder =>
    {
        policyBuilder
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// ===== 健康检查配置 =====
var downstreamSection = builder.Configuration.GetSection("DownstreamServices:ApiService");
var apiBaseUrl = downstreamSection["BaseUrl"];
var apiHealthPath = downstreamSection["HealthCheckPath"];

var healthChecksBuilder = builder.Services.AddHealthChecks();

// 添加下游 API 服务健康检查
if (!string.IsNullOrEmpty(apiBaseUrl) && !string.IsNullOrEmpty(apiHealthPath))
{
    var apiHealthUrl = $"{apiBaseUrl.TrimEnd('/')}{apiHealthPath}";
    healthChecksBuilder.AddUrlGroup(
        new Uri(apiHealthUrl),
        name: "api-service",
        tags: ["downstream", "api"]);
}

// 通过 Gateway 路径添加 console 健康检查（如果配置了网关地址）
var gatewayPublicUrl = builder.Configuration["GatewayService:PublicUrl"];
if (!string.IsNullOrEmpty(gatewayPublicUrl))
{
    var gatewayBase = gatewayPublicUrl.TrimEnd('/');
    var consoleRequestPath = "/console";

    var consoleHealthUrl = $"{gatewayBase}{consoleRequestPath}";
    healthChecksBuilder.AddUrlGroup(
        new Uri(consoleHealthUrl),
        name: "console-service",
        tags: ["downstream", "console"]);
}

// ===== AppSettings 工具初始化 =====
builder.Services.AddSingleton(new AppSettingsTool(builder.Configuration));

// ===== Serilog 日志配置 =====
builder.Host.AddSerilogSetup();

// ===== YARP 反向代理配置 =====
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

if (!OperatingSystem.IsWindows())
{
    RemoveHttpSysDelegationRegistrations(builder.Services);
}

var app = builder.Build();

// 绑定 InternalApp 扩展中的服务
app.ConfigureApplication();

// ===== 中间件配置 =====
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// 启用 WebSocket 支持（用于 Vite HMR）
app.UseWebSockets();

app.UseCors("GatewayCorsPolicy");

app.UseRouting();

// ===== YARP 端点映射 =====
app.MapReverseProxy();

// ===== 端点映射 =====
app.MapRazorPages();

// 健康检查端点
app.MapHealthChecks("/health");
app.MapHealthChecks("/healthz");

// 默认路由到门户页
app.MapFallbackToPage("/Index");

// ===== 启动日志 =====
app.Lifetime.ApplicationStarted.Register(() =>
{
    if (!OperatingSystem.IsWindows())
    {
        Log.Information("当前运行环境非 Windows，Gateway 已跳过 YARP HttpSys delegation 注册");
    }

    var urls = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : "未配置";

    Log.Information("====================================");
    Log.Information("   ____           _ _     _");
    Log.Information("  |  _ \\ __ _  __| (_)___| |__");
    Log.Information("  | |_) / _` |/ _` | / __| '_ \\");
    Log.Information("  |  _ < (_| | (_| | \\__ \\ | | |");
    Log.Information("  |_| \\\\__,_|\\__,_|_|___/_| |_|");
    Log.Information("        Radish.Gateway --by luobo");
    Log.Information("====================================");
    Log.Information("环境: {Environment}", app.Environment.EnvironmentName);
    Log.Information("监听地址: {Urls}", urls);
    Log.Information("CORS 允许来源: {Origins}", string.Join(", ", allowedOrigins));
    if (!string.IsNullOrEmpty(apiBaseUrl))
    {
        Log.Information("下游 API 服务: {ApiUrl}", apiBaseUrl);
    }
});

app.Run();

Log.Information("Radish.Gateway 已关闭");
