using Microsoft.AspNetCore.Authentication.Cookies;

namespace Radish.Api.Security;

public static class HangfireDashboardAuthentication
{
    public const string Scheme = "HangfireDashboard";

    public static void Configure(CookieAuthenticationOptions options, string dashboardPath)
    {
        options.Cookie.Name = "__Secure-radish.hangfire";
        options.Cookie.Path = dashboardPath;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }
}
