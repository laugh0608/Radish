using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using Radish.Infrastructure.FileStorage;
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

        var sticker = new Sticker
        {
            Id = 2001,
            GroupId = 1,
            Code = "test_sticker",
            Name = "Test Sticker",
            ImageUrl = "/uploads/Sticker/2026/03/1001.jpg",
            AttachmentId = orphanAttachment.Id,
            IsDeleted = false
        };

        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        attachmentRepository
            .Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>>()))
            .ReturnsAsync(new List<Attachment> { orphanAttachment });

        var stickerRepository = new Mock<IBaseRepository<Sticker>>(MockBehavior.Strict);
        stickerRepository
            .Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Sticker, bool>>>()))
            .ReturnsAsync(new List<Sticker> { sticker });

        var fileStorage = new Mock<IFileStorage>(MockBehavior.Loose);
        var job = new FileCleanupJob(attachmentRepository.Object, stickerRepository.Object, fileStorage.Object);

        var cleanedCount = await job.CleanupOrphanAttachmentsAsync(24);

        Assert.Equal(0, cleanedCount);
        attachmentRepository.Verify(r => r.UpdateAsync(It.IsAny<Attachment>()), Times.Never);
        fileStorage.Verify(s => s.GetFullPath(It.IsAny<string>()), Times.Never);

        attachmentRepository.VerifyAll();
        stickerRepository.VerifyAll();
    }
}
