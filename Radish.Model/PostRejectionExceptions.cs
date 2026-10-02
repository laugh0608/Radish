namespace Radish.Model;

/// <summary>帖子输入不符合明确内容规则；保留 ArgumentException 消费与响应契约。</summary>
public sealed class PostContentValidationException(string message, string? paramName = null)
    : ArgumentException(message, paramName);

/// <summary>帖子缺失、编辑上限或分类不可用等明确拒绝；保留 InvalidOperationException 消费与响应契约。</summary>
public sealed class PostOperationRejectedException(string message) : InvalidOperationException(message);
