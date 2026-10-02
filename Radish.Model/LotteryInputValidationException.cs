namespace Radish.Model;

/// <summary>抽奖 HTTP 输入不符合明确 ID 规则；保留 ArgumentException 的参数名、消费与响应契约。</summary>
public sealed class LotteryInputValidationException(string message, string paramName)
    : ArgumentException(message, paramName);
