using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
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
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.CustomEnum;
using Serilog;
using Radish.Shared.Constants;
using Xunit;

using System.Net;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Radish.Common.CacheTool;
using Radish.Common.OptionTool;
using Radish.Common.Security;
using Radish.Common.TimeTool;
using Radish.Model.Models;
using StackExchange.Redis;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class FileTokenQuotaLoggingTests
{
    private const string Secret = "FILE_TOKEN_QUOTA_PRIVATE_SENTINEL";
    private static readonly DateTime Now = new(2026, 9, 28, 3, 0, 0, DateTimeKind.Utc);
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task TokenLifecycle_ShouldKeepCredentialsAndAtomicOperationsQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        FileAccessToken? added = null;
        f.Tokens.Setup(r => r.AddAsync(It.IsAny<FileAccessToken>()))
            .Callback<FileAccessToken>(t => added = t).ReturnsAsync(9);
        var created = Assert.IsType<FileAccessTokenCreatedVo>((await f.Controller.CreateAccessToken(
            new CreateFileAccessTokenDto { AttachmentId = 2, ValidHours = 2, MaxAccessCount = 3, AuthorizedIp = " 127.0.0.1 " })).ResponseData);
        Assert.NotNull(added);
        Assert.Equal(FileAccessTokenHashing.HashToken(created.VoToken), added.TokenHash);
        Assert.Equal(Now.AddHours(2), added.ExpiresAt);
        Assert.Equal("127.0.0.1", added.AuthorizedIp);
        Assert.Contains(Uri.EscapeDataString(created.VoToken), created.VoAccessUrl);
        Assert.DoesNotContain(added.TokenHash, created.VoAccessUrl);
        Assert.Equal(2, await f.Service.ValidateAndUseTokenAsync(Secret, 1, "127.0.0.1"));
        f.Tokens.Verify(r => r.TryConsumeAsync(f.Row.TokenHash, 1, "127.0.0.1", Now), Times.Once);
        Assert.IsType<FileStreamResult>(await f.Controller.DownloadByToken(Secret));
        await f.Controller.RevokeAccessTokenById(new RevokeFileAccessTokenDto { TokenId = 9 });
        await f.Controller.RevokeAccessToken(Secret);
        f.Tokens.Verify(r => r.TryRevokeByIdAsync(9, Now), Times.Once);
        f.Tokens.Verify(r => r.TryRevokeByHashAsync(f.Row.TokenHash, Now), Times.Once);
        var summary = await f.Service.GetTokenInfoAsync(Secret, 1, false);
        Assert.NotNull(summary);
        Assert.IsNotType<FileAccessTokenCreatedVo>(summary);
        Assert.Single(Assert.IsType<List<FileAccessTokenSummaryVo>>((await f.Controller.GetAttachmentTokens(2)).ResponseData));
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task TokenRejections_ShouldKeepAclCountAndResponseWithoutLogs(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var reason in new[] { "blank", "missing", "revoked", "expired", "user", "ip", "count", "attachment", "wiki", "race" })
        {
            var f = new Fixture();
            switch (reason)
            {
                case "missing": f.Tokens.Setup(r => r.GetByHashAsync(It.IsAny<string>())).ReturnsAsync((FileAccessToken?)null); break;
                case "revoked": f.Row.IsRevoked = true; break;
                case "expired": f.Row.ExpiresAt = Now; break;
                case "user": f.Row.AuthorizedUserId = 99; break;
                case "ip": f.Row.AuthorizedIp = "192.0.2.1"; break;
                case "count": f.Row.MaxAccessCount = 1; f.Row.AccessCount = 1; break;
                case "attachment": f.Attachment.IsDeleted = true; break;
                case "wiki": f.Wiki.Setup(w => w.IsWikiControlledAsync(f.Attachment)).ReturnsAsync(true); break;
                case "race": f.Tokens.Setup(r => r.TryConsumeAsync(f.Row.TokenHash, 1, "127.0.0.1", Now)).ReturnsAsync((FileAccessToken?)null); break;
            }
            var result = Assert.IsType<ObjectResult>(await f.Controller.DownloadByToken(reason == "blank" ? " " : Secret));
            Assert.Equal(403, result.StatusCode);
            f.Tokens.Verify(r => r.TryConsumeAsync(It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<string?>(), It.IsAny<DateTime>()),
                reason == "race" ? Times.Once() : Times.Never());
            capture.AssertQuiet();
        }
        var rejected = new Fixture();
        rejected.Tokens.Setup(r => r.TryRevokeByIdAsync(9, Now)).ReturnsAsync(false);
        var conflict = await Assert.ThrowsAsync<BusinessException>(() => rejected.Controller.RevokeAccessTokenById(new RevokeFileAccessTokenDto { TokenId = 9 }));
        Assert.Equal(409, conflict.StatusCode);
        await InvokeApiAsync(() => rejected.Controller.RevokeAccessTokenById(new RevokeFileAccessTokenDto { TokenId = 9 }), capture.Logger, 409);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task TokenFailures_ShouldKeepWrappingAndFinalSingleError(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        var failure = new IOException(Secret);
        f.Tokens.Setup(r => r.AddAsync(It.IsAny<FileAccessToken>())).ThrowsAsync(failure);
        f.Tokens.Setup(r => r.TryConsumeAsync(It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<string?>(), It.IsAny<DateTime>())).ThrowsAsync(failure);
        f.Tokens.Setup(r => r.TryRevokeByHashAsync(It.IsAny<string>(), It.IsAny<DateTime>())).ThrowsAsync(failure);
        f.Tokens.Setup(r => r.TryRevokeByIdAsync(It.IsAny<long>(), It.IsAny<DateTime>())).ThrowsAsync(failure);
        f.Tokens.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<FileAccessToken, bool>>>())).ThrowsAsync(failure);
        Func<Task>[] wrapped =
        [
            () => f.Controller.CreateAccessToken(new CreateFileAccessTokenDto { AttachmentId = 2, ValidHours = 1 }),
            () => f.Controller.DownloadByToken(Secret), () => f.Controller.RevokeAccessToken(Secret),
            () => f.Controller.GetAttachmentTokens(2)
        ];
        foreach (var call in wrapped)
        {
            var error = await Assert.ThrowsAsync<BusinessException>(call);
            Assert.Equal(500, error.StatusCode);
            Assert.Same(failure, error.InnerException);
            capture.AssertQuiet();
            await InvokeApiAsync(call, capture.Logger, 500);
            capture.AssertSingle("http.failed", "Error");
            capture.Clear();
        }
        Func<Task> revoke = () => f.Controller.RevokeAccessTokenById(new RevokeFileAccessTokenDto { TokenId = 9 });
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(revoke));
        await InvokeApiAsync(revoke, capture.Logger, 500);
        capture.AssertSingle("http.failed", "Error");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Quota_ShouldKeepRejectionsSettlementAndResetQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var options = new UploadRateLimitOptions { Enable = true, MaxConcurrentUploads = 1, MaxUploadsPerMinute = 2, MaxDailyUploadSize = 100 };
        var quota = CreateQuota(cache, options);
        Assert.True((await quota.AcquireUploadAsync(1, Secret, 60)).IsAllowed);
        Assert.True((await quota.AcquireUploadAsync(1, Secret, 60)).IsAllowed);
        Assert.Equal(UploadRateLimitFailureKind.ConcurrentUploads, (await quota.AcquireUploadAsync(1, "second", 10)).FailureKind);
        var controller = new Fixture(quota).Controller;
        using var stream = new MemoryStream(new byte[10]);
        var response = await controller.UploadDocument(new FormFile(stream, 0, 10, "file", Secret + ".pdf"));
        Assert.Equal(429, response.StatusCode);
        await quota.CompleteUploadAsync(1, Secret);
        await quota.CompleteUploadAsync(1, Secret);
        Assert.Equal(UploadRateLimitFailureKind.DailyUploadSize, (await quota.AcquireUploadAsync(1, "second", 50)).FailureKind);
        Assert.True((await quota.AcquireUploadAsync(1, "second", 30)).IsAllowed);
        await quota.FailUploadAsync(1, "second");
        Assert.Equal(UploadRateLimitFailureKind.UploadFrequency, (await quota.AcquireUploadAsync(1, "third", 10)).FailureKind);
        var state = await quota.GetUploadStatisticsAsync(1);
        Assert.Equal(2, state.UploadsThisMinute);
        Assert.Equal(60, state.UploadedSizeToday);
        Assert.Equal(0, state.ReservedUploadSizeToday);
        Assert.True((await quota.AcquireUploadAsync(2, Secret, 10)).IsAllowed);
        await quota.ResetUserLimitsAsync(1);
        state = await quota.GetUploadStatisticsAsync(1);
        Assert.Equal(0, state.CurrentConcurrentUploads);
        Assert.Equal(0, state.UploadsThisMinute);
        Assert.Equal(0, state.OccupiedUploadSizeToday);
        Assert.Equal(1, (await quota.GetUploadStatisticsAsync(2)).CurrentConcurrentUploads);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task QuotaFailures_ShouldPropagateThroughUploadAndKeepSingleFinalError(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var failure = new IOException(Secret);
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var quota = CreateQuota(cache.Object, new UploadRateLimitOptions { Enable = true });
        var controller = new Fixture(quota).Controller;
        using var stream = new MemoryStream(new byte[10]);
        var file = new FormFile(stream, 0, 10, "file", Secret + ".pdf");
        foreach (var call in new Func<Task>[] { () => controller.UploadDocument(file), () => quota.ResetUserLimitsAsync(1) })
        {
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(call));
            capture.AssertQuiet();
            await InvokeApiAsync(call, capture.Logger, 500);
            capture.AssertSingle("http.failed", "Error");
            capture.Clear();
        }
    }

    private static UploadRateLimitService CreateQuota(IDistributedCache cache, UploadRateLimitOptions options)
    {
        var time = new FixedTimeProvider();
        return new UploadRateLimitService(new Caching(cache), Options.Create(options), Options.Create(new RedisOptions { Enable = false }),
            time, new BusinessCalendar(time, Options.Create(new TimeOptions { DefaultTimeZoneId = "Asia/Shanghai" })), Array.Empty<IConnectionMultiplexer>());
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private sealed class Fixture
    {
        public Mock<IFileAccessTokenRepository> Tokens { get; } = new();
        public Mock<IWikiAttachmentAccessService> Wiki { get; } = new();
        public Attachment Attachment { get; } = new() { Id = 2, UploaderId = 1, IsEnabled = true, OriginalName = Secret, MimeType = "application/pdf" };
        public FileAccessToken Row { get; } = new() { Id = 9, AttachmentId = 2, CreatedBy = 1, TokenHash = FileAccessTokenHashing.HashToken(Secret), ExpiresAt = Now.AddHours(1) };
        public FileAccessTokenService Service { get; }
        public AttachmentController Controller { get; }
        public Fixture(IUploadRateLimitService? quota = null)
        {
            Tokens.Setup(r => r.GetByHashAsync(Row.TokenHash)).ReturnsAsync(Row);
            Tokens.Setup(r => r.QueryByIdAsync(9)).ReturnsAsync(Row);
            Tokens.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<FileAccessToken, bool>>>())).ReturnsAsync([Row]);
            Tokens.Setup(r => r.TryConsumeAsync(Row.TokenHash, 1, "127.0.0.1", Now)).ReturnsAsync(Row);
            Tokens.Setup(r => r.TryRevokeByIdAsync(9, Now)).ReturnsAsync(true);
            Tokens.Setup(r => r.TryRevokeByHashAsync(Row.TokenHash, Now)).ReturnsAsync(true);
            var attachments = new Mock<IBaseRepository<Attachment>>();
            attachments.Setup(r => r.QueryByIdAsync(2)).ReturnsAsync(Attachment);
            Service = new FileAccessTokenService(Tokens.Object, attachments.Object, Wiki.Object, new FixedTimeProvider());
            var download = new Mock<IAttachmentService>();
            download.Setup(s => s.GetDownloadStreamAsync(2, 1, It.IsAny<List<string>>(), AttachmentUrlVariant.Original, 0))
                .ReturnsAsync(() => ((Stream?)new MemoryStream(), new AttachmentAssetDto { OriginalName = Secret, MimeType = "application/pdf" }));
            var current = new Mock<ICurrentUserAccessor>();
            current.SetupGet(c => c.Current).Returns(new CurrentUser { IsAuthenticated = true, UserId = 1, UserName = Secret, Roles = [] });
            var localizer = new Mock<IStringLocalizer<Errors>>();
            localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key, true));
            localizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string key, object[] _) => new LocalizedString(key, key, true));
            Controller = new AttachmentController(download.Object, current.Object, Mock.Of<IUserService>(), quota ?? Mock.Of<IUploadRateLimitService>(),
                Options.Create(new UploadRateLimitOptions { Enable = quota != null }), Service,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayService:PublicUrl"] = "https://example.test" }).Build(),
                localizer.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<AttachmentController>.Instance);
            Controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            Controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
        }
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
