using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Api.Services;
using Radish.Common.OptionTool;
using Radish.Infrastructure.ImageProcessing;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Repository;
using Radish.Service;
using Serilog;
using Radish.Extension.Log;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SqlSugar;
using Xunit;
using WatermarkOptions = Radish.Infrastructure.ImageProcessing.WatermarkOptions;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class OutboxNativeLoggingTests
{
    private const string Secret = "OUTBOX_NATIVE_PRIVATE_SENTINEL";

    [Fact]
    [Trait("Runtime", "Native")]
    public async Task NativeLibraryIntegration_ShouldExerciseRealAbiAndHostFailurePaths()
    {
        var libraryPath = Environment.GetEnvironmentVariable("RADISH_TEST_NATIVE_LIBRARY");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(libraryPath), "未指定 RADISH_TEST_NATIVE_LIBRARY，跳过真实 Rust 动态库验证");
        Assert.True(Path.IsPathFullyQualified(libraryPath!));
        // 显式加载本次构建产物；句柄由测试进程持有，不向产品目录复制动态库。
        var library = NativeLibrary.Load(libraryPath!);
        NativeLibrary.SetDllImportResolver(typeof(RustImageProcessor).Assembly,
            (name, _, _) => name == "radish_lib" ? library : IntPtr.Zero);
        Assert.True(RustImageProcessor.IsRustLibraryAvailable());

        var directory = Path.Combine(Path.GetTempPath(), $"radish-native-interop-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        using var output = new StringWriter();
        using var logger = CreateLogger(output, true);
        var previous = Log.Logger;
        try
        {
            Log.Logger = logger;
            var hashPath = Path.Combine(directory, "hash.txt");
            await File.WriteAllTextAsync(hashPath, "abc", TestContext.Current.CancellationToken);
            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
                RustImageProcessor.CalculateFileSha256(hashPath));
            Assert.Equal("", output.ToString());

            Assert.Null(RustImageProcessor.CalculateFileSha256(Path.Combine(directory, Secret)));
            using (var failed = JsonDocument.Parse(output.ToString()))
            {
                Assert.Equal("native.failed", failed.RootElement.GetProperty("eventCode").GetString());
                Assert.Equal("native-result", failed.RootElement.GetProperty("properties").GetProperty("nativeReason").GetString());
            }
            output.GetStringBuilder().Clear();

            var inputPath = Path.Combine(directory, "input.png");
            var outputPath = Path.Combine(directory, "output.png");
            using var inputImage = new Image<Rgba32>(128, 128);
            await inputImage.SaveAsPngAsync(inputPath, TestContext.Current.CancellationToken);
            var watermark = Marshal.GetDelegateForFunctionPointer<NativeWatermark>(NativeLibrary.GetExport(library, "add_text_watermark"));
            Assert.Equal(0, watermark(inputPath, outputPath, "native", 12, 0.5f, 0));
            using var resultImage = await Image.LoadAsync(outputPath, TestContext.Current.CancellationToken);
            Assert.Equal(128, resultImage.Width);
            Assert.Equal(128, resultImage.Height);

            // 当前 wrapper 使用 .tmp 输入；验证这一既有路径确实返回原生错误并安全回退。
            using var invalidInput = new MemoryStream(Encoding.UTF8.GetBytes(Secret));
            var processor = new RustImageProcessor(Options.Create(new FileStorageOptions()));
            var result = await processor.AddWatermarkAsync(invalidInput, Path.Combine(directory, "invalid.png"),
                new WatermarkOptions { Type = WatermarkType.Text, Text = Secret });
            Assert.False(result.Success);
            Assert.Single(Lines(output));
            using var fallback = JsonDocument.Parse(output.ToString());
            Assert.Equal("native.fallback", fallback.RootElement.GetProperty("eventCode").GetString());
            Assert.Equal("native-result", fallback.RootElement.GetProperty("properties").GetProperty("nativeReason").GetString());
            Assert.DoesNotContain(Secret, output.ToString());
        }
        finally
        {
            Log.Logger = previous;
            Directory.Delete(directory, recursive: true);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NativeWatermark(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string inputPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        uint fontSize, float opacity, byte position);

    [Theory]
    [InlineData(false, "main")]
    [InlineData(true, "main")]
    [InlineData(false, "chat")]
    [InlineData(true, "chat")]
    public async Task Outbox_ShouldLogPersistedRetryAndDeadLetterOnce_WithoutPayload(bool candidate, string source)
    {
        using var db = CreateDatabase();
        var repository = new ReliableOutboxRepository(db);
        var service = new ReliableOutboxService(repository);
        var now = DateTime.UtcNow;
        var id = await repository.AddAsync(new ReliableOutboxDraft(source, 0, ReliableTaskTypes.PostPublished,
            1, Secret, "Post", Secret, Secret, now, MaxAttempts: 2));
        await repository.ClaimDueAsync(source, 10, Secret, now, TimeSpan.FromMinutes(5));
        var processor = new Mock<IReliableTaskProcessor>();
        processor.Setup(p => p.ProcessAsync(It.IsAny<ReliableOutboxSnapshot>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(Secret));
        using var output = new StringWriter();
        using var logger = CreateLogger(output, candidate);
        using var factory = LoggerFactory.Create(builder => builder.AddSerilog(logger));
        var job = new ReliableOutboxExecutionJob(service, processor.Object,
            Mock.Of<IContentModerationCaseRepository>());
        var previous = Log.Logger;
        try
        {
            Log.Logger = logger;
            await job.ExecuteAsync(source, id, TestContext.Current.CancellationToken);
            Assert.Equal(ReliableOutboxStatuses.Pending, (await repository.QueryByIdAsync(source, id))!.Status);
            Assert.Single(Lines(output));
            Assert.Contains("outbox.retrying", output.ToString());
            // 尚未重新领取的重复执行以及无效失败写入不能伪造新事件。
            await job.ExecuteAsync(source, id, TestContext.Current.CancellationToken);
            await repository.MarkFailedAsync(source, id, Secret, Secret, now, null);
            Assert.Single(Lines(output));
            await repository.ClaimDueAsync(source, 10, Secret, now.AddHours(1), TimeSpan.FromMinutes(5));
            await job.ExecuteAsync(source, id, TestContext.Current.CancellationToken);
            var final = await repository.QueryByIdAsync(source, id);
            Assert.Equal(ReliableOutboxStatuses.DeadLetter, final!.Status);
            Assert.Equal(2, final.AttemptCount);
        }
        finally { Log.Logger = previous; }
        Assert.Equal(2, Lines(output).Length);
        Assert.Contains("outbox.dead_letter", output.ToString());
        Assert.DoesNotContain(Secret, output.ToString());
        if (candidate)
        {
            using var retry = JsonDocument.Parse(Lines(output)[0]);
            using var failed = JsonDocument.Parse(Lines(output)[1]);
            Assert.Equal("Warning", retry.RootElement.GetProperty("level").GetString());
            Assert.Equal("Error", failed.RootElement.GetProperty("level").GetString());
            Assert.Equal(retry.RootElement.GetProperty("operationId").GetString(), failed.RootElement.GetProperty("operationId").GetString());
            Assert.Equal(source, failed.RootElement.GetProperty("properties").GetProperty("databaseScope").GetString());
            Assert.Equal("invalid-operation", failed.RootElement.GetProperty("properties").GetProperty("failureKind").GetString());
        }
    }

    [Fact]
    public async Task PermanentFailure_ShouldRemainInAudit_WithoutLeakingToRuntimeLog()
    {
        using var db = CreateDatabase();
        var repository = new ReliableOutboxRepository(db);
        var service = new ReliableOutboxService(repository);
        var now = DateTime.UtcNow;
        var id = await repository.AddAsync(new ReliableOutboxDraft("main", 0, ReliableTaskTypes.PostPublished,
            1, Secret, "Post", Secret, "{}", now));
        await repository.ClaimDueAsync("main", 10, Secret, now, TimeSpan.FromMinutes(5));
        using var output = new StringWriter();
        using var logger = CreateLogger(output, true);
        var previous = Log.Logger;
        try
        {
            Log.Logger = logger;
            await service.MarkFailedAsync("main", id, new PermanentReliableTaskException(Secret), now);
        }
        finally { Log.Logger = previous; }
        var value = await repository.QueryByIdAsync("main", id);
        Assert.Equal(ReliableOutboxStatuses.DeadLetter, value!.Status);
        Assert.Equal(Secret, value.LastErrorSummary);
        Assert.Single(Lines(output));
        Assert.DoesNotContain(Secret, output.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Dispatcher_ShouldOnlySummarizeActualDispatch(int count)
    {
        var service = new Mock<IReliableOutboxService>();
        var messages = Enumerable.Range(0, count).Select(i => new ReliableOutboxSnapshot("main", i, 0,
            Secret, 1, Secret, Secret, Secret, Secret, ReliableOutboxStatuses.Processing,
            0, 6, DateTime.UtcNow, DateTime.UtcNow, null, null)).ToArray();
        service.Setup(s => s.ClaimDueAsync(ReliableOutboxSources.Main, 50, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(messages);
        service.Setup(s => s.ClaimDueAsync(ReliableOutboxSources.Chat, 50, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(Array.Empty<ReliableOutboxSnapshot>());
        using var output = new StringWriter();
        using var logger = CreateLogger(output, true);
        using var factory = LoggerFactory.Create(builder => builder.AddSerilog(logger));
        var client = new Mock<IBackgroundJobClient>();
        var job = new ReliableOutboxDispatcherJob(service.Object, client.Object, factory.CreateLogger<ReliableOutboxDispatcherJob>());
        Assert.Equal(count, await job.DispatchAsync());
        Assert.Equal(count > 0 ? 1 : 0, Lines(output).Length);
        Assert.DoesNotContain(Secret, output.ToString());
        client.Verify(c => c.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()), Times.Exactly(count));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeHashFailure_ShouldReturnNull_AndEmitOneSafeHostEvent(bool candidate)
    {
        using var output = new StringWriter();
        using var logger = CreateLogger(output, candidate);
        var previous = Log.Logger;
        try
        {
            Log.Logger = logger;
            Assert.Null(RustImageProcessor.CalculateFileSha256(Path.Combine(Path.GetTempPath(), Secret, Guid.NewGuid().ToString())));
        }
        finally { Log.Logger = previous; }
        Assert.Single(Lines(output));
        Assert.Contains("native.failed", output.ToString());
        Assert.DoesNotContain(Secret, output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeWatermarkFailure_ShouldKeepFallbackResult_WithoutRawException(bool candidate)
    {
        using var output = new StringWriter();
        using var logger = CreateLogger(output, candidate);
        var processor = new RustImageProcessor(Options.Create(new FileStorageOptions()));
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(Secret));
        var path = Path.Combine(Path.GetTempPath(), $"{Secret}-{Guid.NewGuid():N}.png");
        var previous = Log.Logger;
        try
        {
            Log.Logger = logger;
            var result = await processor.AddWatermarkAsync(input, path, new WatermarkOptions { Type = WatermarkType.Text, Text = Secret });
            Assert.False(result.Success);
        }
        finally
        {
            Log.Logger = previous;
            if (File.Exists(path)) File.Delete(path);
        }
        Assert.Single(Lines(output));
        Assert.Contains("native.fallback", output.ToString());
        Assert.DoesNotContain(Secret, output.ToString());
    }

    [Fact]
    public async Task ImageWatermark_ShouldUseNormalManagedRoute_WithoutDegradationWarning()
    {
        using var output = new StringWriter();
        using var logger = CreateLogger(output, true);
        using var input = new MemoryStream();
        using var image = new Image<Rgba32>(4, 4);
        await image.SaveAsPngAsync(input, TestContext.Current.CancellationToken);
        var path = Path.Combine(Path.GetTempPath(), $"radish-l2-image-{Guid.NewGuid():N}.png");
        var previous = Log.Logger;
        try
        {
            Log.Logger = logger;
            var result = await new RustImageProcessor(Options.Create(new FileStorageOptions())).AddWatermarkAsync(
                input, path, new WatermarkOptions { Type = WatermarkType.Image });
            Assert.True(result.Success);
            Assert.Equal("", output.ToString());
        }
        finally
        {
            Log.Logger = previous;
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static SqlSugarScope CreateDatabase()
    {
        var db = new SqlSugarScope(new[] { "main", "chat" }.Select(id => new ConnectionConfig
        {
            ConfigId = id, ConnectionString = "Data Source=:memory:", DbType = DbType.Sqlite,
            IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
        }).ToList());
        db.GetConnectionScope("main").CodeFirst.InitTables<ReliableOutboxMessage>();
        db.GetConnectionScope("chat").CodeFirst.InitTables<ChatReliableOutboxMessage>();
        return db;
    }

    private static string[] Lines(StringWriter output) => output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static Serilog.Core.Logger CreateLogger(TextWriter output, bool candidate)
    {
        var configuration = new LoggerConfiguration();
        if (candidate)
            RuntimeLoggingConfiguration.Configure(configuration, new ConfigurationBuilder().Build(), "Production", "api", output, output);
        else configuration.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(output));
        return configuration.CreateLogger();
    }

    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter =
            new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }
}
