using Asp.Versioning;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radish.Api.Controllers;
using Radish.Api.Logging;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Radish.Api.Tests;

public sealed class RuntimeLogIngestHostingTests
{
    [Theory]
    [InlineData(15199, "/internal/logs/ingest", true, "POST", 200)]
    [InlineData(15199, "/internal/logs/ingest", false, "POST", 401)]
    [InlineData(15199, "/api/User", true, "POST", 404)]
    [InlineData(15199, "/scalar", true, "GET", 404)]
    [InlineData(15199, "/hub/chat", true, "GET", 404)]
    [InlineData(15199, "/health", true, "GET", 404)]
    [InlineData(15199, "/internal/logs/ingest", true, "GET", 405)]
    [InlineData(5100, "/internal/logs/ingest", true, "POST", 404)]
    [InlineData(5100, "/INTERNAL/logs/ingest", true, "POST", 404)]
    [InlineData(5100, "/api/User", true, "POST", 202)]
    public async Task PortAndCredentialBoundary_ShouldPrecedeBodyAndBusinessPipeline(int port, string path,
        bool credential, string method, int expected)
    {
        var options = Options();
        var service = new CaptureService();
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { EnvironmentName = "Production", ContentRootPath = Path.GetTempPath() });
        builder.WebHost.UseKestrel(); // 仅注册 / Build，不 Start 或绑定端口。
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IRuntimeLogIngestService>(service);
        builder.Services.AddAuthorization();
        builder.Services.AddApiVersioning().AddMvc();
        builder.Services.AddControllers().ConfigureApplicationPartManager(parts =>
        {
            parts.ApplicationParts.Clear();
            parts.ApplicationParts.Add(new AssemblyPart(typeof(RuntimeLogIngestController).Assembly));
        }).AddControllersAsServices();
        await using var app = builder.Build();
        app.UseRuntimeLogIngest(options);
        var businessCalls = 0;
        app.Run(context => { businessCalls++; context.Response.StatusCode = 202; return Task.CompletedTask; });
        var pipeline = ((IApplicationBuilder)app).Build();
        using var requestScope = app.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = requestScope.ServiceProvider };
        context.Connection.LocalPort = port;
        context.Request.Path = path;
        context.Request.Method = method;
        context.Request.ContentType = "application/json";
        context.Request.Headers[RuntimeLogIngestSetup.CredentialHeader] = credential ? options.Credential : "PRIVATE";
        context.Request.Headers["X-Forwarded-Port"] = "15199";
        context.Request.Host = new HostString("localhost", 15199);
        context.Request.Body = new UnreadableStream();
        context.Response.Body = new MemoryStream();
        await pipeline(context);
        Assert.Equal(expected, context.Response.StatusCode);
        Assert.Equal(expected == 200 ? 1 : 0, service.Calls);
        Assert.Equal(expected == 202 ? 1 : 0, businessCalls);
    }

    [Theory]
    [InlineData("", 15199)]
    [InlineData("http://0.0.0.0:15199", 15199)]
    [InlineData("http://0.0.0.0:0", 15199)]
    public void ListenerConfiguration_ShouldRejectMissingOrConflictingPublicBindings(string url, int port)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kestrel:Endpoints:Public:Url"] = url
        }).Build();
        Assert.Throws<InvalidOperationException>(() => RuntimeLogIngestSetup.ValidateListeners(config, port));
    }

    [Fact]
    public void CandidateConfiguration_ShouldRequireExplicitCredentialsAndAllowSeparateListeners()
    {
        var options = Options(); options.Validate(); options.Credential = "short";
        Assert.Throws<InvalidOperationException>(options.Validate);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kestrel:Endpoints:Public:Url"] = "http://0.0.0.0:5100"
        }).Build();
        RuntimeLogIngestSetup.ValidateListeners(config, 15199);
        var disabled = new RuntimeLogIngestOptions(); disabled.Validate();
        Assert.False(disabled.Enabled);
    }

    [Fact]
    public void IngestionScope_ShouldSuppressMainChainAndRestoreOutsideEvents()
    {
        var sink = new CaptureSink();
        using var logger = new LoggerConfiguration().MinimumLevel.Verbose()
            .Filter.ByExcluding(RuntimeLogIngestScope.Suppress).WriteTo.Sink(sink).CreateLogger();
        logger.Information("before");
        using (new RuntimeLogIngestScope())
        {
            logger.Error("PRIVATE");
            using (new RuntimeLogIngestScope()) logger.Information("PRIVATE");
            logger.Warning("PRIVATE");
        }
        logger.Information("after");
        Assert.Equal(2, sink.Count);
        logger.ForContext("SourceContext", "Microsoft.AspNetCore.Hosting.Diagnostics")
            .ForContext("EventId", new { Id = 1 }, destructureObjects: true)
            .ForContext("Path", "/internal/logs/ingest").Information("request started");
        Assert.Equal(2, sink.Count);
        logger.ForContext("SourceContext", "Microsoft.AspNetCore.Hosting.Diagnostics")
            .ForContext("EventId", new { Id = 13 }, destructureObjects: true)
            .ForContext("Path", "/internal/logs/ingest").Error("independent fault");
        Assert.Equal(3, sink.Count);
    }

    [Fact]
    public async Task AuthorizedController_ShouldPersistThroughRealServiceWithoutBusinessMiddleware()
    {
        using var test = new RuntimeLogIngestTestSupport();
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production", ContentRootPath = test.DirectoryPath
        });
        builder.WebHost.UseKestrel();
        builder.Services.AddSingleton<IRuntimeLogIngestService>(test.Service());
        builder.Services.AddAuthorization();
        builder.Services.AddApiVersioning().AddMvc();
        builder.Services.AddControllers().AddApplicationPart(typeof(RuntimeLogIngestController).Assembly).AddControllersAsServices();
        await using var app = builder.Build();
        app.UseRuntimeLogIngest(test.Options);
        app.Run(_ => throw new InvalidOperationException("Business pipeline must not run."));
        var pipeline = ((IApplicationBuilder)app).Build();
        using var scope = app.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Connection.LocalPort = test.Options.Port;
        context.Request.Method = "POST";
        context.Request.Path = RuntimeLogIngestSetup.IngestPath;
        context.Request.ContentType = "Application/X-NDJSON; charset=UTF-8";
        context.Request.Headers[RuntimeLogIngestSetup.CredentialHeader] = test.Options.Credential;
        context.Request.Body = RuntimeLogIngestTestSupport.Body(test.Event());
        context.Response.Body = new MemoryStream();
        await pipeline(context);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(1, test.Db.GetConnectionScope("log").Queryable<Radish.Model.LogModels.RuntimeLogEventRecord>().Count());
    }

    private static RuntimeLogIngestOptions Options() => new()
    {
        Enabled = true, Port = 15199, Credential = new string('k', 40), DeploymentId = "local", Services = ["api"]
    };
    private sealed class CaptureService : IRuntimeLogIngestService
    {
        public int Calls { get; private set; }
        public Task<RuntimeLogIngestResult> IngestAsync(Stream stream, CancellationToken token)
        { Calls++; return Task.FromResult(new RuntimeLogIngestResult(200, new RuntimeLogIngestVo { VoState = "committed" })); }
    }
    private sealed class UnreadableStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) =>
            throw new InvalidOperationException("Body must not be read before admission.");
    }
    private sealed class CaptureSink : ILogEventSink
    {
        public int Count { get; private set; }
        public void Emit(LogEvent logEvent) => Count++;
    }
}
