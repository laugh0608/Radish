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
    public string UserName => UserClaimReader.GetUserName(_accessor.HttpContext?.User, GetToken(), "");

    /// <summary>
    /// 用户 Id
    /// </summary>
    public long UserId => UserClaimReader.GetUserId(_accessor.HttpContext?.User, GetToken());

    /// <summary>
    /// 租户 Id
    /// </summary>
    public long TenantId => UserClaimReader.GetTenantId(_accessor.HttpContext?.User, GetToken());

    /// <summary>
    /// 角色列表
    /// </summary>
    public List<string> Roles => UserClaimReader.GetRoles(_accessor.HttpContext?.User, GetToken());

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

    public bool IsInRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        return Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

}
