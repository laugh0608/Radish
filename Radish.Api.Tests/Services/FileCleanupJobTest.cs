using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.Infrastructure.FileStorage;
using Radish.IService;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Service.Jobs;
using Xunit;

namespace Radish.Api.Tests.Services;

public class FileCleanupJobTest
{
    [Fact]
    public async Task CleanupTempFilesAsync_Should_Preserve_Chunk_Files_And_Empty_Session_Directories()
    {
        var now = new DateTime(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);
        var dataBasesPath = Path.Combine(
            Path.GetTempPath(),
            $"radish-file-cleanup-{Guid.NewGuid():N}");
        var tempPath = Path.Combine(dataBasesPath, "Temp");
        var chunkPath = Path.Combine(tempPath, "Chunks");
        var chunkSessionPath = Path.Combine(chunkPath, Guid.NewGuid().ToString("N"));
        var emptySessionPath = Path.Combine(chunkPath, Guid.NewGuid().ToString("N"));
        var oldChunkPath = Path.Combine(chunkSessionPath, "chunk_0");
        var ordinaryDirectory = Path.Combine(tempPath, "ordinary");
        var oldOrdinaryPath = Path.Combine(ordinaryDirectory, "old.tmp");

        try
        {
            Directory.CreateDirectory(chunkSessionPath);
            Directory.CreateDirectory(emptySessionPath);
            Directory.CreateDirectory(ordinaryDirectory);
            await File.WriteAllTextAsync(
                oldChunkPath,
                "chunk",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                oldOrdinaryPath,
                "ordinary",
                TestContext.Current.CancellationToken);
            File.SetLastWriteTimeUtc(oldChunkPath, now.AddHours(-3));
            File.SetLastWriteTimeUtc(oldOrdinaryPath, now.AddHours(-3));

            var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Loose);
            var attachmentReferenceInspector = new Mock<IAttachmentReferenceInspector>(MockBehavior.Loose);
            var fileStorage = new Mock<IFileStorage>(MockBehavior.Loose);
            var job = CreateJob(
                attachmentRepository,
                attachmentReferenceInspector,
                fileStorage,
                now,
                dataBasesPath,
                chunkPath);

            var cleanedCount = await job.CleanupTempFilesAsync(2);

            Assert.Equal(1, cleanedCount);
            Assert.True(File.Exists(oldChunkPath));
            Assert.True(Directory.Exists(emptySessionPath));
            Assert.False(File.Exists(oldOrdinaryPath));
        }
        finally
        {
            if (Directory.Exists(dataBasesPath))
            {
                Directory.Delete(dataBasesPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CleanupOrphanAttachmentsAsync_Should_Skip_Attachments_Referenced_By_Sticker()
    {
        var now = DateTime.UtcNow;
        var orphanAttachment = new Attachment
        {
            Id = 1001,
            StoragePath = "Sticker/2026/03/1001.jpg",
            ThumbnailPath = "Sticker/2026/03/1001_thumb.jpg",
            CreateTime = now.AddDays(-2),
            IsDeleted = false,
            BusinessId = null
        };

        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        attachmentRepository
            .Setup(r => r.QueryAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Attachment, bool>>>()))
            .ReturnsAsync(new List<Attachment> { orphanAttachment });

        var attachmentReferenceInspector = new Mock<IAttachmentReferenceInspector>(MockBehavior.Strict);
        attachmentReferenceInspector
            .Setup(inspector => inspector.GetReferencedAttachmentIdsAsync(It.IsAny<IReadOnlyCollection<long>>()))
            .ReturnsAsync(new HashSet<long> { orphanAttachment.Id });

        var fileStorage = new Mock<IFileStorage>(MockBehavior.Loose);
        var job = CreateJob(attachmentRepository, attachmentReferenceInspector, fileStorage, now);

        var cleanedCount = await job.CleanupOrphanAttachmentsAsync(24);

        Assert.Equal(0, cleanedCount);
        attachmentRepository.Verify(r => r.UpdateAsync(It.IsAny<Attachment>()), Times.Never);
        fileStorage.Verify(s => s.GetFullPath(It.IsAny<string>()), Times.Never);

        attachmentRepository.VerifyAll();
        attachmentReferenceInspector.VerifyAll();
    }

    [Fact]
    public async Task CleanupOrphanAttachmentsAsync_Should_Skip_Attachments_Referenced_By_Chat_And_AnswerContent()
    {
        var now = DateTime.UtcNow;
        var chatAttachment = new Attachment
        {
            Id = 1002,
            StoragePath = "Chat/2026/03/1002.png",
            CreateTime = now.AddDays(-2),
            IsDeleted = false,
            BusinessId = null
        };
        var answerAttachment = new Attachment
        {
            Id = 1003,
            ThumbnailPath = "Comment/2026/03/1003_thumb.jpg",
            StoragePath = "Comment/2026/03/1003.jpg",
            CreateTime = now.AddDays(-2),
            IsDeleted = false,
            BusinessId = null
        };

        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        attachmentRepository
            .Setup(r => r.QueryAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Attachment, bool>>>()))
            .ReturnsAsync(new List<Attachment> { chatAttachment, answerAttachment });

        var attachmentReferenceInspector = new Mock<IAttachmentReferenceInspector>(MockBehavior.Strict);
        attachmentReferenceInspector
            .Setup(inspector => inspector.GetReferencedAttachmentIdsAsync(It.IsAny<IReadOnlyCollection<long>>()))
            .ReturnsAsync(new HashSet<long> { chatAttachment.Id, answerAttachment.Id });

        var fileStorage = new Mock<IFileStorage>(MockBehavior.Loose);
        var job = CreateJob(attachmentRepository, attachmentReferenceInspector, fileStorage, now);

        var cleanedCount = await job.CleanupOrphanAttachmentsAsync(24);

        Assert.Equal(0, cleanedCount);
        attachmentRepository.Verify(r => r.UpdateAsync(It.IsAny<Attachment>()), Times.Never);
        fileStorage.Verify(s => s.GetFullPath(It.IsAny<string>()), Times.Never);

        attachmentRepository.VerifyAll();
        attachmentReferenceInspector.VerifyAll();
    }

    private static FileCleanupJob CreateJob(
        Mock<IBaseRepository<Attachment>> attachmentRepository,
        Mock<IAttachmentReferenceInspector> attachmentReferenceInspector,
        Mock<IFileStorage> fileStorage,
        DateTime nowUtc,
        string? dataBasesPath = null,
        string? chunkPath = null)
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(nowUtc));
        var calendar = new BusinessCalendar(
            timeProvider,
            Options.Create(new TimeOptions { DefaultTimeZoneId = "Asia/Shanghai" }));
        var chunkedUploadOptions = Options.Create(new ChunkedUploadOptions
        {
            TempChunkPath = chunkPath ?? "DataBases/Temp/Chunks"
        });
        return dataBasesPath == null
            ? new FileCleanupJob(
                attachmentRepository.Object,
                attachmentReferenceInspector.Object,
                fileStorage.Object,
                timeProvider,
                calendar,
                chunkedUploadOptions)
            : new FileCleanupJob(
            attachmentRepository.Object,
            attachmentReferenceInspector.Object,
            fileStorage.Object,
            timeProvider,
            calendar,
            chunkedUploadOptions,
            dataBasesPath);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
