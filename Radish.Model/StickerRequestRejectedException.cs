namespace Radish.Model;

/// <summary>表情管理的标识冲突、目标缺失或快照失效；不用于依赖故障或写入不完整。</summary>
public sealed class StickerRequestRejectedException(string message) : InvalidOperationException(message);
