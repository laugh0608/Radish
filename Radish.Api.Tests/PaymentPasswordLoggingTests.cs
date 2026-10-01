using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Radish.Model.Models;
using Radish.Common.HelpTool;
using Radish.Shared.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.ErrorHandling;
using Radish.Api.Resources;
using Radish.Common;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Extension.Log;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Serilog;
using Xunit;


namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class PaymentPasswordLoggingTests
{
    private const string Secret = "PAYMENT_PASSWORD_PRIVATE_SENTINEL";
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SetAndChange_ShouldPreserveHashesAndWritesQuietly(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        Assert.True((await f.Controller.SetPassword(new() { NewPassword = "274958", ConfirmPassword = "274958" })).IsSuccess);
        Assert.NotNull(f.Row);
        Assert.True(PasswordHasher.VerifyPassword("274958", f.Row.PasswordHash));
        Assert.Equal(Now, f.Row.CreateTime);
        Assert.Empty(f.Row.Salt);
        Assert.Equal(PaymentPasscodeRules.CurrentPasscodeVersion, f.Row.PasscodeVersion);
        Assert.True((await f.Controller.ChangePassword(new() { CurrentPassword = "274958", NewPassword = "583927", ConfirmPassword = "583927" })).IsSuccess);
        Assert.True(PasswordHasher.VerifyPassword("583927", f.Row.PasswordHash));
        Assert.Equal(Now, f.Row.LastModifiedTime);
        Assert.Equal("System", f.Row.ModifyBy);
        f.Repository.Verify(r => r.ResetFailedAttemptsAsync(7, Now), Times.Once);
        f.Repository.Verify(r => r.UpdateLastUsedTimeAsync(7, Now), Times.Once);
        f.Repository.Verify(r => r.UpdateAsync(It.IsAny<UserPaymentPassword>()), Times.Once);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ExpectedRejections_ShouldKeepCodesAndLockoutQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        Assert.Equal(400, (await f.Controller.SetPassword(new() { NewPassword = "111111", ConfirmPassword = "111111" })).StatusCode);
        Assert.Equal(400, (await f.Controller.SetPassword(new() { NewPassword = "274958", ConfirmPassword = "583927" })).StatusCode);
        Assert.Equal(409, (await f.Controller.ChangePassword(new() { CurrentPassword = "274958", NewPassword = "583927", ConfirmPassword = "583927" })).StatusCode);
        f.Row = new() { UserId = 7, PasswordHash = PasswordHasher.HashPassword("274958"), PasscodeVersion = PaymentPasscodeRules.CurrentPasscodeVersion, FailedAttempts = 4 };
        Assert.Equal(409, (await f.Controller.SetPassword(new() { NewPassword = "274958", ConfirmPassword = "274958" })).StatusCode);
        var locked = await f.Controller.ChangePassword(new() { CurrentPassword = "583927", NewPassword = "583927", ConfirmPassword = "583927" });
        Assert.Equal(429, locked.StatusCode);
        Assert.Equal(PaymentPasscodeErrorCodes.Locked, locked.Code);
        f.Repository.Verify(r => r.UpdateFailedAttemptsAsync(7, 5, Now.AddMinutes(30), Now), Times.Once);
        f.Repository.Verify(r => r.UpdateAsync(It.IsAny<UserPaymentPassword>()), Times.Never);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task AdminResetAndUnlock_ShouldKeepRecordsAndFalseResultsQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        f.Row = new() { UserId = 7, PasswordHash = Secret, Salt = Secret, FailedAttempts = 5, LockedUntil = Now.AddMinutes(30), IsEnabled = true };
        Assert.True((await f.Controller.AdminResetPassword(new() { UserId = 7, Reason = Secret })).ResponseData);
        Assert.Empty(f.Row.PasswordHash);
        Assert.Empty(f.Row.Salt);
        Assert.Null(f.Row.LockedUntil);
        Assert.Null(f.Row.PasscodeVersion);
        Assert.False(f.Row.IsEnabled);
        Assert.Equal(0, f.Row.FailedAttempts);
        Assert.Equal(7, f.Row.ModifyId);
        Assert.Equal("Admin_7", f.Row.ModifyBy);
        Assert.Contains(Secret, f.Row.Remark);
        Assert.Equal(Now, f.Row.ModifyTime);
        foreach (var changed in new[] { false, true })
        {
            f.Repository.Setup(r => r.ResetFailedAttemptsAsync(7, Now)).ReturnsAsync(changed);
            Assert.Equal(changed, (await f.Controller.AdminUnlockPassword(7, Secret)).ResponseData);
        }
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task QueriesAndCleanup_ShouldKeepResultsAndOnlyPositiveBatchSummary(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        var status = (await f.Controller.GetStatus()).ResponseData!;
        Assert.False(status.VoHasPaymentPassword);
        Assert.Equal(3, status.VoSecuritySuggestions.Count);
        f.Repository.SetupSequence(r => r.QueryCountAsync(It.IsAny<Expression<Func<UserPaymentPassword, bool>>>())).ReturnsAsync(4).ReturnsAsync(3);
        f.Repository.Setup(r => r.GetLockedUsersCountAsync(Now)).ReturnsAsync(2);
        var stats = (await f.Controller.AdminGetStats()).ResponseData!;
        Assert.Equal(75, stats.VoPasswordSetupRate);
        Assert.Equal(2, stats.VoLockedUsers);
        Assert.Equal(0, (await f.Controller.AdminClearExpiredLocks()).ResponseData);
        capture.AssertQuiet();
        f.Repository.Setup(r => r.ClearExpiredLocksAsync(Now)).ReturnsAsync(3);
        Assert.Equal(3, (await f.Controller.AdminClearExpiredLocks()).ResponseData);
        capture.AssertSingle("payment.locks_cleared", "Info");
        Assert.Contains(candidate ? "\"processedCount\":3" : "cleared: 3", capture.Output.ToString());
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SuggestionsFailure_ShouldKeepFallbackAndSingleSafeEvent(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        f.Repository.Setup(r => r.GetByUserIdAsync(7)).ThrowsAsync(new IOException(Secret));
        Assert.Equal("无法获取安全建议，请稍后重试", Assert.Single((await f.Controller.GetSecuritySuggestions()).ResponseData!));
        capture.AssertSingle("payment.suggestions_failed", "Error");
        capture.Clear();
        f.Repository.SetupSequence(r => r.GetByUserIdAsync(7)).ReturnsAsync((UserPaymentPassword?)null).ThrowsAsync(new IOException(Secret));
        var status = await f.Controller.GetStatus();
        Assert.True(status.IsSuccess);
        Assert.Equal("无法获取安全建议，请稍后重试", Assert.Single(status.ResponseData!.VoSecuritySuggestions));
        capture.AssertSingle("payment.suggestions_failed", "Error");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task StorageFailures_ShouldReachApiOnce(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        f.Repository.Setup(r => r.GetByUserIdAsync(7)).ThrowsAsync(new IOException(Secret));
        f.Repository.Setup(r => r.ResetFailedAttemptsAsync(7, Now)).ThrowsAsync(new IOException(Secret));
        f.Repository.Setup(r => r.QueryCountAsync(It.IsAny<Expression<Func<UserPaymentPassword, bool>>>())).ThrowsAsync(new IOException(Secret));
        f.Repository.Setup(r => r.ClearExpiredLocksAsync(Now)).ThrowsAsync(new IOException(Secret));
        f.Audit.Setup(a => a.QueryPageAsync(It.IsAny<AuditLogQueryDto>())).ThrowsAsync(new IOException(Secret));
        foreach (var call in new Func<Task>[] { () => f.Controller.GetStatus(),
            () => f.Controller.SetPassword(new() { NewPassword = "274958", ConfirmPassword = "274958" }),
            () => f.Controller.ChangePassword(new() { CurrentPassword = "274958", NewPassword = "583927", ConfirmPassword = "583927" }),
            () => f.Controller.AdminResetPassword(new() { UserId = 7, Reason = Secret }),
            () => f.Controller.AdminUnlockPassword(7, Secret), () => f.Controller.AdminGetStats(),
            () => f.Controller.AdminClearExpiredLocks(), () => f.Controller.GetSecurityLogs() })
        {
            await InvokeApiAsync(call, capture.Logger, 500);
            capture.AssertSingle("http.failed", "Error");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ConsumedBusinessFailures_ShouldKeepResponsesAndOnlyLog5xx(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        foreach (var status in new[] { 400, 409, 429, 500, 503 })
        {
            f.Repository.Setup(r => r.GetByUserIdAsync(7)).ThrowsAsync(new BusinessException(Secret, status, "Payment.Test", "error.test"));
            foreach (var call in new Func<Task<MessageModel<bool>>>[] {
                () => f.Controller.SetPassword(new() { NewPassword = "274958", ConfirmPassword = "274958" }),
                () => f.Controller.ChangePassword(new() { CurrentPassword = "274958", NewPassword = "583927", ConfirmPassword = "583927" }) })
            {
                var response = await call();
                Assert.Equal(status, response.StatusCode);
                Assert.Equal(Secret, response.MessageInfo);
                Assert.Equal("Payment.Test", response.Code);
                if (status >= 500) capture.AssertSingle("http.failed", "Error");
                else capture.AssertQuiet();
                capture.Clear();
            }
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SecurityLogQuery_ShouldKeepUserScopePagingAndAdminExclusion(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        f.Audit.Setup(a => a.QueryPageAsync(It.Is<AuditLogQueryDto>(q => q.UserId == 7 && q.RequestPath == "PaymentPassword"
            && q.PageIndex == 1 && q.PageSize == 50 && q.OrderBy == "DateTime" && q.OrderDirection == "desc")))
            .ReturnsAsync(new PageModel<AuditLogVo>
            {
                Page = 1, PageSize = 50, DataCount = 2, PageCount = 1,
                Data = [new AuditLogVo { VoId = 1, VoRequestPath = "/api/v1/PaymentPassword/SetPassword", VoIsSuccess = true, VoIpAddress = Secret },
                    new AuditLogVo { VoId = 2, VoRequestPath = "/api/v1/PaymentPassword/Admin/ResetPassword" }]
            });
        var page = (await f.Controller.GetSecurityLogs(-1, 100)).ResponseData!;
        Assert.Equal(2, page.DataCount);
        Assert.Equal(50, page.PageSize);
        var log = Assert.Single(page.Data);
        Assert.Equal(1, log.VoId);
        Assert.Equal("password_set", log.VoType);
        Assert.Equal("success", log.VoResult);
        Assert.Equal(Secret, log.VoIpAddress);
        capture.AssertQuiet();
    }

    private static readonly DateTime Now = new(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
    private sealed class FixedTime : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }
    private sealed class Fixture : IDisposable
    {
        public Mock<IPaymentPasswordRepository> Repository { get; } = new();
        public Mock<IAuditLogService> Audit { get; } = new();
        public UserPaymentPassword? Row { get; set; }
        public PaymentPasswordController Controller { get; }
        private readonly Serilog.Extensions.Logging.SerilogLoggerFactory _factory;
        public Fixture(Serilog.ILogger logger)
        {
            _factory = new(logger, dispose: false);
            Repository.Setup(r => r.GetByUserIdAsync(7)).ReturnsAsync(() => Row);
            Repository.Setup(r => r.AddAsync(It.IsAny<UserPaymentPassword>())).Callback<UserPaymentPassword>(row => Row = row).ReturnsAsync(1);
            var mapper = new Mock<IMapper>();
            mapper.Setup(m => m.Map<UserPaymentPasswordVo>(It.IsAny<UserPaymentPassword>())).Returns(new UserPaymentPasswordVo());
            var service = new PaymentPasswordService(Repository.Object, Audit.Object, mapper.Object, _factory.CreateLogger<PaymentPasswordService>(), new FixedTime());
            var current = new Mock<ICurrentUserAccessor>();
            current.SetupGet(c => c.Current).Returns(new CurrentUser { UserId = 7, UserName = Secret, Roles = [] });
            var localizer = new Mock<IStringLocalizer<Errors>>();
            localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key, true));
            Controller = new(service, current.Object, localizer.Object);
        }
        public void Dispose() => _factory.Dispose();
    }

    private static async Task InvokeApiAsync(Func<Task> action, Serilog.ILogger logger, int status)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(logger, dispose: false);
        builder.Services.AddSingleton<ApiExceptionHandler>();
        await using var app = builder.Build();
        app.UseApiExceptionHandler();
        app.Run(async _ => await action());
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test/" + Secret;
        context.Response.Body = new MemoryStream();
        context.RequestServices = app.Services;
        await ((IApplicationBuilder)app).Build()(context);
        Assert.Equal(status, context.Response.StatusCode);
    }

    private sealed class Capture : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        public StringWriter Output { get; } = new();
        public Serilog.Core.Logger Logger { get; }
        public string[] Lines => Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        public Capture(bool candidate, string environment)
        {
            var config = new LoggerConfiguration().MinimumLevel.Verbose();
            if (candidate) RuntimeLoggingConfiguration.Configure(config, new ConfigurationBuilder().Build(), environment, "api", Output, Output);
            else config.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(Output));
            Logger = config.CreateLogger(); Log.Logger = Logger;
        }
        public void AssertQuiet() => Assert.Equal("", Output.ToString());
        public void AssertSingle(string code, string level)
        {
            Assert.Single(Lines);
            Assert.Contains(code, Output.ToString());
            Assert.Contains(level, Output.ToString());
            AssertSafe();
        }
        public void AssertSafe()
        {
            Assert.DoesNotContain(Secret, Output.ToString());
            Assert.DoesNotContain("274958", Output.ToString());
            Assert.DoesNotContain("583927", Output.ToString());
            Assert.DoesNotContain("$argon2id", Output.ToString());
            Assert.DoesNotContain("runtime.unclassified", Output.ToString());
        }
        public void Clear() => Output.GetStringBuilder().Clear();
        public void Dispose() { Log.Logger = _previous; Logger.Dispose(); Output.Dispose(); }
    }
    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }
}
