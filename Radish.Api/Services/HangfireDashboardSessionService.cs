using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Radish.Api.Security;
using Radish.Common.HttpContextTool;
using Radish.Model.ViewModels;

namespace Radish.Api.Services;

public sealed class HangfireDashboardSessionService(
    IHttpContextAccessor contextAccessor,
    IOptionsMonitor<CookieAuthenticationOptions> cookieOptions,
    TimeProvider timeProvider) : IHangfireDashboardSessionService
{
    public async Task<HangfireSessionVo?> CreateAsync()
    {
        var context = contextAccessor.HttpContext;
        var tokenExpires = UserClaimReader.GetTokenExpiresAtUtc(context?.User);
        if (context?.User.Identity?.IsAuthenticated != true ||
            tokenExpires == null || tokenExpires <= timeProvider.GetUtcNow())
        {
            return null;
        }

        var options = cookieOptions.Get(HangfireDashboardAuthentication.Scheme);
        var expires = timeProvider.GetUtcNow().Add(options.ExpireTimeSpan);
        if (tokenExpires < expires)
        {
            expires = tokenExpires.Value;
        }

        await context.SignInAsync(HangfireDashboardAuthentication.Scheme, context.User,
            new AuthenticationProperties
            {
                ExpiresUtc = expires,
                IsPersistent = false,
                AllowRefresh = false
            });
        context.Response.Headers.CacheControl = "no-store";
        return new HangfireSessionVo
        {
            VoDashboardPath = options.Cookie.Path!,
            VoExpiresAtUtc = expires.UtcDateTime
        };
    }
}
