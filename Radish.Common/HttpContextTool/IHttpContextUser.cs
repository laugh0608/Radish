using System.Security.Claims;

namespace Radish.Common.HttpContextTool;

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

    string GetToken();

    [Obsolete("禁止新增使用，请改用 CurrentUser / ICurrentUserAccessor")]
    List<string> GetUserInfoFromToken(string claimType);
}
