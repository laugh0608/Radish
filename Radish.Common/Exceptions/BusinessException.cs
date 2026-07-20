namespace Radish.Common.Exceptions;

/// <summary>
/// 业务异常（可携带 HTTP 状态码与业务错误码）
/// </summary>
public class BusinessException : Exception
{
    /// <summary>
    /// HTTP 状态码
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// 业务错误码
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// 多语言消息键
    /// </summary>
    public string? MessageKey { get; }

    /// <summary>
    /// 可安全公开的多语言格式参数。
    /// </summary>
    public IReadOnlyList<object> MessageArguments { get; }

    public BusinessException(
        string message,
        int statusCode = 400,
        string? errorCode = null,
        string? messageKey = null,
        params object[] messageArguments)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        MessageKey = messageKey;
        MessageArguments = messageArguments ?? Array.Empty<object>();
    }

    public BusinessException(
        string message,
        Exception innerException,
        int statusCode = 400,
        string? errorCode = null,
        string? messageKey = null,
        params object[] messageArguments)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        MessageKey = messageKey;
        MessageArguments = messageArguments ?? Array.Empty<object>();
    }
}
