using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Radish.Api.Tests.ErrorHandling;
using Radish.Auth.Controllers;
using Radish.Auth.OpenIddict;
using Radish.Extension.Log;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Radish.Api.Tests.Auth;

[Collection("Runtime logging global state")]
public sealed class AuthOwnedLogSinkTests
{
    private const string AccountSource = "Radish.Auth.Controllers.AccountController";
    private const string IdleSource = "Radish.Auth.OpenIddict.ValidateIdleSessionTokenRequestHandler";
    private static readonly (int Id, string Name, string Code)[] AccountEvents =
    [
        (1, "LoginRejected", "login_rejected"), (2, "LoginResultPrepared", "login_result_prepared"),
        (3, "RegistrationUserCreateReturned", "registration_user_create_returned"),
        (4, "RegistrationRewardReturned", "registration_reward_returned"),
        (5, "RegistrationResultPrepared", "registration_result_prepared"), (6, "RegistrationFailed", "registration_failed"),
        (7, "ClientResolutionReturned", "client_resolution_returned"), (8, "ClientResolutionFailed", "client_resolution_failed"),
        (9, "ReturnUrlParseFailed", "return_url_parse_failed")
    ];

    [Theory]
    [MemberData(nameof(AuthAccountLoggingTests.OutputModes), MemberType = typeof(AuthAccountLoggingTests))]
    public async Task Generators_ShouldUseExactIdentitiesAndDiscardConflictingScope(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var scope = capture.Factory.CreateLogger("scope").BeginScope(new Dictionary<string, object>
        {
            ["SourceContext"] = AuthAccountFixture.Secret, ["EventCode"] = "runtime.failed", ["SourceCategory"] = "database",
            ["Diagnostic"] = true, ["authStage"] = AuthAccountFixture.Secret, ["payload"] = AuthAccountFixture.Secret,
            ["traceId"] = AuthAccountFixture.Secret, ["failureKind"] = AuthAccountFixture.Secret
        });
        GenerateAccountEvents(capture.Factory.CreateLogger<AccountController>());
        await new ValidateIdleSessionTokenRequestHandler(AuthIdleSessionLoggingTests.Options(),
            capture.Factory.CreateLogger<ValidateIdleSessionTokenRequestHandler>()).HandleAsync(AuthIdleSessionLoggingTests.Context("expired"));
        AuthAccountFixture.AssertCodes(capture, AccountEvents.Select(e => "auth.account." + e.Code).Append("auth.idle_session.rejected").ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfiguredOutputs_ShouldCoverTenEventsAndTwoUnknownSummaries(bool audit)
    {
        using var capture = new HttpBoundaryCapture(false, "Development", "auth", audit: audit);
        using var scope = capture.SensitiveScope();
        GenerateAccountEvents(capture.Factory.CreateLogger<AccountController>());
        await new ValidateIdleSessionTokenRequestHandler(AuthIdleSessionLoggingTests.Options(),
            capture.Factory.CreateLogger<ValidateIdleSessionTokenRequestHandler>()).HandleAsync(AuthIdleSessionLoggingTests.Context("expired"));
        capture.Factory.CreateLogger<AccountController>().LogWarning(new EventId(999, "Future"), new UnreadableAuthException(), "{payload}", AuthAccountFixture.Secret);
        capture.Factory.CreateLogger<ValidateIdleSessionTokenRequestHandler>().LogError(new EventId(999, "Future"), new UnreadableAuthException(), "{payload}", AuthAccountFixture.Secret);
        Assert.Equal(12, capture.Codes.Distinct().Count());
        Assert.Equal(12, capture.Events.Length);
        AuthAccountFixture.AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(AuthAccountLoggingTests.OutputModes), MemberType = typeof(AuthAccountLoggingTests))]
    public void DiagnosticGate_ShouldRetainResultsAndSeriousUnknownEvents(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth", diagnostics: false);
        GenerateAccountEvents(capture.Factory.CreateLogger<AccountController>());
        var expected = AccountEvents.Select(e => "auth.account." + e.Code);
        if (candidate) expected = expected.Where(c => c is "auth.account.login_result_prepared" or "auth.account.registration_result_prepared" or
            "auth.account.registration_failed" or "auth.account.client_resolution_failed" or "auth.account.return_url_parse_failed");
        Assert.Equal(expected, capture.Codes);
        capture.Clear();
        foreach (var source in new[] { AccountSource, IdleSource })
        {
            foreach (var level in new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical })
                capture.Factory.CreateLogger(source).Log(level, new EventId(999, "Future"), new UnreadableAuthException(), "{payload}", AuthAccountFixture.Secret);
            Assert.Equal(candidate ? 3 : 6, capture.Events.Length);
            Assert.All(capture.Codes, code => Assert.Equal(source == AccountSource ? "auth.account.unknown" : "auth.idle_session.unknown", code));
            AuthAccountFixture.AssertSafe(capture);
            capture.Clear();
        }
    }

    [Fact]
    public void InvalidIdentityAndPayload_ShouldNotGuessEventsOrWidenToAdjacentSources()
    {
        var captured = new HttpBoundaryTestSink();
        var sink = new AuthenticationLogSink(captured);
        foreach (var entry in AccountEvents)
        {
            foreach (var identity in new LogEventPropertyValue[]
            {
                new ScalarValue(entry.Id), Identity(entry.Id, null), Identity(entry.Id, entry.Name.ToLowerInvariant()), Identity(entry.Id + 100, entry.Name),
                new StructureValue([new("Id", new ScalarValue(entry.Id)), new("Id", new ScalarValue(entry.Id)), new("Name", new ScalarValue(entry.Name))]),
                new StructureValue([new("Id", new ScalarValue(entry.Id)), new("Name", new ScalarValue(entry.Name)), new("Name", new ScalarValue(entry.Name))]),
                new StructureValue([new("Id", new ScalarValue(entry.Id.ToString())), new("Name", new ScalarValue(entry.Name))])
            })
            {
                sink.Emit(Event(AccountSource, identity));
                var output = captured.Events.Last();
                Assert.Equal("auth.account.unknown", ((ScalarValue)output.Properties["EventCode"]).Value);
                Assert.Null(output.Exception);
                Assert.DoesNotContain(AuthAccountFixture.Secret, output.RenderMessage() + string.Join(" ", output.Properties.Values));
            }
            sink.Emit(Event(AccountSource, Identity(entry.Id, entry.Name)));
            var known = captured.Events.Last();
            Assert.Equal("auth.account." + entry.Code, ((ScalarValue)known.Properties["EventCode"]).Value);
            Assert.DoesNotContain("authReason", known.Properties.Keys);
            Assert.DoesNotContain("authStage", known.Properties.Keys);
            Assert.DoesNotContain("authClientOutcome", known.Properties.Keys);
            Assert.DoesNotContain("durationMs", known.Properties.Keys);
            Assert.DoesNotContain("failureKind", known.Properties.Keys);
        }
        var neighbor = Event(AccountSource + "Other", Identity(1, "LoginRejected"));
        sink.Emit(neighbor);
        Assert.Same(neighbor, captured.Events.Last());
        var idleNeighbor = Event("Radish.Auth.OpenIddict.AttachIdleSessionRefreshTokenHandler", Identity(1, "IdleSessionRejected"));
        sink.Emit(idleNeighbor);
        Assert.Same(idleNeighbor, captured.Events.Last());
    }

    [Fact]
    public void TypedCorrelationAndTimestamp_ShouldSurviveWithoutStringScopeIdentifiers()
    {
        var captured = new HttpBoundaryTestSink();
        var trace = ActivityTraceId.CreateRandom();
        var span = ActivitySpanId.CreateRandom();
        var original = Event(AccountSource, Identity(2, "LoginResultPrepared"));
        var input = new LogEvent(original.Timestamp, LogEventLevel.Information, null, original.MessageTemplate,
            original.Properties.Select(p => new LogEventProperty(p.Key, p.Value)), trace, span);
        new AuthenticationLogSink(captured).Emit(input);
        var output = Assert.Single(captured.Events);
        Assert.Equal(input.Timestamp, output.Timestamp);
        Assert.Equal(trace, output.TraceId);
        Assert.Equal(span, output.SpanId);
        Assert.False((bool)((ScalarValue)output.Properties["Diagnostic"]).Value!);
        Assert.DoesNotContain("traceId", output.Properties.Keys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutputFailure_ShouldPreserveWriteVersusAuditSemanticsAndCompletionOrder(bool audit)
    {
        using var capture = new HttpBoundaryCapture(false, "Development", "auth");
        var failure = new IOException("Auth test sink unavailable.");
        var target = new AuthThrowingSink(failure);
        var configuration = new LoggerConfiguration().MinimumLevel.Verbose().Enrich.FromLogContext();
        if (audit) configuration.AuditTo.Sink(new AuthenticationLogSink(target));
        else configuration.WriteTo.Sink(new AuthenticationLogSink(target));
        using var logger = configuration.CreateLogger();
        using var factory = new Serilog.Extensions.Logging.SerilogLoggerFactory(logger, dispose: false);
        var idle = AuthIdleSessionLoggingTests.Context("expired");
        var handler = new ValidateIdleSessionTokenRequestHandler(AuthIdleSessionLoggingTests.Options(), factory.CreateLogger<ValidateIdleSessionTokenRequestHandler>());
        var thrown = await Record.ExceptionAsync(async () => await handler.HandleAsync(idle));
        Assert.True(idle.IsRejected);
        if (audit) Assert.Same(failure, Assert.Single(Assert.IsType<AggregateException>(thrown).InnerExceptions)); else Assert.Null(thrown);

        using var f = new AuthAccountFixture(capture);
        var controller = new FaultingAccountController(f.Localizer.Object, f.Users.Object, f.Applications.Object, f.Coins.Object,
            f.Settings.Object, factory.CreateLogger<AccountController>())
        { ControllerContext = f.Controller.ControllerContext, TempData = f.Controller.TempData, Url = f.Controller.Url };
        target.FailingCode = "auth.account.login_result_prepared";
        thrown = await Record.ExceptionAsync(() => controller.Login(AuthAccountFixture.Email, AuthAccountFixture.Secret, AuthAccountFixture.ReturnUrl));
        Assert.NotNull(f.Principal);
        if (audit) Assert.Same(failure, Assert.Single(Assert.IsType<AggregateException>(thrown).InnerExceptions)); else Assert.Null(thrown);
        target.FailingCode = "auth.account.registration_user_create_returned";
        f.Calls.Clear();
        var result = Assert.IsType<RedirectToActionResult>(await controller.Register(f.Registration()));
        Assert.Equal(audit ? "Register" : "Login", result.ActionName);
        Assert.Equal(audit ? new[] { "create" } : new[] { "create", "reward" }, f.Calls);
        if (audit) Assert.Equal("auth.account.registration_failed", ((ScalarValue)target.Events.Last().Properties["EventCode"]).Value);
    }

    private static void GenerateAccountEvents(Microsoft.Extensions.Logging.ILogger logger)
    {
        // 真实自有生成方法；returnUrl 解析 catch 的证据仅止于此，不伪造解析器故障。
        var type = typeof(AccountController).Assembly.GetType("Radish.Auth.Logging.AuthAccountLog", throwOnError: true)!;
        var calls = new (string Method, object[] Args)[]
        {
            ("LoginRejected", [logger, "invalid-credentials"]), ("LoginResultPrepared", [logger, 123L]),
            ("UserCreateReturned", [logger]), ("RewardReturned", [logger]), ("RegistrationResultPrepared", [logger]),
            ("RegistrationFailed", [logger, "reward", new UnreadableAuthException()]),
            ("ClientResolutionReturned", [logger, "resolved"]), ("ClientResolutionFailed", [logger, new UnreadableAuthException()]),
            ("ReturnUrlParseFailed", [logger, new UnreadableAuthException()])
        };
        foreach (var call in calls) type.GetMethod(call.Method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, call.Args);
    }

    private static StructureValue Identity(int id, string? name) => new([new("Id", new ScalarValue(id)), new("Name", new ScalarValue(name))]);

    private static LogEvent Event(string source, LogEventPropertyValue identity) => new(DateTimeOffset.UtcNow, LogEventLevel.Warning,
        new UnreadableAuthException(), new MessageTemplateParser().Parse(AuthAccountFixture.Secret),
        new[]
        {
            new LogEventProperty("SourceContext", new ScalarValue(source)), new("EventId", identity),
            new("EventCode", new ScalarValue("auth.account.login_result_prepared")), new("Diagnostic", new ScalarValue(true)),
            new("authReason", new ScalarValue(AuthAccountFixture.Secret)), new("authStage", new ScalarValue(AuthAccountFixture.Secret)),
            new("authClientOutcome", new ScalarValue(AuthAccountFixture.Secret)), new("durationMs", new ScalarValue(-1L)),
            new("failureKind", new ScalarValue(AuthAccountFixture.Secret)), new("traceId", new ScalarValue(AuthAccountFixture.Secret))
        });
}

internal sealed class AuthThrowingSink(Exception failure) : ILogEventSink
{
    internal string? FailingCode;
    internal readonly List<LogEvent> Events = [];
    public void Emit(LogEvent logEvent)
    {
        Events.Add(logEvent);
        if (FailingCode == null || logEvent.Properties["EventCode"] is ScalarValue { Value: string code } && code == FailingCode)
            throw failure;
    }
}
