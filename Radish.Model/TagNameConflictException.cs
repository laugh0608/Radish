namespace Radish.Model;

/// <summary>标签名称已被未删除标签占用；保留 InvalidOperationException 消费与响应契约。</summary>
public sealed class TagNameConflictException(string message) : InvalidOperationException(message);
