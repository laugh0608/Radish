using Microsoft.AspNetCore.Http;

namespace Radish.Common.HttpContextTool;

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClaimsPrincipalNormalizer _normalizer;
    private CurrentUser? _current;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor, IClaimsPrincipalNormalizer normalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _normalizer = normalizer;
    }

    public CurrentUser Current => _current ??= _normalizer.Normalize(_httpContextAccessor.HttpContext?.User, GetToken());

    private string? GetToken()
    {
        var authorization = _httpContextAccessor.HttpContext?.Request?.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return null;
        }

        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization[7..]
            : authorization;
    }
}
