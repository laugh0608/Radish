namespace Radish.Shared.Constants;

/// <summary>
/// 正式 API 对外错误码。领域服务可在对应命名空间继续扩展更细粒度错误码。
/// </summary>
public static class ApiErrorCodes
{
    public const string ValidationFailed = "Common.ValidationFailed";
    public const string NotFound = "Common.NotFound";
    public const string Conflict = "Common.Conflict";
    public const string Unauthorized = "Auth.Unauthorized";
    public const string Forbidden = "Auth.Forbidden";
    public const string RateLimitExceeded = "RateLimit.Exceeded";
    public const string IpBlocked = "RateLimit.IpBlocked";
    public const string UnexpectedError = "System.UnexpectedError";
    public const string DependencyBadGateway = "Dependency.BadGateway";
    public const string DependencyUnavailable = "Dependency.Unavailable";
}
