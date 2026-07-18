using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Shared.Constants;

namespace Radish.Service;

/// <summary>Chat 消息附件可靠绑定服务</summary>
public sealed class ChatAttachmentBindingService : IChatAttachmentBindingService
{
    private readonly IChannelMessageRepository _messageRepository;
    private readonly IBaseRepository<Attachment> _attachmentRepository;

    public ChatAttachmentBindingService(
        IChannelMessageRepository messageRepository,
        IBaseRepository<Attachment> attachmentRepository)
    {
        _messageRepository = messageRepository;
        _attachmentRepository = attachmentRepository;
    }

    public async Task BindAsync(ChatAttachmentBindingTaskPayload payload)
    {
        if (payload.TenantId < 0 || payload.MessageId <= 0 || payload.AttachmentId <= 0 || payload.UploaderId <= 0)
        {
            throw new PermanentReliableTaskException("Chat 附件绑定任务参数无效。");
        }

        var message = await _messageRepository.QueryFirstIncludingDeletedAsync(candidate =>
            candidate.Id == payload.MessageId &&
            candidate.TenantId == payload.TenantId &&
            candidate.UserId == payload.UploaderId &&
            candidate.AttachmentId == payload.AttachmentId);
        if (message == null)
        {
            throw new PermanentReliableTaskException("Chat 附件绑定任务没有匹配的消息事实。");
        }

        var attachment = await _attachmentRepository.QueryFirstAsync(candidate =>
            candidate.Id == payload.AttachmentId && candidate.IsEnabled && !candidate.IsDeleted);
        if (attachment == null ||
            attachment.TenantId != payload.TenantId ||
            attachment.UploaderId != payload.UploaderId ||
            attachment.BusinessType != AttachmentBusinessTypes.Chat)
        {
            throw new PermanentReliableTaskException("Chat 附件绑定任务没有匹配的附件事实。");
        }

        if (attachment.BusinessId.HasValue && attachment.BusinessId.Value != payload.MessageId)
        {
            throw new PermanentReliableTaskException("Chat 附件已绑定到其他消息。");
        }

        if (attachment.BusinessId == payload.MessageId && !attachment.IsPublic)
        {
            return;
        }

        attachment.BusinessId = payload.MessageId;
        attachment.IsPublic = false;
        attachment.ModifyTime = DateTime.UtcNow;
        attachment.ModifyBy = string.IsNullOrWhiteSpace(payload.UploaderName)
            ? "System"
            : payload.UploaderName.Trim();
        attachment.ModifyId = payload.UploaderId;
        if (!await _attachmentRepository.UpdateAsync(attachment))
        {
            throw new InvalidOperationException("Chat 附件绑定未写入任何记录。");
        }
    }
}
