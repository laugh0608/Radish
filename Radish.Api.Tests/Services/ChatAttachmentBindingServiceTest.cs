using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using Radish.Shared.Constants;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class ChatAttachmentBindingServiceTest
{
    [Fact]
    public async Task BindAsync_ShouldBindMatchingAttachmentToPersistedMessage()
    {
        var messageRepository = CreateMessageRepository();
        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var attachment = CreateAttachment();
        attachmentRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Attachment, bool>>?>()))
            .ReturnsAsync(attachment);
        attachmentRepository
            .Setup(repository => repository.UpdateAsync(attachment))
            .ReturnsAsync(true);
        var service = new ChatAttachmentBindingService(messageRepository.Object, attachmentRepository.Object);

        await service.BindAsync(CreatePayload());

        Assert.Equal(90001, attachment.BusinessId);
        Assert.False(attachment.IsPublic);
        Assert.Equal(20001, attachment.ModifyId);
        attachmentRepository.Verify(repository => repository.UpdateAsync(attachment), Times.Once);
    }

    [Fact]
    public async Task BindAsync_ShouldRemainIdempotentWhenAttachmentIsAlreadyBound()
    {
        var messageRepository = CreateMessageRepository();
        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var attachment = CreateAttachment();
        attachment.BusinessId = 90001;
        attachment.IsPublic = false;
        attachmentRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Attachment, bool>>?>()))
            .ReturnsAsync(attachment);
        var service = new ChatAttachmentBindingService(messageRepository.Object, attachmentRepository.Object);

        await service.BindAsync(CreatePayload());

        attachmentRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Attachment>()), Times.Never);
    }

    [Fact]
    public async Task BindAsync_ShouldPermanentlyRejectAttachmentBoundToAnotherMessage()
    {
        var messageRepository = CreateMessageRepository();
        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var attachment = CreateAttachment();
        attachment.BusinessId = 90002;
        attachmentRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Attachment, bool>>?>()))
            .ReturnsAsync(attachment);
        var service = new ChatAttachmentBindingService(messageRepository.Object, attachmentRepository.Object);

        var exception = await Assert.ThrowsAsync<PermanentReliableTaskException>(() =>
            service.BindAsync(CreatePayload()));

        Assert.Contains("已绑定到其他消息", exception.Message, StringComparison.Ordinal);
        attachmentRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Attachment>()), Times.Never);
    }

    private static Mock<IChannelMessageRepository> CreateMessageRepository()
    {
        var repository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        repository
            .Setup(item => item.QueryFirstIncludingDeletedAsync(It.IsAny<Expression<Func<ChannelMessage, bool>>>() ))
            .ReturnsAsync(new ChannelMessage
            {
                Id = 90001,
                ChannelId = 70001,
                UserId = 20001,
                AttachmentId = 80001,
                TenantId = 30000
            });
        return repository;
    }

    private static Attachment CreateAttachment()
    {
        return new Attachment
        {
            Id = 80001,
            UploaderId = 20001,
            UploaderName = "Requester",
            BusinessType = AttachmentBusinessTypes.Chat,
            MimeType = "image/png",
            IsPublic = true,
            IsEnabled = true,
            TenantId = 30000
        };
    }

    private static ChatAttachmentBindingTaskPayload CreatePayload() =>
        new(30000, 90001, 80001, 20001, "Requester");
}
