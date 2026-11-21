using Microsoft.Net.Http.Headers;
using Radish.Common.HttpContextTool;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace Radish.Gateway;

public static class WebSessionProxy
{
    private const string RouteId = "auth-web-session";

    public static IReverseProxyBuilder AddWebSessionRouting(this IReverseProxyBuilder proxy)
    {
        return proxy.LoadFromMemory([
        new RouteConfig
        {
            RouteId = RouteId,
            ClusterId = "authCluster",
            Match = new RouteMatch { Path = "/web/session/{**catch-all}" },
            Transforms =
            [
                new Dictionary<string, string> { ["X-Forwarded"] = "Set" }
            ]
        },
        new RouteConfig
        {
            RouteId = "web-session-hubs",
            ClusterId = "apiCluster",
            Match = new RouteMatch { Path = WebSessionProtocol.HubPrefix + "/{**catch-all}" },
            Transforms = [new Dictionary<string, string> { ["RequestHeaderOriginalHost"] = "true" }]
        }], []).AddTransforms(builder =>
        {
            if (builder.Route.RouteId == RouteId) return;

            var authCluster = builder.Route.ClusterId == "authCluster";
            builder.AddRequestTransform(context =>
            {
                var headers = context.ProxyRequest.Headers;
                if (headers.TryGetValues(HeaderNames.Cookie, out var values) &&
                    values.Any(value => value.Contains(WebSessionCookieNames.Login, StringComparison.OrdinalIgnoreCase) ||
                                        value.Contains(WebSessionCookieNames.Session, StringComparison.OrdinalIgnoreCase)))
                {
                    var retained = FilterCookies(values, authCluster, context.HttpContext.Request.Path);
                    headers.Remove(HeaderNames.Cookie);
                    if (retained.Length != 0) headers.TryAddWithoutValidation(HeaderNames.Cookie, retained);
                }

                return ValueTask.CompletedTask;
            });
        });
    }

    public static string FilterCookies(IEnumerable<string> headers, bool authCluster, PathString path)
    {
        var allowCorrelation = authCluster && (path.Equals("/Account/Login", StringComparison.OrdinalIgnoreCase) ||
                                               path.Equals("/connect/authorize", StringComparison.OrdinalIgnoreCase));
        var selected = headers.SelectMany(header => header.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        return string.Join("; ", selected.Where(value =>
        {
            var split = value.IndexOf('=');
            var name = split < 0 ? value : value[..split].TrimEnd();
            return !name.Equals(WebSessionCookieNames.Session, StringComparison.OrdinalIgnoreCase) &&
                   (allowCorrelation || !name.Equals(WebSessionCookieNames.Login, StringComparison.OrdinalIgnoreCase));
        }));
    }
}
