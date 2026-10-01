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
using System.Reflection;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class AttachmentUploadLoggingTests
{
    private const string Secret = "ATTACHMENT_UPLOAD_PRIVATE_SENTINEL";
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SuccessAndDedup_ShouldKeepOwnershipProcessingAndQuietOutput(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        Assert.True((await f.Upload()).IsSuccess);
        f.Repository.Verify(r => r.AddAsync(It.Is<Attachment>(a => a.UploaderId == 7 && a.BusinessId == null && a.BusinessType == "General")), Times.Once);
        capture.AssertQuiet();
        var existing = new Attachment { Id = 23, StoragePath = Secret, FileHash = Secret };
        f.Repository.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Attachment, bool>>>())).ReturnsAsync(existing);
        f.Storage.Setup(s => s.ExistsAsync(Secret)).ReturnsAsync(true);
        Assert.Equal(23, Assert.IsType<AttachmentVo>((await f.Upload()).ResponseData).VoId);
        capture.AssertQuiet();
        f.Storage.Setup(s => s.ExistsAsync(Secret)).ReturnsAsync(false);
        Assert.True((await f.Upload()).IsSuccess);
        f.Repository.Verify(r => r.SoftDeleteByIdAsync(23, "System"), Times.Once);
        capture.AssertSingle("attachment.dedup_source_missing", "Warning");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task StorageResults_ShouldKeepStatusAndOnlyLogServerFailures(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        foreach (var (kind, status) in new[] { (FileUploadFailureKind.FileTooLarge, 413), (FileUploadFailureKind.UnsupportedType, 415), (FileUploadFailureKind.StorageFailed, 500) })
        {
            f.Storage.Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<FileUploadOptionsDto>()))
                .ReturnsAsync(FileUploadResult.Fail(kind, Secret));
            Assert.Equal(status, (await f.Upload()).StatusCode);
            if (status == 500) capture.AssertSingle("http.failed", "Error");
            else capture.AssertQuiet();
            capture.Clear();
        }
        var failure = new IOException(Secret);
        f.Storage.Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<FileUploadOptionsDto>())).ThrowsAsync(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => f.Upload()));
        capture.AssertQuiet();
        await InvokeApiAsync(() => f.Upload(), capture.Logger, 500);
        capture.AssertSingle("http.failed", "Error");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ImageFailure_ShouldKeepContractCleanupAndSingleFinalError(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var effect in new[] { "thumbnail", "watermark", "exif", "sizes" })
        {
            using var f = new Fixture(capture.Logger);
            f.Image.Setup(i => i.GenerateThumbnailAsync(It.IsAny<Stream>(), It.IsAny<string>(), 150, 150, 85)).ReturnsAsync(ImageProcessResult.Fail(Secret));
            f.Image.Setup(i => i.AddWatermarkAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<Radish.Infrastructure.ImageProcessing.WatermarkOptions>())).ThrowsAsync(new IOException(Secret));
            f.Image.Setup(i => i.RemoveExifAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync(false);
            f.Image.Setup(i => i.GenerateMultipleSizesAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<List<ImageSize>>())).ThrowsAsync(new IOException(Secret));
            var result = await f.Controller.UploadImage(f.File, generateThumbnail: effect == "thumbnail", generateMultipleSizes: effect == "sizes",
                addWatermark: effect == "watermark", watermarkText: Secret, removeExif: effect == "exif");
            Assert.Equal(500, result.StatusCode);
            f.Repository.Verify(r => r.AddAsync(It.IsAny<Attachment>()), Times.Never);
            f.Storage.Verify(s => s.DeleteAsync(It.IsAny<string>()), Times.Exactly(effect == "sizes" ? 5 : 2));
            capture.AssertSingle("http.failed", "Error");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task CleanupFailure_ShouldAggregateDistinctPathsAndPreserveOriginalFailure(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        f.Repository.Setup(r => r.AddAsync(It.IsAny<Attachment>())).ReturnsAsync(0);
        f.Storage.Setup(s => s.DeleteAsync(Fixture.Source)).ThrowsAsync(new IOException(Secret));
        f.Storage.Setup(s => s.DeleteAsync(Fixture.Thumbnail)).ReturnsAsync(false);
        f.Storage.Setup(s => s.ExistsAsync(Fixture.Thumbnail)).ReturnsAsync(true);
        Assert.Equal(500, (await f.Upload()).StatusCode);
        Assert.Equal(2, capture.Lines.Length);
        Assert.Contains("attachment.cleanup_failed", capture.Lines[0]);
        Assert.Contains("http.failed", capture.Lines[1]);
        Assert.Contains("failedCount", capture.Lines[0]);
        Assert.Contains("2", capture.Lines[0]);
        capture.AssertSafe();
        f.Storage.Verify(s => s.DeleteAsync(Fixture.Source), Times.Once);
        f.Storage.Verify(s => s.DeleteAsync(Fixture.Thumbnail), Times.Once);
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task AccountingAndNullResult_ShouldKeepResponseWithSafeConsumedFailure(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var image in new[] { true, false })
        {
            using var f = new Fixture(capture.Logger);
            f.Quota.Setup(q => q.CompleteUploadAsync(7, It.IsAny<string>())).ThrowsAsync(new IOException(Secret));
            Assert.True((await f.Upload(image)).IsSuccess);
            capture.AssertSingle("upload.cleanup.failed", "Error");
            capture.Clear();
            f.Mapper.Setup(m => m.Map<AttachmentVo>(It.IsAny<object>())).Returns((AttachmentVo)null!);
            f.Quota.Setup(q => q.FailUploadAsync(7, It.IsAny<string>())).ThrowsAsync(new IOException(Secret));
            Assert.Equal(500, (await f.Upload(image)).StatusCode);
            Assert.Equal(2, capture.Lines.Length);
            Assert.Contains("upload.cleanup.failed", capture.Lines[0]);
            Assert.Contains("http.failed", capture.Lines[1]);
            capture.AssertSafe();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task LocalStorageFailure_ShouldReturnStableResultWithoutDuplicateError(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        var local = new LocalFileStorage(Options.Create(new FileStorageOptions { Local = new LocalStorageOptions { BasePath = f.TempPath } }));
        using var input = new FailingStream();
        var result = await local.UploadAsync(input, Secret + ".png");
        Assert.False(result.Success);
        Assert.Equal(FileUploadFailureKind.StorageFailed, result.FailureKind);
        capture.AssertQuiet();
        f.Storage.Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<FileUploadOptionsDto>())).ReturnsAsync(result);
        Assert.Equal(500, (await f.Upload()).StatusCode);
        capture.AssertSingle("http.failed", "Error");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ReplacementExhaustion_ShouldPreserveInnerFailureWithoutPerAttemptLogs(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        // A successful processor result with no output exercises the existing three move attempts.
        f.Image.Setup(i => i.AddWatermarkAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<Radish.Infrastructure.ImageProcessing.WatermarkOptions>()))
            .ReturnsAsync(new ImageProcessResult { Success = true });
        Assert.Equal(500, (await f.Controller.UploadImage(f.File, generateThumbnail: false, addWatermark: true, watermarkText: Secret, removeExif: false)).StatusCode);
        capture.AssertSingle("http.failed", "Error");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ImageSuccess_ShouldKeepDerivedPathsAndWatermarkOutOfLogs(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        f.Image.Setup(i => i.GenerateThumbnailAsync(It.IsAny<Stream>(), It.IsAny<string>(), 150, 150, 85))
            .ReturnsAsync(new ImageProcessResult { Success = true });
        f.Image.Setup(i => i.AddWatermarkAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<Radish.Infrastructure.ImageProcessing.WatermarkOptions>()))
            .ReturnsAsync((Stream _, string path, Radish.Infrastructure.ImageProcessing.WatermarkOptions options) =>
            {
                Assert.Equal(Secret, options.Text);
                File.WriteAllBytes(path, [5, 6]);
                return new ImageProcessResult { Success = true, OutputPath = path };
            });
        f.Image.Setup(i => i.RemoveExifAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync((Stream _, string path) => { File.WriteAllBytes(path, [7, 8]); return true; });
        f.Image.Setup(i => i.GenerateMultipleSizesAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<List<ImageSize>>()))
            .ReturnsAsync((Stream _, string path, List<ImageSize> sizes) => sizes.Select(size => new ImageProcessResult
            {
                Success = true,
                OutputPath = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "_" + size.Name + ".png")
            }).ToList());
        Assert.True((await f.Controller.UploadImage(f.File, generateThumbnail: true, generateMultipleSizes: true,
            addWatermark: true, watermarkText: Secret, removeExif: true)).IsSuccess);
        f.Repository.Verify(r => r.AddAsync(It.Is<Attachment>(a => a.SmallPath != null && a.MediumPath != null && a.LargePath != null)), Times.Once);
        Assert.Equal(new byte[] { 7, 8 }, File.ReadAllBytes(Path.Combine(f.TempPath, "source.png")));
        capture.AssertQuiet();
    }

    private sealed class FailingStream : MemoryStream
    {
        public override long Length => throw new IOException(Secret);
    }

    private sealed class Fixture : IDisposable
    {
        public const string Source = Secret + "/stored.png";
        public const string Thumbnail = Secret + "/thumb.png";
        public string TempPath { get; } = Path.Combine(Path.GetTempPath(), Secret, Guid.NewGuid().ToString("N"));
        public Mock<IBaseRepository<Attachment>> Repository { get; } = new();
        public Mock<IFileStorage> Storage { get; } = new();
        public Mock<IImageProcessor> Image { get; } = new();
        public Mock<IMapper> Mapper { get; } = new();
        public Mock<IUploadRateLimitService> Quota { get; } = new();
        public AttachmentService Service { get; }
        public AttachmentController Controller { get; }
        private readonly MemoryStream _input = new(new byte[] { 1, 2, 3, 4 });
        private readonly Serilog.Extensions.Logging.SerilogLoggerFactory _loggerFactory;
        public IFormFile File => new FormFile(_input, 0, 4, "file", Secret + ".png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
        public Fixture(Serilog.ILogger logger)
        {
            new ServiceCollection().ConfigureApplication();
            Directory.CreateDirectory(TempPath);
            System.IO.File.WriteAllBytes(Path.Combine(TempPath, "source.png"), [1, 2, 3, 4]);
            var result = FileUploadResult.Ok("stored.png", Source, 4, "image/png");
            result.ThumbnailPath = Thumbnail;
            Storage.Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<FileUploadOptionsDto>())).ReturnsAsync(result);
            Storage.Setup(s => s.GetFullPath(Source)).Returns(Path.Combine(TempPath, "source.png"));
            Storage.Setup(s => s.GetFullPath(Thumbnail)).Returns(Path.Combine(TempPath, "thumb.png"));
            Storage.Setup(s => s.DeleteAsync(It.IsAny<string>())).ReturnsAsync(true);
            Repository.Setup(r => r.AddAsync(It.IsAny<Attachment>())).ReturnsAsync(99);
            Mapper.Setup(m => m.Map<AttachmentVo>(It.IsAny<object>())).Returns((object a) => new AttachmentVo { VoId = ((Attachment)a).Id });
            Service = new AttachmentService(Mapper.Object, Repository.Object, Storage.Object, Image.Object, Mock.Of<IAttachmentUrlResolver>(),
                Mock.Of<IChatChannelAccessService>(), Mock.Of<IWikiAttachmentAccessService>(), Options.Create(new FileStorageOptions
                { Deduplication = new DeduplicationOptions { Enable = true }, Watermark = new Radish.Common.OptionTool.WatermarkOptions { Enable = true } }));
            // Confine image helper output and cleanup to this fixture's unique directory.
            typeof(AttachmentService).GetField("_tempPath", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Service, TempPath);
            Quota.Setup(q => q.AcquireUploadAsync(7, It.IsAny<string>(), 4, It.IsAny<TimeSpan?>())).ReturnsAsync(UploadRateLimitCheckResult.Allowed());
            var current = new Mock<ICurrentUserAccessor>();
            current.SetupGet(c => c.Current).Returns(new CurrentUser { UserId = 7, UserName = Secret, Roles = [] });
            var localizer = new Mock<IStringLocalizer<Errors>>();
            localizer.Setup(l => l[It.IsAny<string>()]).Returns((string k) => new LocalizedString(k, k, true));
            localizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string k, object[] _) => new LocalizedString(k, k, true));
            _loggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(logger, dispose: false);
            Controller = new AttachmentController(Service, current.Object, Mock.Of<IUserService>(), Quota.Object, Options.Create(new UploadRateLimitOptions { Enable = true }),
                Mock.Of<IFileAccessTokenService>(), new ConfigurationBuilder().Build(), localizer.Object, _loggerFactory.CreateLogger<AttachmentController>());
            Controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        }
        public Task<MessageModel> Upload(bool image = true) => image
            ? Controller.UploadImage(File, generateThumbnail: false, removeExif: false)
            : Controller.UploadDocument(new FormFile(_input, 0, 4, "file", Secret + ".pdf"));
        public void Dispose()
        {
            _input.Dispose(); _loggerFactory.Dispose();
            if (Directory.Exists(TempPath)) Directory.Delete(TempPath, true);
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
