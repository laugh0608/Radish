using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
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
using Microsoft.AspNetCore.Mvc;
using Radish.Common.CoreTool;
using Radish.Common.OptionTool;
using Radish.Infrastructure.FileStorage;
using Radish.Infrastructure.ImageProcessing;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class AttachmentAccessLoggingTests
{
    private const string Secret = "ATTACHMENT_ACCESS_PRIVATE_SENTINEL";
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task DownloadRoutes_ShouldKeepStreamMetadataAndCounterQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        foreach (var call in new Func<Task<IActionResult>>[] { () => f.Controller.Open(1), () => f.Controller.OpenThumbnail(1),
            () => f.Controller.Download(1), () => f.Controller.DownloadByToken(Secret) })
        {
            var result = Assert.IsType<FileStreamResult>(await call());
            using var stream = result.FileStream;
            Assert.Equal("image/png", result.ContentType);
            Assert.Equal(3, stream.Length);
        }
        f.Storage.Verify(s => s.DownloadAsync(Secret), Times.Exactly(3));
        f.Storage.Verify(s => s.DownloadAsync(Secret + "-thumb"), Times.Once);
        Assert.Equal(4, f.Row.DownloadCount);
        f.Repository.Verify(r => r.UpdateAsync(It.Is<Attachment>(a => a.Id == 1)), Times.Exactly(4));
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Denials_ShouldKeepAccessRulesQuietAndAvoidStorage(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var reason in new[] { "missing", "deleted", "disabled", "private", "chat", "wiki" })
        {
            var f = new Fixture();
            switch (reason)
            {
                case "missing": f.Repository.Setup(r => r.QueryByIdAsync(1)).ReturnsAsync((Attachment?)null); break;
                case "deleted": f.Row.IsDeleted = true; break;
                case "disabled": f.Row.IsEnabled = false; break;
                case "private": f.Row.IsPublic = false; f.Row.UploaderId = 99; break;
                case "chat": f.Row.BusinessType = AttachmentBusinessTypes.Chat; f.Row.BusinessId = 9; break;
                case "wiki": f.Wiki.Setup(w => w.IsWikiControlledAsync(f.Row)).ReturnsAsync(true); break;
            }
            Assert.Equal(404, Assert.IsType<NotFoundObjectResult>(await f.Controller.Download(1)).StatusCode);
            f.Storage.Verify(s => s.DownloadAsync(It.IsAny<string>()), Times.Never);
            f.Repository.Verify(r => r.UpdateAsync(It.IsAny<Attachment>()), Times.Never);
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task DownloadFailures_ShouldKeep404AndSingleConsumedEvent(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var reason in new[] { "query", "access", "storage", "empty" })
        {
            var f = new Fixture();
            var failure = new IOException(Secret);
            if (reason == "query") f.Repository.Setup(r => r.QueryByIdAsync(1)).ThrowsAsync(failure);
            if (reason == "access") f.Wiki.Setup(w => w.IsWikiControlledAsync(f.Row)).ThrowsAsync(failure);
            if (reason == "storage") f.Storage.Setup(s => s.DownloadAsync(Secret)).ThrowsAsync(failure);
            if (reason == "empty") f.Storage.Setup(s => s.DownloadAsync(Secret)).ReturnsAsync((Stream?)null);
            Assert.Equal(404, Assert.IsType<NotFoundObjectResult>(await f.Controller.DownloadByToken(Secret)).StatusCode);
            capture.AssertSingle(reason == "empty" ? "attachment.download_unavailable" : "attachment.download_failed", reason == "empty" ? "Warning" : "Error");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task CounterFailure_ShouldNotLoseSuccessfulStreamOrDuplicateError(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        f.Repository.Setup(r => r.UpdateAsync(It.IsAny<Attachment>())).ThrowsAsync(new IOException(Secret));
        var result = Assert.IsType<FileStreamResult>(await f.Controller.Download(1));
        using var stream = result.FileStream;
        Assert.Equal(3, stream.Length);
        capture.AssertSingle("attachment.download_count_failed", "Error");
        capture.Clear();
        f.Repository.Setup(r => r.UpdateAsync(It.IsAny<Attachment>())).ReturnsAsync(false);
        await f.Service.IncrementDownloadCountAsync(1);
        capture.AssertQuiet();
        f.Repository.Setup(r => r.QueryByIdAsync(1)).ReturnsAsync((Attachment?)null);
        await f.Service.IncrementDownloadCountAsync(1);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Delete_ShouldKeepSoftDeleteAndConsumedResponses(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        Attachment? change = null;
        f.Repository.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Attachment, Attachment>>>(), It.IsAny<Expression<Func<Attachment, bool>>>()))
            .Callback<Expression<Func<Attachment, Attachment>>, Expression<Func<Attachment, bool>>>((update, where) =>
            {
                change = update.Compile()(f.Row);
                Assert.True(where.Compile()(f.Row));
                Assert.False(where.Compile()(new Attachment { Id = 2 }));
            }).ReturnsAsync(1);
        Assert.True((await f.Controller.Delete(1)).IsSuccess);
        Assert.NotNull(change);
        Assert.True(change.IsDeleted);
        Assert.NotNull(change.ModifyTime);
        f.Storage.Verify(s => s.DeleteAsync(It.IsAny<string>()), Times.Never);
        capture.AssertQuiet();
        f.Repository.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Attachment, Attachment>>>(), It.IsAny<Expression<Func<Attachment, bool>>>())).ReturnsAsync(0);
        Assert.Equal(500, (await f.Controller.Delete(1)).StatusCode);
        capture.AssertSingle("attachment.delete_rejected", "Warning");
        capture.Clear();
        f.Repository.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Attachment, Attachment>>>(), It.IsAny<Expression<Func<Attachment, bool>>>())).ThrowsAsync(new IOException(Secret));
        Assert.Equal(500, (await f.Controller.Delete(1)).StatusCode);
        capture.AssertSingle("attachment.delete_failed", "Error");
        capture.Clear();
        f.Row.UploaderId = 99;
        Assert.Equal(403, (await f.Controller.Delete(1)).StatusCode);
        capture.AssertQuiet();
        f.Repository.Setup(r => r.QueryByIdAsync(1)).ReturnsAsync((Attachment?)null);
        Assert.False(await f.Service.DeleteFileAsync(1));
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task DeleteBatch_ShouldKeepPartialCountAndPreflightFailureOwnership(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        f.Repository.Setup(r => r.QueryByIdAsync(2)).ReturnsAsync((Attachment?)null);
        f.Repository.Setup(r => r.QueryByIdAsync(3)).ThrowsAsync(new IOException(Secret));
        f.Repository.Setup(r => r.QueryByIdAsync(4)).ReturnsAsync(new Attachment { Id = 4 });
        var result = await f.Controller.DeleteBatch([1, 2, 3, 4]);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, Assert.IsType<int>(result.ResponseData));
        capture.AssertSingle("attachment.delete_failed", "Error");
        capture.Clear();
        f.Repository.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Attachment, bool>>>())).ThrowsAsync(new IOException(Secret));
        await InvokeApiAsync(() => f.Controller.Delete(1), capture.Logger, 500);
        capture.AssertSingle("http.failed", "Error");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task LocalDownload_ShouldKeepMissingAndRejectedPathResultsWithoutInventedCause(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var root = Path.Combine(Path.GetTempPath(), Secret, Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalFileStorage(Options.Create(new FileStorageOptions { Local = new LocalStorageOptions { BasePath = root } }));
            var f = new Fixture(storage);
            foreach (var path in new[] { Secret + ".png", "../" + Secret + ".png" })
            {
                f.Row.StoragePath = path;
                Assert.Equal(404, Assert.IsType<NotFoundObjectResult>(await f.Controller.Download(1)).StatusCode);
                capture.AssertSingle("attachment.download_unavailable", "Warning");
                capture.Clear();
            }
            f.Row.StoragePath = "source.png";
            File.WriteAllBytes(Path.Combine(root, "source.png"), [1, 2, 3]);
            var result = Assert.IsType<FileStreamResult>(await f.Controller.Download(1));
            using var stream = result.FileStream;
            Assert.Equal(0, stream.Position);
            Assert.Equal(3, stream.Length);
            capture.AssertQuiet();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class Fixture
    {
        public Attachment Row { get; } = new() { Id = 1, UploaderId = 7, IsPublic = true, IsEnabled = true,
            StoragePath = Secret, ThumbnailPath = Secret + "-thumb", MimeType = "image/png", OriginalName = Secret, BusinessType = "General" };
        public Mock<IBaseRepository<Attachment>> Repository { get; } = new();
        public Mock<IFileStorage> Storage { get; } = new();
        public Mock<IWikiAttachmentAccessService> Wiki { get; } = new();
        public AttachmentService Service { get; }
        public AttachmentController Controller { get; }
        public Fixture(IFileStorage? storage = null)
        {
            new ServiceCollection().ConfigureApplication();
            Repository.Setup(r => r.QueryByIdAsync(1)).ReturnsAsync(Row);
            Repository.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Attachment, bool>>>()))
                .ReturnsAsync((Expression<Func<Attachment, bool>> p) => p.Compile()(Row) ? Row : null);
            Repository.Setup(r => r.UpdateAsync(It.IsAny<Attachment>())).ReturnsAsync(true);
            Repository.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Attachment, Attachment>>>(), It.IsAny<Expression<Func<Attachment, bool>>>())).ReturnsAsync(1);
            Storage.Setup(s => s.DownloadAsync(It.IsAny<string>())).ReturnsAsync(() => (Stream)new MemoryStream(new byte[] { 1, 2, 3 }));
            Service = new AttachmentService(Mock.Of<IMapper>(), Repository.Object, storage ?? Storage.Object, Mock.Of<IImageProcessor>(),
                Mock.Of<IAttachmentUrlResolver>(), Mock.Of<IChatChannelAccessService>(), Wiki.Object, Options.Create(new FileStorageOptions()));
            var current = new Mock<ICurrentUserAccessor>();
            current.SetupGet(c => c.Current).Returns(new CurrentUser { UserId = 7, UserName = Secret, Roles = [] });
            var tokens = new Mock<IFileAccessTokenService>();
            tokens.Setup(t => t.ValidateAndUseTokenAsync(Secret, 7, It.IsAny<string>(), 0, It.IsAny<IReadOnlyCollection<string>>())).ReturnsAsync(1);
            Controller = new AttachmentController(Service, current.Object, Mock.Of<IUserService>(), Mock.Of<IUploadRateLimitService>(),
                Options.Create(new UploadRateLimitOptions()), tokens.Object, new ConfigurationBuilder().Build(), Mock.Of<IStringLocalizer<Errors>>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AttachmentController>.Instance);
            Controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
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
