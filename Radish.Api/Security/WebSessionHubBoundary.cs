using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Radish.Common.HttpContextTool;
using Radish.Shared.Constants;

namespace Radish.Api.Security;

public static class WebSessionHubBoundary
{
    public const string AuthorizationPolicy = "RadishWebHub";

    public static bool IsLegacyHubPath(PathString path) =>
        path.StartsWithSegments("/hub/notification") || path.StartsWithSegments("/hub/chat") ||
        path.StartsWithSegments("/hub/comment");

    public static bool IsWebHubPath(PathString path) =>
        IsHubRequest(path, WebSessionProtocol.HubPrefix + "/notification") ||
        IsHubRequest(path, WebSessionProtocol.HubPrefix + "/chat") ||
        IsHubRequest(path, WebSessionProtocol.HubPrefix + "/comment");

    public static bool HasWebClientIdentity(ClaimsPrincipal principal) =>
        principal.Identity?.IsAuthenticated == true && principal.FindFirst("client_id")?.Value == WebSessionProtocol.ClientId;

    public static HubEndpointConventionBuilder MapWebSessionHub<THub>(this IEndpointRouteBuilder endpoints, string hub)
        where THub : Hub => endpoints.MapHub<THub>($"{WebSessionProtocol.HubPrefix}/{hub}",
            options => options.CloseOnAuthenticationExpiration = true)
        .RequireAuthorization(AuthorizationPolicies.Client, AuthorizationPolicy);

    public static async Task EnforceLegacyBoundaryAsync(HttpContext context, Func<Task> next)
    {
        // The legacy comment endpoint also serves guests. Reject verified Web credentials before it can downgrade to a guest connection.
        if (IsLegacyHubPath(context.Request.Path) && HasWebClientIdentity(context.User))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next();
    }

    private static bool IsHubRequest(PathString path, string endpoint) => path.StartsWithSegments(endpoint, out var rest) &&
        (rest == PathString.Empty || rest == "/" || rest == "/negotiate" || rest == "/negotiate/");
}
