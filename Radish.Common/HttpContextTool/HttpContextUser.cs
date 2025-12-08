using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Radish.Common.HttpContextTool;

/// <summary>
/// 从 HTTP 上下文中获取请求的用户信息
/// </summary>
public class HttpContextUser : IHttpContextUser
{
    private readonly IHttpContextAccessor _accessor;
    private readonly ILogger<HttpContextUser> _logger;

    public HttpContextUser(IHttpContextAccessor accessor, ILogger<HttpContextUser> logger)
    {
        _accessor = accessor;
        _logger = logger;
    }

    /// <summary>
    /// 用户名
    /// </summary>
    public string UserName => GetName();

    /// <summary>
    /// 获取用户名
    /// </summary>
    /// <returns></returns>
    private string GetName()
    {
        if (!IsAuthenticated() || _accessor.HttpContext == null)
        {
            return "";
        }

        // 优先从 OIDC 的 name Claim 获取
        var nameFromOidc = GetClaimValueByType("name").FirstOrDefault();
        if (!nameFromOidc.IsNullOrEmpty())
        {
            return nameFromOidc;
        }

        // 兼容 ClaimTypes.Name
        var nameFromClaimTypes = GetClaimValueByType(ClaimTypes.Name).FirstOrDefault();
        if (!nameFromClaimTypes.IsNullOrEmpty())
        {
            return nameFromClaimTypes;
        }

        // 最后再使用 Identity.Name
        if (_accessor.HttpContext.User.Identity?.Name.IsNotEmptyOrNull() == true)
        {
            return _accessor.HttpContext.User.Identity.Name;
        }

        return "";
    }

    /// <summary>
    /// 用户 Id
    /// </summary>
    public long UserId => GetUserIdFromClaims();

    /// <summary>
    /// 租户 Id
    /// </summary>
    public long TenantId => GetTenantIdFromClaims();

    private long GetUserIdFromClaims()
    {
        // 优先使用 OIDC 标准的 sub
        var sub = GetClaimValueByType("sub").FirstOrDefault();
        if (!sub.IsNullOrEmpty() && sub.ObjToLong() > 0)
        {
            return sub.ObjToLong();
        }

        // 兼容映射后的 ClaimTypes.NameIdentifier
        var nameId = GetClaimValueByType(ClaimTypes.NameIdentifier).FirstOrDefault();
        if (!nameId.IsNullOrEmpty() && nameId.ObjToLong() > 0)
        {
            return nameId.ObjToLong();
        }

        // 兼容旧版 jti
        var jti = GetClaimValueByType("jti").FirstOrDefault();
        if (!jti.IsNullOrEmpty() && jti.ObjToLong() > 0)
        {
            return jti.ObjToLong();
        }

        // 再退一步，直接从原始 Token 中解析 sub/jti，避免中间件 Claim 映射影响
        var subFromToken = GetUserInfoFromToken("sub").FirstOrDefault();
        if (!subFromToken.IsNullOrEmpty() && subFromToken.ObjToLong() > 0)
        {
            return subFromToken.ObjToLong();
        }

        var jtiFromToken = GetUserInfoFromToken("jti").FirstOrDefault();
        if (!jtiFromToken.IsNullOrEmpty() && jtiFromToken.ObjToLong() > 0)
        {
            return jtiFromToken.ObjToLong();
        }

        return 0;
    }

    private long GetTenantIdFromClaims()
    {
        // 优先使用 tenant_id，其次兼容旧版 TenantId
        var tenantId = GetClaimValueByType("tenant_id").FirstOrDefault();
        if (!tenantId.IsNullOrEmpty() && tenantId.ObjToLong() > 0)
        {
            return tenantId.ObjToLong();
        }

        var legacyTenantId = GetClaimValueByType("TenantId").FirstOrDefault();
        if (!legacyTenantId.IsNullOrEmpty())
        {
            return legacyTenantId.ObjToLong();
        }

        return 0;
    }

    /// <summary>
    /// 是否已获得认证
    /// </summary>
    /// <returns></returns>
    public bool IsAuthenticated()
    {
        return _accessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    }

    /// <summary>
    /// 获取 Token
    /// </summary>
    /// <returns></returns>
    public string GetToken()
    {
        var token = _accessor.HttpContext?.Request?.Headers["Authorization"].ObjToString().Replace("Bearer ", "");
        if (!token.IsNullOrEmpty())
        {
            return token;
        }

        return token;
    }

    /// <summary>
    /// 从 Token 中获取用户信息
    /// </summary>
    /// <param name="claimType"></param>
    /// <returns></returns>
    public List<string> GetUserInfoFromToken(string claimType)
    {
        var jwtHandler = new JwtSecurityTokenHandler();
        var token = "";

        token = GetToken();
        // token 校验
        if (token.IsNotEmptyOrNull() && jwtHandler.CanReadToken(token))
        {
            JwtSecurityToken jwtToken = jwtHandler.ReadJwtToken(token);

            return (from item in jwtToken.Claims
                where item.Type == claimType
                select item.Value).ToList();
        }

        return new List<string>() { };
    }

    public IEnumerable<Claim> GetClaimsIdentity()
    {
        if (_accessor.HttpContext == null) return ArraySegment<Claim>.Empty;

        if (!IsAuthenticated()) return GetClaimsIdentity(GetToken());

        var claims = _accessor.HttpContext.User.Claims.ToList();
        var headers = _accessor.HttpContext.Request.Headers;
        foreach (var header in headers)
        {
            claims.Add(new Claim(header.Key, header.Value));
        }

        return claims;
    }

    public IEnumerable<Claim> GetClaimsIdentity(string token)
    {
        var jwtHandler = new JwtSecurityTokenHandler();
        // token 校验
        if (token.IsNotEmptyOrNull() && jwtHandler.CanReadToken(token))
        {
            var jwtToken = jwtHandler.ReadJwtToken(token);

            return jwtToken.Claims;
        }

        return new List<Claim>();
    }

    public List<string> GetClaimValueByType(string claimType)
    {
        return (from item in GetClaimsIdentity()
            where item.Type == claimType
            select item.Value).ToList();
    }
}