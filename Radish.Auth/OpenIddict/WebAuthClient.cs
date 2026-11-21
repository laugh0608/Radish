using OpenIddict.Abstractions;
using Radish.Common.HttpContextTool;

namespace Radish.Auth.OpenIddict;

public static class WebAuthClient
{
    public const string ClientId = "radish-web";
    private const string SettingsSection = "OpenIddict:WebSession:";
    public const string SessionIdClaim = OpenIddictConstants.Claims.Prefixes.Private + "radish_web_session_id";

    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue<bool>(SettingsSection + "Enabled");

    public static OpenIddictApplicationDescriptor? CreateRegistration(IConfiguration configuration)
    {
        if (!IsEnabled(configuration))
        {
            return null;
        }

        var secret = configuration[SettingsSection + "ClientSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("radish-web requires OpenIddict:WebSession:ClientSecret.");
        }

        if (!Uri.TryCreate(configuration["OpenIddict:Server:Issuer"], UriKind.Absolute, out var issuer) ||
            issuer.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(issuer.UserInfo) ||
            !string.IsNullOrEmpty(issuer.Query) || !string.IsNullOrEmpty(issuer.Fragment))
        {
            throw new InvalidOperationException("radish-web requires a configured HTTPS OpenIddict issuer.");
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = ClientId,
            ClientSecret = secret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            DisplayName = "Radish Community Web"
        };

        var publicOrigin = new Uri(issuer.GetLeftPart(UriPartial.Authority));
        descriptor.RedirectUris.Add(new Uri(publicOrigin, "/web/session/callback"));
        descriptor.PostLogoutRedirectUris.Add(publicOrigin);
        descriptor.Permissions.UnionWith([
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.Endpoints.EndSession,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            OpenIddictConstants.Permissions.ResponseTypes.Code,
            OpenIddictConstants.Permissions.Prefixes.Scope + UserScopes.OpenId,
            OpenIddictConstants.Permissions.Prefixes.Scope + UserScopes.Profile,
            OpenIddictConstants.Permissions.Prefixes.Scope + UserScopes.OfflineAccess,
            OpenIddictConstants.Permissions.Prefixes.Scope + UserScopes.RadishApi
        ]);
        descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
        return descriptor;
    }
}
