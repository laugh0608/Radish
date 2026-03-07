using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Radish.Common.HttpContextTool;

public class HttpContextUser : IHttpContextUser
{
    private readonly IHttpContextAccessor _accessor;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public HttpContextUser(IHttpContextAccessor accessor, ILogger<HttpContextUser> logger)
        : this(accessor, logger, new CurrentUserAccessor(accessor, new ClaimsPrincipalNormalizer()))
    {
    }

    public HttpContextUser(IHttpContextAccessor accessor, ILogger<HttpContextUser> logger, ICurrentUserAccessor currentUserAccessor)
    {
        _accessor = accessor;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    public string UserName => Current.UserName;

    public long UserId => Current.UserId;

    public long TenantId => Current.TenantId;

    public List<string> Roles => Current.Roles.ToList();

    public bool IsAuthenticated()
    {
        return _accessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    }

    public string GetToken()
    {
        var authorization = _accessor.HttpContext?.Request?.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return string.Empty;
        }

        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization[7..]
            : authorization;
    }

    [Obsolete("禁止新增使用，请改用 CurrentUser / ICurrentUserAccessor")]
    public List<string> GetUserInfoFromToken(string claimType)
    {
        var jwtHandler = new JwtSecurityTokenHandler();
        var token = GetToken();
        if (token.IsNotEmptyOrNull() && jwtHandler.CanReadToken(token))
        {
            var jwtToken = jwtHandler.ReadJwtToken(token);
            return (from item in jwtToken.Claims
                where item.Type == claimType
                select item.Value).ToList();
        }

        return new List<string>();
    }

    [Obsolete("禁止新增使用，请改用 CurrentUser / ICurrentUserAccessor")]
    public IEnumerable<Claim> GetClaimsIdentity()
    {
        if (_accessor.HttpContext == null)
        {
            return ArraySegment<Claim>.Empty;
        }

        if (!IsAuthenticated())
        {
            return GetClaimsFromToken(GetToken());
        }

        var claims = _accessor.HttpContext.User.Claims.ToList();
        var headers = _accessor.HttpContext.Request.Headers;
        foreach (var header in headers)
        {
            claims.Add(new Claim(header.Key, header.Value));
        }

        return claims;
    }

    private IEnumerable<Claim> GetClaimsFromToken(string token)
    {
        var jwtHandler = new JwtSecurityTokenHandler();
        if (token.IsNotEmptyOrNull() && jwtHandler.CanReadToken(token))
        {
            var jwtToken = jwtHandler.ReadJwtToken(token);
            return jwtToken.Claims;
        }

        return new List<Claim>();
    }

    [Obsolete("禁止新增使用，请改用 CurrentUser / ICurrentUserAccessor")]
    public List<string> GetClaimValueByType(string claimType)
    {
        return (from item in GetClaimsIdentity()
            where item.Type == claimType
            select item.Value).ToList();
    }

    public bool IsInRole(string role)
    {
        return Current.IsInRole(role);
    }
}
