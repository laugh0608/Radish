using Asp.Versioning;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Radish.Extension.SqlSugarExtension;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Repository;
using Radish.Service;
using Radish.Service.Logging;

namespace Radish.Api.Logging;

public static class RuntimeLogIngestSetup
{
    public const string CredentialHeader = "X-Radish-Collector-Key";
    public const string IngestPath = "/internal/logs/ingest";
    public const string PruneCommand = "--prune-runtime-logs";
    public const string ReceiveOnlyCommand = "--runtime-log-ingest-only";

    public static async Task RunRuntimeLogReceiverAsync(this WebApplicationBuilder builder)
    {
        builder.AddRuntimeLogIngest(receiveOnly: true);
        builder.Services.AddSqlSugarSetup();
        builder.Services.AddAuthorization();
        builder.Services.AddApiVersioning().AddMvc();
        builder.Services.AddControllers();
        builder.Logging.ClearProviders();
        await using var app = builder.Build();
        app.UseRuntimeLogIngest(app.Services.GetRequiredService<RuntimeLogIngestOptions>());
        await app.RunAsync();
    }

    public static void AddRuntimeLogIngest(this WebApplicationBuilder builder, bool receiveOnly = false)
    {
        var options = builder.Configuration.GetSection("RadishLogging:Ingest").Get<RuntimeLogIngestOptions>() ?? new();
        options.Mode = builder.Configuration["RadishLogging:Mode"] ?? "Production";
        options.Validate();
        if (receiveOnly && !options.Enabled)
            throw new InvalidOperationException("Enable runtime log ingestion explicitly before starting the maintenance receiver.");
        if (options.Enabled && options.Mode == "Development" && !builder.Environment.IsDevelopment())
            throw new InvalidOperationException("Development ingestion requires a Development host.");
        if (options.Enabled)
        {
            if (!receiveOnly)
                ValidateListeners(builder.Configuration, options.Port);
            builder.WebHost.ConfigureKestrel(server =>
            {
                // 覆盖默认 Kestrel 配置加载器，维护接收器只绑定显式内部端口。
                IConfiguration listenerConfiguration = receiveOnly
                    ? new ConfigurationBuilder().Build()
                    : builder.Configuration.GetSection("Kestrel");
                server.Configure(listenerConfiguration, reloadOnChange: false);
                server.ListenAnyIP(options.Port);
            });
        }
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(new RuntimeLogIngestState(options.Enabled ? options.MaxConcurrentRequests : 2));
        builder.Services.AddSingleton(new RuntimeLogIngestDiagnostics());
        builder.Services.AddScoped<IRuntimeLogIngestRepository, RuntimeLogIngestRepository>();
        builder.Services.AddScoped<IRuntimeLogRetentionRepository, RuntimeLogIngestRepository>();
        builder.Services.AddScoped<IRuntimeLogIngestService, RuntimeLogIngestService>();
        builder.Services.AddScoped<IRuntimeLogRetentionService, RuntimeLogRetentionService>();
    }

    public static void ValidateListeners(IConfiguration configuration, int ingestPort)
    {
        var endpoints = configuration.GetSection("Kestrel:Endpoints").GetChildren().ToArray();
        if (endpoints.Length == 0) throw new InvalidOperationException("Ingestion requires explicit public Kestrel endpoints.");
        var ports = new HashSet<int> { ingestPort };
        foreach (var endpoint in endpoints)
        {
            var text = endpoint["Url"]?.Replace("*", "localhost", StringComparison.Ordinal).Replace("+", "localhost", StringComparison.Ordinal);
            if (!Uri.TryCreate(text, UriKind.Absolute, out var address) || address.Scheme is not ("http" or "https") ||
                address.Port is < 1 or > 65535 || !ports.Add(address.Port))
                throw new InvalidOperationException("Invalid or conflicting ingestion/public listener configuration.");
        }
    }

    /// <summary>必须位于静态文件、认证、限流、通用审计与业务端点之前。</summary>
    public static void UseRuntimeLogIngest(this IApplicationBuilder app, RuntimeLogIngestOptions options)
    {
        app.Use(async (context, next) =>
        {
            var internalPort = options.Enabled && context.Connection.LocalPort == options.Port;
            if (!internalPort)
            {
                if (RuntimeLogIngestScope.IsInternalPath(context.Request.Path.Value ?? ""))
                { await ReplyAsync(context, 404, "not-found"); return; }
                await next(context);
                return;
            }
            using var scope = new RuntimeLogIngestScope();
            if (!context.Request.Path.Equals(IngestPath, StringComparison.OrdinalIgnoreCase))
            { await ReplyAsync(context, 404, "not-found"); return; }
            var key = context.Request.Headers[CredentialHeader];
            if (key.Count != 1 || !options.Authenticate(key[0] ?? ""))
            { await ReplyAsync(context, 401, "unauthorized"); return; }
            if (!HttpMethods.IsPost(context.Request.Method))
            { await ReplyAsync(context, 405, "method-not-allowed"); return; }
            var encoding = context.Request.Headers.ContentEncoding;
            var validType = MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var contentType) &&
                (contentType.MediaType.Equals("application/x-ndjson", StringComparison.OrdinalIgnoreCase) ||
                 contentType.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)) &&
                (!contentType.Charset.HasValue || contentType.Charset.Equals("utf-8", StringComparison.OrdinalIgnoreCase));
            if ((encoding.Count > 0 && (encoding.Count != 1 || !string.Equals(encoding[0], "identity", StringComparison.OrdinalIgnoreCase))) || !validType)
            { await ReplyAsync(context, 400, "invalid-envelope"); return; }
            if (context.Request.ContentLength > RuntimeLogIngestOptions.MaxEnvelopeBytes)
            { await ReplyAsync(context, 413, "invalid-envelope"); return; }
            var size = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (size is { IsReadOnly: false }) size.MaxRequestBodySize = RuntimeLogIngestOptions.MaxEnvelopeBytes;
            await next(context);
        });
        app.MapWhen(context => options.Enabled && context.Connection.LocalPort == options.Port, branch =>
        {
            // 分支不会进入业务鉴权、body audit、通用错误响应或静态文件管道。
            branch.UseRouting();
            branch.UseAuthorization();
            branch.UseEndpoints(endpoints => endpoints.MapControllers());
        });
    }

    private static Task ReplyAsync(HttpContext context, int status, string state)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new MessageModel<RuntimeLogIngestVo>
        {
            StatusCode = status, IsSuccess = false, MessageInfo = "Runtime log ingestion rejected.",
            ResponseData = new RuntimeLogIngestVo { VoState = state }
        }, context.RequestAborted);
    }
}
