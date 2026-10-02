namespace Radish.Model;

/// <summary>评论内容不符合明确长度规则；保留 ArgumentException 消费与响应契约。</summary>
public sealed class CommentContentValidationException(string message) : ArgumentException(message);

/// <summary>评论不存在或服务已返回明确失败结果；服务内部已消费的故障由服务记录，消费者不重复记录。</summary>
public sealed class CommentOperationRejectedException(string message) : InvalidOperationException(message);
