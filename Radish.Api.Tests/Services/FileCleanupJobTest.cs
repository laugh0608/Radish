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

    [Fact]
    public async Task CleanupOrphanAttachmentsAsync_Should_Skip_Attachments_Referenced_By_Chat_And_AnswerContent()
    {
        var now = DateTime.Now;
        var chatAttachment = new Attachment
        {
            Id = 1002,
            Url = "/uploads/Chat/2026/03/1002.png",
            StoragePath = "Chat/2026/03/1002.png",
            CreateTime = now.AddDays(-2),
            IsDeleted = false,
            BusinessId = null
        };
        var answerAttachment = new Attachment
        {
            Id = 1003,
            Url = "/uploads/Comment/2026/03/1003.jpg",
            ThumbnailPath = "Comment/2026/03/1003_thumb.jpg",
            StoragePath = "Comment/2026/03/1003.jpg",
            CreateTime = now.AddDays(-2),
            IsDeleted = false,
            BusinessId = null
        };

        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        attachmentRepository
            .Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>>()))
            .ReturnsAsync(new List<Attachment> { chatAttachment, answerAttachment });

        var stickerRepository = new Mock<IBaseRepository<Sticker>>(MockBehavior.Strict);
        stickerRepository
            .Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Sticker, bool>>>()))
            .ReturnsAsync(new List<Sticker>());

        var channelMessageRepository = new Mock<IBaseRepository<ChannelMessage>>(MockBehavior.Strict);
        channelMessageRepository
            .Setup(r => r.QueryAsync(It.IsAny<Expression<Func<ChannelMessage, bool>>>()))
            .ReturnsAsync(new List<ChannelMessage>
            {
                new()
                {
                    Id = 2002,
                    ChannelId = 1,
                    UserId = 9527,
                    UserName = "tester",
                    Type = MessageType.Image,
                    AttachmentId = chatAttachment.Id,
                    CreateTime = now
                }
            });

        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        postRepository
            .Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Post, bool>>>()))
            .ReturnsAsync(new List<Post>());

        var commentRepository = new Mock<IBaseRepository<Comment>>(MockBehavior.Strict);
        commentRepository
            .Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Comment, bool>>>()))
            .ReturnsAsync(new List<Comment>());

        var postAnswerRepository = new Mock<IBaseRepository<PostAnswer>>(MockBehavior.Strict);
        postAnswerRepository
            .Setup(r => r.QueryAsync(It.IsAny<Expression<Func<PostAnswer, bool>>>()))
            .ReturnsAsync(new List<PostAnswer>
            {
                new()
                {
                    Id = 3003,
                    PostId = 4004,
                    AuthorId = 9527,
                    AuthorName = "tester",
                    Content = "![answer](/uploads/Comment/2026/03/1003_thumb.jpg#radish:full=%2Fuploads%2FComment%2F2026%2F03%2F1003.jpg)",
                    CreateTime = now
                }
            });

        var fileStorage = new Mock<IFileStorage>(MockBehavior.Loose);
        var job = new FileCleanupJob(
            attachmentRepository.Object,
            stickerRepository.Object,
            fileStorage.Object,
            postRepository.Object,
            commentRepository.Object,
            postAnswerRepository.Object,
            channelMessageRepository.Object);

        var cleanedCount = await job.CleanupOrphanAttachmentsAsync(24);

        Assert.Equal(0, cleanedCount);
        attachmentRepository.Verify(r => r.UpdateAsync(It.IsAny<Attachment>()), Times.Never);
        fileStorage.Verify(s => s.GetFullPath(It.IsAny<string>()), Times.Never);

        attachmentRepository.VerifyAll();
        stickerRepository.VerifyAll();
        channelMessageRepository.VerifyAll();
        postRepository.VerifyAll();
        commentRepository.VerifyAll();
        postAnswerRepository.VerifyAll();
    }
}
