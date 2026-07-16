using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using Radish.Common.Exceptions;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.Constants;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class ChunkedUploadServiceTest : IDisposable
{
    private readonly List<string> _tempPaths = [];

    [Fact(DisplayName = "禁用分片上传时不创建临时目录或限流预留")]
    public async Task CreateSessionAsync_ShouldFailWithoutSideEffects_WhenDisabled()
    {
        var harness = CreateHarness(enable: false);

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            harness.Service.CreateSessionAsync(CreateRequest(), 7, "tester"));

        exception.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        exception.ErrorCode.ShouldBe(AttachmentErrorCodes.ChunkedUploadDisabled);
        Directory.Exists(harness.TempPath).ShouldBeFalse();
        harness.RateLimit.Verify(
            service => service.AcquireUploadAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    [Fact(DisplayName = "创建会话使用服务端默认分片并规范业务类型")]
    public async Task CreateSessionAsync_ShouldValidateAndReserveBeforePersisting()
    {
        var harness = CreateHarness();
        var request = CreateRequest(totalSize: 5, chunkSize: 0, businessType: "post");

        var result = await harness.Service.CreateSessionAsync(request, 7, "tester");

        result.VoChunkSize.ShouldBe(2);
        result.VoTotalChunks.ShouldBe(3);
        harness.Session.ShouldNotBeNull();
        harness.Session!.BusinessType.ShouldBe(AttachmentBusinessTypes.Post);
        harness.Session.BusinessId.ShouldBeNull();
        Directory.Exists(Path.Combine(harness.TempPath, result.VoSessionId)).ShouldBeTrue();
        harness.RateLimit.Verify(service => service.AcquireUploadAsync(
            7,
            result.VoSessionId,
            5,
            It.Is<TimeSpan?>(lifetime => lifetime == TimeSpan.FromHours(24.5))), Times.Once);
    }

    [Theory(DisplayName = "创建会话在写磁盘和预留前拒绝不安全参数")]
    [InlineData("bad.svg", 5, null)]
    [InlineData("safe.txt", 11, null)]
    [InlineData("safe.txt", 5, 9L)]
    public async Task CreateSessionAsync_ShouldRejectInvalidRequest(
        string fileName,
        long totalSize,
        long? businessId)
    {
        var harness = CreateHarness();
        var request = CreateRequest(fileName, totalSize, 2);
        request.BusinessId = businessId;

        await Should.ThrowAsync<BusinessException>(() =>
            harness.Service.CreateSessionAsync(request, 7, "tester"));

        harness.Session.ShouldBeNull();
        harness.RateLimit.Verify(
            service => service.AcquireUploadAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    [Fact(DisplayName = "创建会话立即拒绝图片专属业务的非图片文件")]
    public async Task CreateSessionAsync_ShouldRejectNonImageForAvatar()
    {
        var harness = CreateHarness();
        var request = CreateRequest("avatar.pdf", 5, 2, AttachmentBusinessTypes.Avatar);
        request.MimeType = "application/pdf";

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            harness.Service.CreateSessionAsync(request, 7, "tester"));

        exception.StatusCode.ShouldBe(StatusCodes.Status415UnsupportedMediaType);
        exception.ErrorCode.ShouldBe(AttachmentErrorCodes.UnsupportedMediaType);
        harness.RateLimit.Verify(
            service => service.AcquireUploadAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    [Fact(DisplayName = "分片必须符合索引对应的精确长度且重复上传幂等")]
    public async Task UploadChunkAsync_ShouldEnforceExactLengthAndBeIdempotent()
    {
        var harness = CreateHarness();
        var session = await harness.Service.CreateSessionAsync(CreateRequest(totalSize: 5, chunkSize: 2), 7, "tester");

        var invalid = await Should.ThrowAsync<BusinessException>(() =>
            harness.Service.UploadChunkAsync(session.VoSessionId, 2, CreateFormFile([1, 2]), 7));
        invalid.ErrorCode.ShouldBe(AttachmentErrorCodes.UploadChunkInvalid);

        var first = await harness.Service.UploadChunkAsync(
            session.VoSessionId,
            2,
            CreateFormFile([1]),
            7);
        var duplicate = await harness.Service.UploadChunkAsync(
            session.VoSessionId,
            2,
            CreateFormFile([1]),
            7);

        first.VoUploadedChunkIndexes.ShouldBe([2]);
        duplicate.VoUploadedChunkIndexes.ShouldBe([2]);
        harness.Repository.Verify(repository => repository.UpdateAsync(It.IsAny<UploadSession>()), Times.Once);
    }

    [Fact(DisplayName = "同一会话并发上传不同分片不会丢索引")]
    public async Task UploadChunkAsync_ShouldSerializeSessionMutation()
    {
        var harness = CreateHarness();
        var session = await harness.Service.CreateSessionAsync(CreateRequest(totalSize: 4, chunkSize: 2), 7, "tester");

        await Task.WhenAll(
            harness.Service.UploadChunkAsync(session.VoSessionId, 0, CreateFormFile([1, 2]), 7),
            harness.Service.UploadChunkAsync(session.VoSessionId, 1, CreateFormFile([3, 4]), 7));

        harness.Session.ShouldNotBeNull();
        var indexes = JsonConvert.DeserializeObject<List<int>>(harness.Session!.UploadedChunkIndexes!);
        indexes.ShouldBe([0, 1]);
        harness.Session.UploadedChunks.ShouldBe(2);
    }

    [Fact(DisplayName = "会话记录创建失败时清理目录并释放配额预留")]
    public async Task CreateSessionAsync_ShouldRollbackSideEffects_WhenRepositoryRejectsInsert()
    {
        var harness = CreateHarness();
        harness.AddResult = 0;

        await Should.ThrowAsync<InvalidOperationException>(() =>
            harness.Service.CreateSessionAsync(CreateRequest(), 7, "tester"));

        Directory.EnumerateDirectories(harness.TempPath).ShouldBeEmpty();
        harness.RateLimit.Verify(
            service => service.FailUploadAsync(7, It.IsAny<string>()),
            Times.Once);
    }

    [Fact(DisplayName = "分片状态更新被拒绝时删除未提交分片以便安全重试")]
    public async Task UploadChunkAsync_ShouldDeleteChunk_WhenRepositoryRejectsUpdate()
    {
        var harness = CreateHarness();
        var session = await harness.Service.CreateSessionAsync(CreateRequest(totalSize: 2, chunkSize: 2), 7, "tester");
        harness.RejectNextUpdate = true;

        await Should.ThrowAsync<InvalidOperationException>(() =>
            harness.Service.UploadChunkAsync(session.VoSessionId, 0, CreateFormFile([1, 2]), 7));

        File.Exists(Path.Combine(harness.TempPath, session.VoSessionId, "chunk_0")).ShouldBeFalse();
        harness.Session!.UploadedChunks.ShouldBe(0);
    }

    [Fact(DisplayName = "取消会话可重试并始终尝试释放预留")]
    public async Task CancelSessionAsync_ShouldReleaseReservationIdempotently()
    {
        var harness = CreateHarness();
        var session = await harness.Service.CreateSessionAsync(CreateRequest(), 7, "tester");

        await harness.Service.CancelSessionAsync(session.VoSessionId, 7);
        await harness.Service.CancelSessionAsync(session.VoSessionId, 7);

        harness.Session!.Status.ShouldBe("Cancelled");
        Directory.Exists(Path.Combine(harness.TempPath, session.VoSessionId)).ShouldBeFalse();
        harness.RateLimit.Verify(
            service => service.FailUploadAsync(7, session.VoSessionId),
            Times.Exactly(2));
    }

    [Fact(DisplayName = "取消状态未落库时保留临时文件与配额预留")]
    public async Task CancelSessionAsync_ShouldKeepResources_WhenRepositoryRejectsUpdate()
    {
        var harness = CreateHarness();
        var session = await harness.Service.CreateSessionAsync(CreateRequest(), 7, "tester");
        harness.RejectNextUpdate = true;

        await Should.ThrowAsync<InvalidOperationException>(() =>
            harness.Service.CancelSessionAsync(session.VoSessionId, 7));

        Directory.Exists(Path.Combine(harness.TempPath, session.VoSessionId)).ShouldBeTrue();
        harness.RateLimit.Verify(
            service => service.FailUploadAsync(It.IsAny<long>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact(DisplayName = "过期会话拒绝新分片并释放预留及临时文件")]
    public async Task UploadChunkAsync_ShouldExpireAndReleaseSession()
    {
        var harness = CreateHarness();
        var session = await harness.Service.CreateSessionAsync(CreateRequest(), 7, "tester");
        harness.TimeProvider.SetUtcNow(harness.TimeProvider.GetUtcNow().AddHours(25));

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            harness.Service.UploadChunkAsync(session.VoSessionId, 0, CreateFormFile([1, 2]), 7));

        exception.StatusCode.ShouldBe(StatusCodes.Status410Gone);
        harness.Session!.Status.ShouldBe("Expired");
        Directory.Exists(Path.Combine(harness.TempPath, session.VoSessionId)).ShouldBeFalse();
        harness.RateLimit.Verify(service => service.FailUploadAsync(7, session.VoSessionId), Times.Once);
    }

    [Fact(DisplayName = "后台清理跨租户会话并在状态落库后释放资源")]
    public async Task CleanupExpiredSessionsAsync_ShouldProcessCrossTenantSession()
    {
        var harness = CreateHarness();
        var session = await harness.Service.CreateSessionAsync(CreateRequest(), 7, "tester");
        harness.Session!.TenantId = 42;
        harness.TimeProvider.SetUtcNow(harness.TimeProvider.GetUtcNow().AddHours(25));

        await harness.Service.CleanupExpiredSessionsAsync();

        harness.Session.Status.ShouldBe("Expired");
        Directory.Exists(Path.Combine(harness.TempPath, session.VoSessionId)).ShouldBeFalse();
        harness.Repository.Verify(repository => repository.TryMarkExpiredAcrossTenantsAsync(
            session.VoSessionId,
            42,
            7,
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>()), Times.Once);
        harness.RateLimit.Verify(service => service.FailUploadAsync(7, session.VoSessionId), Times.Once);
    }

    [Fact(DisplayName = "后台对账重试清理终态会话目录和配额")]
    public async Task CleanupExpiredSessionsAsync_ShouldReconcileTerminalDirectory()
    {
        var harness = CreateHarness();
        var session = await harness.Service.CreateSessionAsync(CreateRequest(), 7, "tester");
        await harness.Service.CancelSessionAsync(session.VoSessionId, 7);
        Directory.CreateDirectory(Path.Combine(harness.TempPath, session.VoSessionId));

        await harness.Service.CleanupExpiredSessionsAsync();

        Directory.Exists(Path.Combine(harness.TempPath, session.VoSessionId)).ShouldBeFalse();
        harness.RateLimit.Verify(
            service => service.FailUploadAsync(7, session.VoSessionId),
            Times.Exactly(2));
    }

    [Fact(DisplayName = "后台对账在宽限期后清理没有数据库记录的孤儿目录")]
    public async Task CleanupExpiredSessionsAsync_ShouldRemoveAgedOrphanDirectory()
    {
        var harness = CreateHarness();
        var orphanSessionId = Guid.NewGuid().ToString("N");
        var orphanDirectory = Path.Combine(harness.TempPath, orphanSessionId);
        Directory.CreateDirectory(orphanDirectory);
        Directory.SetLastWriteTimeUtc(
            orphanDirectory,
            harness.TimeProvider.GetUtcNow().UtcDateTime.Subtract(TimeSpan.FromHours(1)));

        await harness.Service.CleanupExpiredSessionsAsync();

        Directory.Exists(orphanDirectory).ShouldBeFalse();
    }

    [Fact(DisplayName = "合并成功结算预留并清理临时目录")]
    public async Task MergeChunksAsync_ShouldCompleteReservationAndCleanup()
    {
        var harness = CreateHarness();
        var session = await harness.Service.CreateSessionAsync(CreateRequest(totalSize: 4, chunkSize: 2), 7, "tester");
        await harness.Service.UploadChunkAsync(session.VoSessionId, 0, CreateFormFile([1, 2]), 7);
        await harness.Service.UploadChunkAsync(session.VoSessionId, 1, CreateFormFile([3, 4]), 7);

        var attachment = await harness.Service.MergeChunksAsync(
            new MergeChunksDto { SessionId = session.VoSessionId, GenerateThumbnail = false, RemoveExif = false },
            7,
            "tester");

        attachment.ShouldNotBeNull();
        attachment!.VoId.ShouldBe(99);
        harness.Session!.Status.ShouldBe("Completed");
        Directory.Exists(Path.Combine(harness.TempPath, session.VoSessionId)).ShouldBeFalse();
        harness.RateLimit.Verify(service => service.CompleteUploadAsync(7, session.VoSessionId), Times.Once);
    }

    [Fact(DisplayName = "已完成会话重试合并时返回既有附件且不重复持久化")]
    public async Task MergeChunksAsync_ShouldReturnExistingAttachment_WhenAlreadyCompleted()
    {
        var harness = CreateHarness();
        var session = await harness.Service.CreateSessionAsync(CreateRequest(totalSize: 2, chunkSize: 2), 7, "tester");
        await harness.Service.UploadChunkAsync(session.VoSessionId, 0, CreateFormFile([1, 2]), 7);
        var request = new MergeChunksDto
        {
            SessionId = session.VoSessionId,
            GenerateThumbnail = false,
            RemoveExif = false
        };

        var first = await harness.Service.MergeChunksAsync(request, 7, "tester");
        var retried = await harness.Service.MergeChunksAsync(request, 7, "tester");

        first!.VoId.ShouldBe(99);
        retried!.VoId.ShouldBe(99);
        harness.Attachment.Verify(service => service.UploadFileAsync(
            It.IsAny<IFormFile>(),
            It.IsAny<FileUploadOptionsDto>(),
            It.IsAny<long>(),
            It.IsAny<string>()), Times.Once);
        harness.Attachment.Verify(service => service.QueryByIdAsync(99), Times.Once);
        harness.RateLimit.Verify(
            service => service.CompleteUploadAsync(7, session.VoSessionId),
            Times.Exactly(2));
    }

    [Fact(DisplayName = "附件持久化后会话回写持续失败时当前响应成功并保留待恢复状态")]
    public async Task MergeChunksAsync_ShouldReturnPersistedAttachment_WhenSessionCompletionUpdateFails()
    {
        var harness = CreateHarness();
        var session = await harness.Service.CreateSessionAsync(CreateRequest(totalSize: 2, chunkSize: 2), 7, "tester");
        await harness.Service.UploadChunkAsync(session.VoSessionId, 0, CreateFormFile([1, 2]), 7);
        harness.FailCompletedSessionUpdate = true;

        var attachment = await harness.Service.MergeChunksAsync(
            new MergeChunksDto { SessionId = session.VoSessionId, GenerateThumbnail = false, RemoveExif = false },
            7,
            "tester");

        attachment.ShouldNotBeNull();
        attachment!.VoId.ShouldBe(99);
        harness.Session!.Status.ShouldBe("Uploading");
        harness.Session.AttachmentId.ShouldBeNull();
        Directory.Exists(Path.Combine(harness.TempPath, session.VoSessionId)).ShouldBeFalse();
        harness.RateLimit.Verify(service => service.CompleteUploadAsync(7, session.VoSessionId), Times.Once);
        harness.RateLimit.Verify(
            service => service.FailUploadAsync(It.IsAny<long>(), It.IsAny<string>()),
            Times.Never);
    }

    private Harness CreateHarness(bool enable = true)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "radish-chunk-tests", Guid.NewGuid().ToString("N"));
        _tempPaths.Add(tempPath);
        return new Harness(tempPath, enable);
    }

    private static CreateUploadSessionDto CreateRequest(
        string fileName = "safe.txt",
        long totalSize = 4,
        int chunkSize = 2,
        string businessType = AttachmentBusinessTypes.General)
    {
        return new CreateUploadSessionDto
        {
            FileName = fileName,
            TotalSize = totalSize,
            MimeType = "text/plain",
            ChunkSize = chunkSize,
            BusinessType = businessType
        };
    }

    private static IFormFile CreateFormFile(byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "chunkData", "chunk.bin");
    }

    public void Dispose()
    {
        foreach (var path in _tempPaths)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    private sealed class Harness
    {
        public Harness(string tempPath, bool enable)
        {
            TempPath = tempPath;
            Repository = new Mock<IUploadSessionRepository>(MockBehavior.Loose);
            Repository
                .Setup(repository => repository.AddAsync(It.IsAny<UploadSession>()))
                .Returns<UploadSession>(session =>
                {
                    if (AddResult <= 0)
                    {
                        return Task.FromResult(AddResult);
                    }

                    session.Id = AddResult;
                    Session = CloneSession(session);
                    return Task.FromResult(AddResult);
                });
            Repository
                .Setup(repository => repository.QueryFirstAsync(
                    It.IsAny<Expression<Func<UploadSession, bool>>?>()))
                .Returns<Expression<Func<UploadSession, bool>>?>(predicate =>
                {
                    var session = Session;
                    return Task.FromResult(
                        session != null && (predicate == null || predicate.Compile()(session))
                            ? CloneSession(session)
                            : null);
                });
            Repository
                .Setup(repository => repository.QueryAsync(
                    It.IsAny<Expression<Func<UploadSession, bool>>?>()))
                .Returns<Expression<Func<UploadSession, bool>>?>(predicate =>
                {
                    var session = Session;
                    var result = session != null && (predicate == null || predicate.Compile()(session))
                        ? new List<UploadSession> { CloneSession(session) }
                        : [];
                    return Task.FromResult(result);
                });
            Repository
                .Setup(repository => repository.QueryExpiredAcrossTenantsAsync(It.IsAny<DateTime>()))
                .Returns<DateTime>(expiredBeforeUtc =>
                {
                    var session = Session;
                    var result = session != null &&
                                 session.ExpiresAt < expiredBeforeUtc &&
                                 (session.Status == "Uploading" || session.Status == "Failed")
                        ? new List<UploadSession> { CloneSession(session) }
                        : [];
                    return Task.FromResult(result);
                });
            Repository
                .Setup(repository => repository.QueryBySessionIdsAcrossTenantsAsync(It.IsAny<List<string>>()))
                .Returns<List<string>>(sessionIds =>
                {
                    var session = Session;
                    var result = session != null && sessionIds.Contains(session.SessionId)
                        ? new List<UploadSession> { CloneSession(session) }
                        : [];
                    return Task.FromResult(result);
                });
            Repository
                .Setup(repository => repository.QueryTerminalForSettlementAcrossTenantsAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<int>()))
                .Returns<DateTime, int>((modifiedAfterUtc, _) =>
                {
                    var session = Session;
                    var activityTime = session?.ModifyTime ?? session?.CreateTime;
                    var result = session != null &&
                                 activityTime >= modifiedAfterUtc &&
                                 (session.Status == "Completed" ||
                                  session.Status == "Cancelled" ||
                                  session.Status == "Expired" ||
                                  session.Status == "Failed")
                        ? new List<UploadSession> { CloneSession(session) }
                        : [];
                    return Task.FromResult(result);
                });
            Repository
                .Setup(repository => repository.TryMarkExpiredAcrossTenantsAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<long>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>()))
                .Returns<string, long, long, DateTime, DateTime>((sessionId, tenantId, userId, expiredBeforeUtc, modifiedAtUtc) =>
                {
                    var session = Session;
                    if (session == null ||
                        session.SessionId != sessionId ||
                        session.TenantId != tenantId ||
                        session.UserId != userId ||
                        session.ExpiresAt >= expiredBeforeUtc ||
                        (session.Status != "Uploading" && session.Status != "Failed"))
                    {
                        return Task.FromResult(false);
                    }

                    session.Status = "Expired";
                    session.ErrorMessage = "上传会话已过期";
                    session.ModifyTime = modifiedAtUtc;
                    return Task.FromResult(true);
                });
            Repository
                .Setup(repository => repository.UpdateAsync(It.IsAny<UploadSession>()))
                .Returns<UploadSession>(session =>
                {
                    if (RejectNextUpdate)
                    {
                        RejectNextUpdate = false;
                        return Task.FromResult(false);
                    }

                    if (FailCompletedSessionUpdate && session.Status == "Completed")
                    {
                        throw new InvalidOperationException("database unavailable");
                    }

                    Session = CloneSession(session);
                    return Task.FromResult(true);
                });

            RateLimit = new Mock<IUploadRateLimitService>(MockBehavior.Strict);
            RateLimit
                .Setup(service => service.AcquireUploadAsync(
                    It.IsAny<long>(),
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(UploadRateLimitCheckResult.Allowed());
            RateLimit
                .Setup(service => service.CompleteUploadAsync(It.IsAny<long>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            RateLimit
                .Setup(service => service.FailUploadAsync(It.IsAny<long>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            Attachment = new Mock<IAttachmentService>(MockBehavior.Strict);
            Attachment
                .Setup(service => service.UploadFileAsync(
                    It.IsAny<IFormFile>(),
                    It.IsAny<FileUploadOptionsDto>(),
                    It.IsAny<long>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new AttachmentVo { VoId = 99 });
            Attachment
                .Setup(service => service.QueryByIdAsync(99))
                .ReturnsAsync(new AttachmentVo { VoId = 99 });
            TimeProvider = new MutableTimeProvider(
                new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero));
            Service = new ChunkedUploadService(
                Repository.Object,
                Attachment.Object,
                RateLimit.Object,
                Options.Create(new ChunkedUploadOptions
                {
                    Enable = enable,
                    DefaultChunkSize = 2,
                    MinChunkSize = 1,
                    MaxChunkSize = 10,
                    SessionExpirationHours = 24,
                    TempChunkPath = tempPath
                }),
                Options.Create(new FileStorageOptions
                {
                    MaxFileSize = new MaxFileSizeOptions
                    {
                        Avatar = 10,
                        Image = 10,
                        Document = 10,
                        Video = 10,
                        Audio = 10
                    }
                }),
                TimeProvider);
        }

        public string TempPath { get; }

        public Mock<IUploadSessionRepository> Repository { get; }

        public Mock<IUploadRateLimitService> RateLimit { get; }

        public Mock<IAttachmentService> Attachment { get; }

        public MutableTimeProvider TimeProvider { get; }

        public ChunkedUploadService Service { get; }

        public UploadSession? Session { get; private set; }

        public long AddResult { get; set; } = 1;

        public bool RejectNextUpdate { get; set; }

        public bool FailCompletedSessionUpdate { get; set; }

        private static UploadSession CloneSession(UploadSession session)
        {
            return JsonConvert.DeserializeObject<UploadSession>(
                JsonConvert.SerializeObject(session))!;
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void SetUtcNow(DateTimeOffset value)
        {
            _utcNow = value.ToUniversalTime();
        }
    }
}
