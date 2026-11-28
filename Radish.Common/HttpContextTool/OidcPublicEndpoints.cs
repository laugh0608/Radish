using Microsoft.Extensions.Configuration;

namespace Radish.Common.HttpContextTool;

/// <summary>Auth 登记和 Scalar 页面共用的公开 Gateway 地址。</summary>
public sealed class OidcPublicEndpoints
{
    public OidcPublicEndpoints(IConfiguration configuration)
    {
        var issuer = configuration["OpenIddict:Server:Issuer"];
        if (string.IsNullOrWhiteSpace(issuer)) issuer = "https://localhost:5000";
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            uri.Host.Length == 0 || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidOperationException("OpenIddict:Server:Issuer 必须为公开 Gateway 的 HTTP(S) 基址。");

        BaseUri = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }

    public Uri BaseUri { get; }
    public Uri Authorization => new(BaseUri, "connect/authorize");
    public Uri Token => new(BaseUri, "connect/token");
    public Uri ScalarCallback => new(BaseUri, "scalar/oauth2-callback");
}
