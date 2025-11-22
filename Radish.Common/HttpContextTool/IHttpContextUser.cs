using System.Security.Claims;

namespace Radish.Common.HttpContextTool;

public interface IHttpContextUser
{
    string UserName { get; }
    long UserId { get; }
    long TenantId { get; }
    bool IsAuthenticated();
    IEnumerable<Claim> GetClaimsIdentity();
    List<string> GetClaimValueByType(string claimType);
    string GetToken();
    List<string> GetUserInfoFromToken(string claimType);
}