using Radish.Common;
using Radish.Common.CoreTool;
using Radish.Extension.SerilogExtension;
using Serilog;

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

// ===== AppSettings 工具初始化 =====
builder.Services.AddSingleton(new AppSettingsTool(builder.Configuration));

// ===== Serilog 日志配置 =====
builder.Host.AddSerilogSetup();

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

app.UseCors("GatewayCorsPolicy");

app.UseRouting();

// ===== 端点映射 =====
app.MapRazorPages();

// 健康检查端点
app.MapHealthChecks("/health");
app.MapHealthChecks("/healthz");

// 默认路由到门户页
app.MapFallbackToPage("/Index");

// ===== 启动日志 =====
Log.Information("========================================");
Log.Information("Radish.Gateway 正在启动...");
Log.Information("环境: {Environment}", app.Environment.EnvironmentName);
Log.Information("监听地址: {Urls}", string.Join(", ", builder.WebHost.GetSetting("urls")?.Split(';') ?? ["未配置"]));
Log.Information("CORS 允许来源: {Origins}", string.Join(", ", allowedOrigins));
if (!string.IsNullOrEmpty(apiBaseUrl))
{
    Log.Information("下游 API 服务: {ApiUrl}", apiBaseUrl);
}
Log.Information("========================================");

app.Run();

Log.Information("Radish.Gateway 已关闭");
