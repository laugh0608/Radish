using Microsoft.Extensions.FileProviders;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.Extension.Log;
using Serilog;
using Yarp.ReverseProxy;

var builder = WebApplication.CreateBuilder(args);

// ===== 配置管理 =====
builder.Host.ConfigureAppConfiguration((hostingContext, config) =>
{
    hostingContext.Configuration.ConfigureApplication();
    config.Sources.Clear();
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

// 通过 Gateway 路径添加 docs 与 console 健康检查（如果配置了网关地址）
var gatewayPublicUrl = builder.Configuration["GatewayService:PublicUrl"];
if (!string.IsNullOrEmpty(gatewayPublicUrl))
{
    var gatewayBase = gatewayPublicUrl.TrimEnd('/');
    var docsRequestPath = builder.Configuration["Docs:RequestPath"] ?? "/docs";
    var consoleRequestPath = "/console";

    var docsHealthUrl = $"{gatewayBase}{docsRequestPath}";
    healthChecksBuilder.AddUrlGroup(
        new Uri(docsHealthUrl),
        name: "docs-service",
        tags: ["downstream", "docs"]);

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
