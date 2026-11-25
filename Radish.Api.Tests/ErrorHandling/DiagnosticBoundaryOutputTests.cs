using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radish.Api.ErrorHandling;
using Radish.Common;
using Radish.DbMigrate;
using Radish.Extension.Log;
using Serilog.Context;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class DiagnosticBoundaryOutputTests
{
    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task InvalidRequestIds_ShouldRemainInResponseAndNeverBeReplacedByScope(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var services = new ServiceCollection().BuildServiceProvider();
        using var scope = LogContext.PushProperty("requestId", "forged-scope-request");
        var handler = new ApiExceptionHandler(capture.Factory.CreateLogger<ApiExceptionHandler>());
        foreach (var id in new[] { "", "bad.request", new string('a', 65) })
        {
            using var body = new MemoryStream();
            var context = new DefaultHttpContext { TraceIdentifier = id, RequestServices = services };
            context.Request.Path = "/api/boundary";
            context.Response.Body = body;
            Assert.True(await handler.TryHandleAsync(context, new IOException(HttpBoundaryCapture.Secret), TestContext.Current.CancellationToken));
            Assert.Equal(500, context.Response.StatusCode);
            Assert.Equal(id, context.Response.Headers["X-Correlation-ID"].ToString());
            using var response = JsonDocument.Parse(body.ToArray());
            Assert.Equal(id, response.RootElement.GetProperty("traceId").GetString());
            Assert.Equal("http.failed", Assert.Single(capture.Codes));
            if (candidate)
            {
                using var result = JsonDocument.Parse(capture.Output.ToString());
                Assert.False(result.RootElement.TryGetProperty("requestId", out _));
                Assert.False(result.RootElement.TryGetProperty("traceId", out _));
                Assert.False(result.RootElement.TryGetProperty("spanId", out _));
            }
            else
            {
                var entry = Assert.Single(capture.Sink.Events);
                Assert.False(entry.Properties.ContainsKey("requestId"));
                Assert.Null(entry.TraceId);
                Assert.Null(entry.SpanId);
            }
            capture.AssertSafe();
            capture.Clear();
        }
    }

    [Fact]
    public async Task GenericCandidateFallback_ShouldIgnoreUntypedCorrelationProperties()
    {
        using var capture = new HttpBoundaryCapture(true, "Production", service: "dbmigrate");
        using var trace = LogContext.PushProperty("traceId", "11111111111111111111111111111111");
        using var span = LogContext.PushProperty("spanId", "2222222222222222");
        using var operation = LogContext.PushProperty("operationId", "01234567-89ab-4cde-8f01-23456789abcd");
        using var request = LogContext.PushProperty("requestId", "scope-only-request");
        await DbMigrateCommand.RunAsync(["help"], capture.Logger, () => Task.CompletedTask);
        Assert.Equal(new[] { "dbmigrate.started", "dbmigrate.completed" }, capture.Codes);
        foreach (var line in capture.Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            using var json = JsonDocument.Parse(line);
            foreach (var key in new[] { "traceId", "spanId", "operationId", "requestId" })
            {
                Assert.False(json.RootElement.TryGetProperty(key, out _));
                Assert.False(json.RootElement.GetProperty("properties").TryGetProperty(key, out _));
            }
        }
    }

    public static IEnumerable<object[]> CommandRoutes()
    {
        foreach (var candidate in new[] { false, true })
        foreach (var command in new[] { "help", "doctor", "verify", "apply" })
            yield return [candidate, command];
    }

    [Theory]
    [MemberData(nameof(CommandRoutes))]
    public async Task Commands_ShouldKeepSafeReportsAndDiagnosticsOnSeparateStreams(bool candidate, string command)
    {
        const string connection = "PRIVATE_CONNECTION_STRING";
        const string suffix = "TRUNCATED_LABEL_SUFFIX";
        using var report = new StringWriter();
        using var diagnostics = new StringWriter();
        using var services = new ServiceCollection().BuildServiceProvider();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RadishLogging:Enabled"] = candidate.ToString(),
            ["Databases:0:ConnId"] = "Disabled\r\nreport",
            ["Databases:0:Enabled"] = "false",
            ["Databases:0:ConnectionString"] = connection,
            ["Databases:0:DbType"] = "2",
            ["Databases:1:ConnId"] = "Enabled\u001Blabel",
            ["Databases:1:Enabled"] = "true",
            ["MainDb"] = "Main\u2028" + new string('x', 90) + suffix
        }).Build();
        var oldConfig = AppSettingsTool.Configuration;
        var oldOutput = Console.Out;
        var oldError = Console.Error;
        try
        {
            AppSettingsTool.Configuration = config;
            Console.SetOut(report);
            Console.SetError(diagnostics);
            var exit = await RuntimeProcess.RunAsync("dbmigrate", async () =>
            {
                using var session = new RuntimeLoggingSession(config, "Production", "dbmigrate", Console.Error, Console.Error);
                using var fallback = session.Logger == null ? DbMigrateCommand.CreateLogger(config, "Production", Console.Error) : null;
                var logger = session.Logger ?? fallback!;
                using var global = new ApplicationLogScope(logger);
                await DbMigrateCommand.RunAsync([command], logger,
                    () => DbMigrateRunner.RunAsync(services, config, "Production\u202Elabel", [command]));
            }, Console.Error);
            Assert.Equal(command is "verify" or "apply" ? 1 : 0, exit);
        }
        finally
        {
            Console.SetOut(oldOutput);
            Console.SetError(oldError);
            AppSettingsTool.Configuration = oldConfig;
        }

        var stdout = report.ToString();
        var stderr = diagnostics.ToString();
        if (command == "help")
        {
            Assert.Contains("Radish.DbMigrate 用法", stdout);
        }
        else
        {
            Assert.Contains("[Doctor] 结论", stdout);
            Assert.Contains(@"Environment: Production\u202Elabel", stdout);
            Assert.Contains(@"ConnId=Disabled\u000D\u000Areport, Enabled=False", stdout);
            Assert.Contains(@"连接 Enabled\u001Blabel 已启用", stdout);
            Assert.Contains(@"MainDb=Main\u2028", stdout);
            Assert.Contains("… 未在 Enabled=true", stdout);
            Assert.DoesNotContain("\u2028", stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("\u001B", stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("\nreport", stdout, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("eventId", stdout);
        Assert.DoesNotContain(connection, stdout + stderr);
        Assert.DoesNotContain(suffix, stdout + stderr);
        Assert.DoesNotContain("[Doctor]", stderr);
        var codes = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
        {
            using var json = JsonDocument.Parse(line);
            var code = json.RootElement.GetProperty("eventCode").GetString();
            if (code == "runtime.failed")
            {
                Assert.True(json.RootElement.GetProperty("isFatal").GetBoolean());
                Assert.Equal("bootstrap", json.RootElement.GetProperty("deploymentId").GetString());
            }
            return code;
        });
        Assert.Equal(command switch
        {
            "apply" => ["dbmigrate.started", "dbmigrate.apply", "runtime.failed"],
            "verify" => ["dbmigrate.started", "runtime.failed"],
            _ => new[] { "dbmigrate.started", "dbmigrate.completed" }
        }, codes);
    }
}
