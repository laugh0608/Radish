using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Radish.Auth.OpenIddict;
using Radish.Common.HttpContextTool;

namespace Radish.Auth.WebSessions;

public sealed class WebTokenBackchannel(
    HttpClient client, IConfiguration configuration, IOptionsMonitor<OpenIddictServerOptions> server, TimeProvider clock)
    : IWebTokenBackchannel
{
    public Task<WebTokenBackchannelResult> ExchangeAsync(string code, string verifier, string nonce, WebAuthSession session,
        CancellationToken cancellationToken)
    {
        return SendAsync(new Dictionary<string, string>
        {
            ["grant_type"] = OpenIddictConstants.GrantTypes.AuthorizationCode,
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = WebAuthClient.GetCallback(WebAuthClient.GetIssuer(configuration)).AbsoluteUri
        }, nonce, session, cancellationToken);
    }

    public Task<WebTokenBackchannelResult> RefreshAsync(string refreshToken, Guid lease, WebAuthSession session,
        CancellationToken cancellationToken)
    {
        return SendAsync(new Dictionary<string, string>
        {
            ["grant_type"] = OpenIddictConstants.GrantTypes.RefreshToken,
            ["refresh_token"] = refreshToken,
            [WebAuthClient.RefreshLeaseParameter] = lease.ToString("N")
        }, null, session, cancellationToken);
    }

    private async Task<WebTokenBackchannelResult> SendAsync(Dictionary<string, string> parameters, string? nonce,
        WebAuthSession session, CancellationToken cancellationToken)
    {
        var issuer = WebAuthClient.GetIssuer(configuration);
        parameters["client_id"] = WebAuthClient.ClientId;
        parameters["client_secret"] = WebAuthClient.GetClientSecret(configuration);

        try
        {
            using var form = new FormUrlEncodedContent(parameters);
            using var response = await client.PostAsync(new Uri(WebAuthClient.GetOrigin(issuer), "/connect/token"), form, cancellationToken);
            if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.BadRequest))
            {
                return new(WebTokenBackchannelStatus.Unavailable);
            }

            using var payload = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken));
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return new(ReadString(payload.RootElement, "error") == OpenIddictConstants.Errors.InvalidGrant
                    ? WebTokenBackchannelStatus.Rejected : WebTokenBackchannelStatus.Unavailable);
            }

            var access = ReadString(payload.RootElement, "access_token");
            var refresh = ReadString(payload.RootElement, "refresh_token");
            if (string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(refresh) ||
                !string.Equals(ReadString(payload.RootElement, "token_type"), "Bearer", StringComparison.OrdinalIgnoreCase))
            {
                return new(WebTokenBackchannelStatus.Unavailable);
            }

            if (nonce is not null)
            {
                var identity = ReadString(payload.RootElement, "id_token");
                if (string.IsNullOrWhiteSpace(identity) || !ValidateIdentity(identity, issuer, nonce, session.Subject))
                {
                    return new(WebTokenBackchannelStatus.Rejected);
                }
            }

            return ValidateAccessToken(access, issuer, session) is { } expiry
                ? new(WebTokenBackchannelStatus.Success, new(access, refresh, expiry))
                : new(WebTokenBackchannelStatus.Unavailable);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(WebTokenBackchannelStatus.Unavailable);
        }
        catch (Exception error) when (error is HttpRequestException or IOException or JsonException or SecurityTokenException or ArgumentException)
        {
            return new(WebTokenBackchannelStatus.Unavailable);
        }
    }

    private bool ValidateIdentity(string token, Uri issuer, string nonce, string subject)
    {
        var principal = ValidateToken(token, issuer, WebAuthClient.ClientId, OpenIddictConstants.JsonWebTokenTypes.GenericJsonWebToken,
            out var parsedToken);
        var authorizedParty = principal.FindFirst("azp")?.Value;
        return parsedToken.Audiences.SequenceEqual([WebAuthClient.ClientId]) &&
               (authorizedParty is null || authorizedParty == WebAuthClient.ClientId) &&
               principal.FindFirst("sub")?.Value == subject && principal.FindFirst("nonce")?.Value == nonce;
    }

    private DateTimeOffset? ValidateAccessToken(string token, Uri issuer, WebAuthSession session)
    {
        var principal = ValidateToken(token, issuer, UserScopes.RadishApi, OpenIddictConstants.JsonWebTokenTypes.AccessToken,
            out var parsedToken);
        return principal.FindFirst("sub")?.Value == session.Subject &&
               principal.FindFirst(UserClaimTypes.TenantId)?.Value == session.TenantId &&
               principal.FindFirst("client_id")?.Value == WebAuthClient.ClientId
            ? new DateTimeOffset(parsedToken.ValidTo, TimeSpan.Zero)
            : null;
    }

    private ClaimsPrincipal ValidateToken(string token, Uri issuer, string audience, string type, out JwtSecurityToken parsed)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var signing = server.CurrentValue.SigningCredentials;
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidIssuer = issuer.AbsoluteUri,
            ValidAudience = audience,
            ValidTypes = [type],
            IgnoreTrailingSlashWhenValidatingAudience = false,
            IssuerSigningKeys = signing.Select(item => item.Key),
            ValidAlgorithms = signing.Select(item => item.Algorithm),
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
            LifetimeValidator = (start, end, _, _) => end > now && (start is null || start <= now)
        }, out var validatedToken);
        parsed = (JwtSecurityToken)validatedToken;
        return principal;
    }

    private static string? ReadString(JsonElement payload, string key) =>
        payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
}
