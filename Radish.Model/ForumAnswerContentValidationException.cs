namespace Radish.Model;

/// <summary>回答内容不符合明确规则；保留 ArgumentException 的参数名、消费与响应契约。</summary>
public sealed class ForumAnswerContentValidationException(string message, string paramName)
    : ArgumentException(message, paramName);
