using Radish.Common.HttpContextTool;

namespace Radish.Auth.WebSessions;

public static class WebSessionLoginCookie
{
    public static string? Read(IRequestCookieCollection cookies, string? state, out string name)
    {
        name = WebSessionCookieNames.Login;
        if (!WebSessionSecrets.IsSecret(state)) return null;

        var scoped = WebSessionCookieNames.LoginPrefix + WebSessionSecrets.Digest(state);
        if (cookies.TryGetValue(scoped, out var secret))
        {
            name = scoped;
            return secret;
        }

        // A login started before an upgrade still has its original ten-minute binding.
        return cookies[name];
    }
}
