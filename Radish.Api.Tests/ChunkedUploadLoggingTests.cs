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

using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Radish.Common.OptionTool;
using Radish.Model.Models;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class ChunkedUploadLoggingTests
{
    private const string Secret = "CHUNK_UPLOAD_PRIVATE_SENTINEL";
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Lifecycle_ShouldKeepPersistedReplayAndCancellationQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        await f.PrepareAsync();
        var merged = await f.Controller.MergeChunks(f.Request);
        Assert.True(merged.IsSuccess);
        Assert.Equal(99, Assert.IsType<AttachmentVo>(merged.ResponseData).VoId);
        Assert.Equal("Completed", f.Session!.Status);
        Assert.False(Directory.Exists(f.SessionPath));
        Assert.True((await f.Controller.MergeChunks(f.Request)).IsSuccess);
        f.Attachment.Verify(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<FileUploadOptionsDto>(), 7, Secret), Times.Once);
        f.Quota.Verify(q => q.CompleteUploadAsync(7, f.Session.SessionId), Times.Exactly(2));
        Assert.NotNull(await f.Service.GetSessionAsync(f.Session.SessionId, 7));
        using var cancelled = new Fixture();
        await cancelled.PrepareAsync();
        await cancelled.Service.CancelSessionAsync(cancelled.Session!.SessionId, 7);
        await cancelled.Service.CancelSessionAsync(cancelled.Session.SessionId, 7);
        Assert.Equal("Cancelled", cancelled.Session.Status);
        Assert.False(Directory.Exists(cancelled.SessionPath));
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task CompletedStateRetry_ShouldReportOneFinalOutcomeAndKeepSuccess(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var recover in new[] { true, false })
        {
            using var f = new Fixture();
            await f.PrepareAsync();
            f.CompletedFailuresRemaining = recover ? 1 : 2;
            Assert.True((await f.Controller.MergeChunks(f.Request)).IsSuccess);
            Assert.Equal(2, f.CompletedAttempts);
            Assert.Equal(recover ? "Completed" : "Uploading", f.Session!.Status);
            Assert.False(Directory.Exists(f.SessionPath));
            f.Quota.Verify(q => q.CompleteUploadAsync(7, f.Session.SessionId), Times.Once);
            f.Quota.Verify(q => q.FailUploadAsync(It.IsAny<long>(), It.IsAny<string>()), Times.Never);
            capture.AssertSingle(recover ? "upload.session.update_recovered" : "upload.session.update_failed", recover ? "Warning" : "Error");
            Assert.DoesNotContain(f.Session.SessionId, capture.Output.ToString());
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task MergeFailure_ShouldPreserveFailureStateReleaseAndSingleApiLog(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        await f.PrepareAsync();
        f.Attachment.Setup(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<FileUploadOptionsDto>(), 7, Secret))
            .ThrowsAsync(new IOException(Secret));
        await InvokeApiAsync(() => f.Controller.MergeChunks(f.Request), capture.Logger, 500);
        Assert.Equal("Failed", f.Session!.Status);
        Assert.Equal("分片合并或附件处理失败", f.Session.ErrorMessage);
        Assert.False(Directory.Exists(f.SessionPath));
        f.Quota.Verify(q => q.FailUploadAsync(7, f.Session.SessionId), Times.Once);
        f.Quota.Verify(q => q.CompleteUploadAsync(It.IsAny<long>(), It.IsAny<string>()), Times.Never);
        capture.AssertSingle("http.failed", "Error");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task FailedStateWrite_ShouldReportIndependentFailureWithoutLosingOriginal(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        await f.PrepareAsync();
        f.FailFailedStateUpdate = true;
        var failure = new IOException(Secret);
        f.Attachment.Setup(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<FileUploadOptionsDto>(), 7, Secret)).ThrowsAsync(failure);
        var wrapped = await Assert.ThrowsAsync<BusinessException>(() => f.Controller.MergeChunks(f.Request));
        Assert.Same(failure, wrapped.InnerException);
        Assert.Equal(500, wrapped.StatusCode);
        Assert.Equal("Uploading", f.Session!.Status);
        Assert.False(Directory.Exists(f.SessionPath));
        capture.AssertSingle("upload.session.update_failed", "Error");
        // The state-write failure is distinct from the original merge error consumed by the API.
        await InvokeApiAsync(() => Task.FromException(wrapped), capture.Logger, 500);
        Assert.Equal(2, capture.Lines.Length);
        Assert.Contains("http.failed", capture.Lines[1]);
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SettlementFailure_ShouldKeepSuccessfulAttachmentAndSafeCleanupEvent(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        await f.PrepareAsync();
        f.Quota.Setup(q => q.CompleteUploadAsync(7, It.IsAny<string>())).ThrowsAsync(new IOException(Secret));
        Assert.True((await f.Controller.MergeChunks(f.Request)).IsSuccess);
        Assert.Equal("Completed", f.Session!.Status);
        Assert.False(Directory.Exists(f.SessionPath));
        capture.AssertSingle("upload.cleanup.failed", "Error");
        Assert.DoesNotContain(f.Session.SessionId, capture.Output.ToString());
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task RejectedChunkAndCreation_ShouldPreserveRollbackWithoutLocalError(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        var session = await f.Service.CreateSessionAsync(Fixture.CreateRequest(), 7, Secret);
        f.Repository.Setup(r => r.UpdateAsync(It.IsAny<UploadSession>())).ReturnsAsync(false);
        using var stream = new MemoryStream(new byte[] { 1, 2 });
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => f.Service.UploadChunkAsync(session.VoSessionId, 0,
            new FormFile(stream, 0, 2, "chunk", Secret), 7));
        Assert.False(File.Exists(Path.Combine(f.SessionPath, "chunk_0")));
        Assert.Equal(0, f.Session!.UploadedChunks);
        capture.AssertQuiet();
        using var create = new Fixture();
        var failure = new IOException(Secret);
        create.Repository.Setup(r => r.AddAsync(It.IsAny<UploadSession>())).ThrowsAsync(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => create.Service.CreateSessionAsync(Fixture.CreateRequest(), 7, Secret)));
        Assert.Empty(Directory.EnumerateDirectories(create.TempPath));
        create.Quota.Verify(q => q.FailUploadAsync(7, It.IsAny<string>()), Times.Once);
        capture.AssertQuiet();
    }

    private sealed class Fixture : IDisposable
    {
        public string TempPath { get; } = Path.Combine(Path.GetTempPath(), Secret, Guid.NewGuid().ToString("N"));
        public string SessionPath => Path.Combine(TempPath, Session!.SessionId);
        public UploadSession? Session { get; private set; }
        public int CompletedFailuresRemaining { get; set; }
        public int CompletedAttempts { get; private set; }
        public bool FailFailedStateUpdate { get; set; }
        public Mock<IUploadSessionRepository> Repository { get; } = new();
        public Mock<IAttachmentService> Attachment { get; } = new();
        public Mock<IUploadRateLimitService> Quota { get; } = new();
        public ChunkedUploadService Service { get; }
        public ChunkedUploadController Controller { get; }
        public MergeChunksDto Request => new() { SessionId = Session!.SessionId, GenerateThumbnail = false, RemoveExif = false };
        public static CreateUploadSessionDto CreateRequest() => new()
        {
            FileName = Secret + ".txt", TotalSize = 2, ChunkSize = 2, MimeType = "text/plain", BusinessType = AttachmentBusinessTypes.General
        };
        public Fixture()
        {
            Repository.Setup(r => r.AddAsync(It.IsAny<UploadSession>())).ReturnsAsync((UploadSession s) => { s.Id = 1; Session = Clone(s); return 1L; });
            Repository.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UploadSession, bool>>>()))
                .ReturnsAsync((Expression<Func<UploadSession, bool>> p) => Session != null && p.Compile()(Session) ? Clone(Session) : null);
            Repository.Setup(r => r.UpdateAsync(It.IsAny<UploadSession>())).ReturnsAsync((UploadSession s) =>
            {
                if (s.Status == "Completed")
                {
                    CompletedAttempts++;
                    if (CompletedFailuresRemaining-- > 0) throw new IOException(Secret);
                }
                if (s.Status == "Failed" && FailFailedStateUpdate) throw new IOException(Secret);
                Session = Clone(s);
                return true;
            });
            Quota.Setup(q => q.AcquireUploadAsync(7, It.IsAny<string>(), 2, It.IsAny<TimeSpan?>())).ReturnsAsync(UploadRateLimitCheckResult.Allowed());
            Attachment.Setup(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<FileUploadOptionsDto>(), 7, Secret)).ReturnsAsync(new AttachmentVo { VoId = 99 });
            Attachment.Setup(s => s.QueryByIdAsync(99)).ReturnsAsync(new AttachmentVo { VoId = 99 });
            Service = new ChunkedUploadService(Repository.Object, Attachment.Object, Quota.Object,
                Options.Create(new ChunkedUploadOptions { Enable = true, MinChunkSize = 1, MaxChunkSize = 10, DefaultChunkSize = 2, TempChunkPath = TempPath, SessionExpirationHours = 24 }),
                Options.Create(new FileStorageOptions { MaxFileSize = new MaxFileSizeOptions { Avatar = 10, Image = 10, Document = 10, Audio = 10, Video = 10 } }), TimeProvider.System);
            var current = new Mock<ICurrentUserAccessor>();
            current.SetupGet(c => c.Current).Returns(new CurrentUser { UserId = 7, UserName = Secret, Roles = [] });
            Controller = new ChunkedUploadController(Service, current.Object, Mock.Of<IUserService>());
        }
        public async Task PrepareAsync()
        {
            var session = await Service.CreateSessionAsync(CreateRequest(), 7, Secret);
            using var stream = new MemoryStream(new byte[] { 1, 2 });
            await Service.UploadChunkAsync(session.VoSessionId, 0, new FormFile(stream, 0, 2, "chunk", Secret), 7);
        }
        private static UploadSession Clone(UploadSession session) => JsonConvert.DeserializeObject<UploadSession>(JsonConvert.SerializeObject(session))!;
        public void Dispose()
        {
            if (Directory.Exists(TempPath)) Directory.Delete(TempPath, recursive: true);
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
