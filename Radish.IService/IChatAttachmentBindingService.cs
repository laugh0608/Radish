using Radish.Model;

namespace Radish.IService;

/// <summary>Chat 消息附件可靠绑定服务</summary>
public interface IChatAttachmentBindingService
{
    /// <summary>按消息事实幂等绑定 Chat 附件；同一任务可安全重放。</summary>
    Task BindAsync(ChatAttachmentBindingTaskPayload payload);
}
