using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
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
    public async Task CleanupOrphanAttachmentsAsync_Should_Skip_Attachments_Referenced_By_Sticker()
    {
        var now = DateTime.Now;
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
        var job = new FileCleanupJob(attachmentRepository.Object, attachmentReferenceInspector.Object, fileStorage.Object);

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
        var now = DateTime.Now;
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
        var job = new FileCleanupJob(attachmentRepository.Object, attachmentReferenceInspector.Object, fileStorage.Object);

        var cleanedCount = await job.CleanupOrphanAttachmentsAsync(24);

        Assert.Equal(0, cleanedCount);
        attachmentRepository.Verify(r => r.UpdateAsync(It.IsAny<Attachment>()), Times.Never);
        fileStorage.Verify(s => s.GetFullPath(It.IsAny<string>()), Times.Never);

        attachmentRepository.VerifyAll();
        attachmentReferenceInspector.VerifyAll();
    }
}
