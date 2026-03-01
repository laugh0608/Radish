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

    public BusinessException(string message, int statusCode = 400, string? errorCode = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    public BusinessException(string message, Exception innerException, int statusCode = 400, string? errorCode = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}
