namespace Radish.Model;

/// <summary>轻回应通知阶段的参数异常；由最终消费点构造响应并记录，保留父类消费契约。</summary>
public sealed class PostQuickReplyNotificationEnqueueException(ArgumentException cause)
    : ArgumentException("Quick reply notification enqueue failed", cause)
{
    // 包装时不读取原消息，避免在事务回滚前触发响应文案求值。
    public override string Message => InnerException!.Message;
}
