using System.Security.Claims;

namespace Radish.Common.HttpContextTool;

/// <summary>
/// HTTP 当前用户兼容层，禁止新增业务依赖，请优先使用 <see cref="CurrentUser"/> / <see cref="ICurrentUserAccessor"/>。
/// </summary>
public interface IHttpContextUser
{
    string UserName { get; }
    long UserId { get; }
    long TenantId { get; }
    List<string> Roles { get; }
    bool IsAuthenticated();
    bool IsInRole(string role);

    [Obsolete("禁止新增使用，请改用 CurrentUser / ICurrentUserAccessor")]
    IEnumerable<Claim> GetClaimsIdentity();

    [Obsolete("禁止新增使用，请改用 CurrentUser / ICurrentUserAccessor")]
    List<string> GetClaimValueByType(string claimType);

    [Obsolete("禁止新增使用，请改用 ICurrentUserAccessor；仅兼容历史 Bearer Token 直取场景")]
    string GetToken();

    [Obsolete("禁止新增使用，请改用 CurrentUser / ICurrentUserAccessor")]
    List<string> GetUserInfoFromToken(string claimType);
}
