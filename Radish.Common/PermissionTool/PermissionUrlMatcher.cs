using System.Text.RegularExpressions;

namespace Radish.Common.PermissionTool;

public static class PermissionUrlMatcher
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    public static bool IsMatch(string? requestPath, string? permissionUrl)
    {
        if (string.IsNullOrWhiteSpace(requestPath) || string.IsNullOrWhiteSpace(permissionUrl))
        {
            return false;
        }

        var normalizedPermissionUrl = permissionUrl.Trim();
        if (!normalizedPermissionUrl.StartsWith('/'))
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(
                requestPath.Trim(),
                $"^{normalizedPermissionUrl}$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                MatchTimeout);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
