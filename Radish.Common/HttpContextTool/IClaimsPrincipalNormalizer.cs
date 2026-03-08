using System.Security.Claims;

namespace Radish.Common.HttpContextTool;

public interface IClaimsPrincipalNormalizer
{
    CurrentUser Normalize(ClaimsPrincipal? principal, string? token = null);
}
