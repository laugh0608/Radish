using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Radish.Common.OptionTool;
using Moq;
using OpenIddict.Abstractions;
using Radish.Api.Controllers;
using Radish.Api.Services;
using Radish.Common.CacheTool;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Extension.Log;
using Radish.IService;
using Radish.Model.ViewModels.Client;
using Radish.Service;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class ApplicationOperationOutputTests
{
    private static readonly string[] Sources =
    [
        "Radish.Api.Controllers.UserController", "Radish.Api.Controllers.ClientController", "Radish.Service.BootstrapService",
        "Radish.Service.NotificationDedupService", "Radish.Extension.RateLimitExtension.RateLimitSetup", "Radish.Api.ErrorHandling.ApiExceptionHandler"
    ];

    [Theory]
    [MemberData(nameof(ApplicationOperationLoggingTests.Routes), MemberType = typeof(ApplicationOperationLoggingTests))]
    public void UnknownIdentity_ShouldDropPayloadAndKeepSevereEvents(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, diagnostics: false, audit: audit);
        using var scope = capture.SensitiveScope();
        using var category = LogContext.PushProperty("SourceCategory", "database");
        using var kind = LogContext.PushProperty("failureKind", "io");
        using var diagnostic = LogContext.PushProperty("Diagnostic", false);
        foreach (var source in Sources)
        foreach (var level in Enum.GetValues<LogEventLevel>())
        {
            capture.Logger.ForContext("SourceContext", source).ForContext("EventCode", HttpBoundaryCapture.Secret)
                .Write(level, new HangfireUnreadableException(), "Private {payload}", HttpBoundaryCapture.Secret);
            if (level < LogEventLevel.Warning) Assert.Empty(capture.Events);
            else
            {
                var result = Assert.Single(capture.Events);
                Assert.EndsWith(".unknown", result.Code);
                Assert.Null(result.FailureKind);
                Assert.False(result.Diagnostic);
                capture.AssertSafe();
            }
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(ApplicationOperationLoggingTests.Routes), MemberType = typeof(ApplicationOperationLoggingTests))]
    public void NamedIdentities_ShouldRejectMalformedOrCrossSourceIds(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        var identities = new LogEventPropertyValue[]
        {
            new ScalarValue(1), new ScalarValue(HttpBoundaryCapture.Secret),
            new StructureValue([new("Id", new ScalarValue(1))]),
            new StructureValue([new("Id", new ScalarValue(1)), new("Name", new ScalarValue("DedupCheckFailed"))]),
            new StructureValue([new("Id", new ScalarValue(1)), new("Id", new ScalarValue(1)), new("Name", new ScalarValue("ClientListFailed"))]),
            new StructureValue([new("Id", new ScalarValue(1)), new("Name", new ScalarValue("ClientListFailed")), new("Name", new ScalarValue("ClientListFailed"))]),
            new StructureValue([new("Id", new ScalarValue("1")), new("Name", new ScalarValue("ClientListFailed"))])
        };
        foreach (var identity in identities)
        {
            capture.Logger.Write(new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, new HangfireUnreadableException(),
                new MessageTemplateParser().Parse(HttpBoundaryCapture.Secret),
                [new("SourceContext", new ScalarValue(Sources[1])), new("EventCode", new ScalarValue("client.management.created")),
                 new("EventId", identity), new("failureKind", new ScalarValue("io"))]));
            Assert.Equal("client.management.unknown", Assert.Single(capture.Codes));
            Assert.Null(Assert.Single(capture.Events).FailureKind);
            capture.AssertSafe(); capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(ApplicationOperationLoggingTests.Routes), MemberType = typeof(ApplicationOperationLoggingTests))]
    public void KnownEvents_ShouldRejectForeignFieldsAndPreserveTypedTrace(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var scope = capture.SensitiveScope();
        using var code = LogContext.PushProperty("EventCode", "runtime.failed");
        using var source = LogContext.PushProperty("SourceContext", HttpBoundaryCapture.Secret);
        using var diagnostic = LogContext.PushProperty("Diagnostic", true);
        using var correlation = LogContext.PushProperty("operationId", "01234567-89ab-4cde-8f01-23456789abcd");
        using var rate = LogContext.PushProperty("rateLimitState", "enabled");
        using var activity = new Activity("application-operation").SetIdFormat(ActivityIdFormat.W3C).Start();
        capture.Factory.CreateLogger<ClientController>().LogError(new EventId(1, "ClientListFailed"),
            new HangfireUnreadableException(), "Private {failureKind} {payload}", "timeout", HttpBoundaryCapture.Secret);
        Assert.Equal("client.management.list_failed", Assert.Single(capture.Codes));
        Assert.Equal("timeout", Assert.Single(capture.Events).FailureKind);
        Assert.False(Assert.Single(capture.Events).Diagnostic);
        if (candidate)
        {
            using var json = JsonDocument.Parse(capture.Output.ToString());
            Assert.Equal(activity.TraceId.ToHexString(), json.RootElement.GetProperty("traceId").GetString());
            Assert.False(json.RootElement.TryGetProperty("operationId", out _));
            Assert.Equal("failureKind", Assert.Single(json.RootElement.GetProperty("properties").EnumerateObject()).Name);
        }
        else
        {
            var result = Assert.Single(capture.Sink.Events);
            Assert.Equal(activity.TraceId, result.TraceId);
            Assert.Equal(5, result.Properties.Count);
            Assert.False(result.Properties.ContainsKey("operationId"));
            Assert.False(result.Properties.ContainsKey("rateLimitState"));
        }
        capture.AssertSafe();
        capture.Clear();
        capture.Logger.ForContext("SourceContext", Sources[0]).ForContext("EventCode", "user.current_avatar_degraded")
            .Error("Failure {failureKind}", new { secret = HttpBoundaryCapture.Secret });
        Assert.Null(Assert.Single(capture.Events).FailureKind);
        capture.AssertSafe();
    }

    [Fact]
    public void LegacyCodeConsole_ShouldRebuildAndRejectDiagnosticsInProductionHost()
    {
        var previous = Console.Out;
        using var output = new StringWriter();
        using var services = new ServiceCollection().BuildServiceProvider();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RadishLogging:Mode"] = "Development", ["RadishLogging:Diagnostics"] = "true"
        }).Build();
        try
        {
            Console.SetOut(output);
            var setup = new LoggerConfiguration();
            SerilogSetup.ConfigureLegacy(setup, configuration, services, new SerilogOptions
            {
                MinimumLevel = "Verbose", Console = new() { Enable = true, EnableSqlLog = false },
                File = new() { Enable = false }, Database = new() { Enable = false }
            }, "Production");
            using var logger = setup.CreateLogger();
            using var scope = LogContext.PushProperty("payload", HttpBoundaryCapture.Secret);
            logger.ForContext("SourceContext", Sources[4]).ForContext("EventCode", "rate_limit.rejected")
                .Warning(new HangfireUnreadableException(), HttpBoundaryCapture.Secret);
            Assert.Equal("", output.ToString());
            logger.ForContext("SourceContext", Sources[0]).ForContext("EventCode", "user.current_avatar_degraded")
                .Error(new HangfireUnreadableException(), "Private {failureKind} {payload}", "io", HttpBoundaryCapture.Secret);
            Assert.Contains("user.current_avatar_degraded", output.ToString());
            Assert.DoesNotContain(HttpBoundaryCapture.Secret, output.ToString());
        }
        finally { Console.SetOut(previous); }
    }

    [Fact]
    public void AdjacentSources_ShouldNotBeIntercepted()
    {
        using var sink = new HttpBoundaryTestSink();
        var wrapper = new ApplicationOperationLogSink(sink);
        var value = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null, new MessageTemplateParser().Parse("Adjacent"),
            [new("SourceContext", new ScalarValue(Sources[0] + ".Other"))]);
        wrapper.Emit(value);
        Assert.Same(value, Assert.Single(sink.Events));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyOutputFailure_ShouldKeepWriteToAndAuditToSemantics(bool audit)
    {
        using var capture = new HttpBoundaryCapture(false, "Production", audit: audit);
        capture.Sink.EmitFailure = new IOException(HttpBoundaryCapture.Secret);
        var query = new Mock<IClientApplicationQueryService>();
        query.Setup(q => q.QueryAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(HttpBoundaryCapture.Secret));
        var controller = new ClientController(Mock.Of<IOpenIddictApplicationManager>(), query.Object,
            Mock.Of<ICurrentUserAccessor>(), capture.Factory.CreateLogger<ClientController>());
        if (audit) await Assert.ThrowsAnyAsync<Exception>(() => controller.GetClients(cancellationToken: TestContext.Current.CancellationToken));
        else Assert.False((await controller.GetClients(cancellationToken: TestContext.Current.CancellationToken)).IsSuccess);
        Assert.Empty(capture.Events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClientSuccessOutputFailure_ShouldNotUndoAlreadyCompletedCreate(bool audit)
    {
        using var capture = new HttpBoundaryCapture(false, "Production", audit: audit);
        capture.Sink.EmitFailure = new IOException(HttpBoundaryCapture.Secret);
        var manager = new Mock<IOpenIddictApplicationManager>();
        var writes = 0;
        manager.Setup(m => m.CreateAsync(It.IsAny<OpenIddictApplicationDescriptor>(), It.IsAny<CancellationToken>()))
            .Callback(() => writes++).ReturnsAsync(new object());
        var controller = new ClientController(manager.Object, Mock.Of<IClientApplicationQueryService>(),
            Mock.Of<ICurrentUserAccessor>(a => a.Current == new CurrentUser { UserId = 123 }), capture.Factory.CreateLogger<ClientController>());
        var dto = new CreateClientDto { ClientId = "private-client", ClientType = "confidential", GrantTypes = ["client_credentials"], Scopes = ["radish-api"] };
        if (audit) Assert.Equal(500, (await Assert.ThrowsAsync<BusinessException>(() => controller.CreateClient(dto))).StatusCode);
        else Assert.True((await controller.CreateClient(dto)).IsSuccess);
        Assert.Equal(1, writes);
        Assert.Empty(capture.Events);
    }

    [Fact]
    public async Task CandidateWriterFailure_ShouldUseSafeEmergencyAndKeepConsumedCacheFailure()
    {
        using var writer = new HangfireBrokenWriter();
        using var capture = new HttpBoundaryCapture(true, "Production", output: writer);
        var cache = new Mock<ICaching>();
        cache.Setup(c => c.ExistsAsync(It.IsAny<string>())).ThrowsAsync(new HangfireUnreadableException());
        var service = new NotificationDedupService(cache.Object, capture.Factory.CreateLogger<NotificationDedupService>());
        Assert.False(await service.ShouldDedupAsync(1, HttpBoundaryCapture.Secret, 2));
        Assert.False(await service.ShouldDedupAsync(1, HttpBoundaryCapture.Secret, 2));
        Assert.Empty(capture.Events);
        var emergency = capture.Emergency.ToString();
        Assert.Single(emergency.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.DoesNotContain(HttpBoundaryCapture.Secret, emergency);
        Assert.DoesNotContain(HangfireOutputLoggingTests.Secret, emergency);
        Assert.Contains("pipeline.output_failed", emergency);
    }
}
