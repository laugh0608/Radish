namespace Radish.Model;

/// <summary>表情管理明确的参数拒绝；保留原 ArgumentException 消息、参数名和消费契约。</summary>
public sealed class StickerInputValidationException(string message, string paramName)
    : ArgumentException(message, paramName);
