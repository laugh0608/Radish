using System.Globalization;
using System.Security.Claims;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Radish.Common.HttpContextTool;
using Radish.IService;

namespace Radish.Auth.OpenIddict;

public sealed class RequireWebAuthSessionAuthorizationHandler(IConfiguration configuration)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        // Require an Auth-created session before OpenIddict creates an authorization for this login.
        if (context.EndpointType == OpenIddictServerEndpointType.Authorization &&
            context.Request?.ClientId == WebAuthClient.ClientId &&
            (!WebAuthClient.IsEnabled(configuration) || context.Principal is null ||
             !Guid.TryParse(context.Principal.GetClaim(WebAuthClient.SessionIdClaim), out _)))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest, "web_session_login_required");
        }

        return default;
    }
}

public sealed class WebAuthSessionTokenHandler(
    IConfiguration configuration, IWebAuthSessionService sessions, IUserService users) :
    IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenRequestContext>,
    IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenRequestContext context)
    {
        if (context.ClientId != WebAuthClient.ClientId)
        {
            return;
        }

        var isRefresh = context.Request.IsRefreshTokenGrantType();
        var principal = isRefresh ? context.RefreshTokenPrincipal : context.AuthorizationCodePrincipal;
        var grant = await EvaluateGrantAsync(context.Request, principal, context.CancellationToken);
        if (grant.Status != WebAuthSessionGrantStatus.Allowed)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidGrant, GetErrorDescription(grant.Status));
        }
    }

    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (context.Request is not { ClientId: WebAuthClient.ClientId } request)
        {
            return;
        }

        if (context.EndpointType == OpenIddictServerEndpointType.Authorization)
        {
            // A regular Auth cookie or browser-supplied parameter cannot establish a Web session.
            if (!WebAuthClient.IsEnabled(configuration) || !TryReadBinding(context.Principal, out var binding) ||
                !await sessions.BindAuthorizationAsync(binding, context.CancellationToken))
            {
                context.Reject(OpenIddictConstants.Errors.InvalidRequest, "web_session_login_required");
            }

            return;
        }

        if (context.EndpointType != OpenIddictServerEndpointType.Token)
        {
            return;
        }

        // Read the account before the final session check so elapsed time and concurrent revocation count at issuance.
        if (!WebAuthClient.IsEnabled(configuration) || !TryReadBinding(context.Principal, out var tokenBinding) ||
            !await IsAccountEnabledAsync(tokenBinding))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidGrant, "web_session_invalid");
            return;
        }

        var grant = await EvaluateGrantAsync(request, context.Principal, context.CancellationToken);
        if (grant.Status != WebAuthSessionGrantStatus.Allowed)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidGrant, GetErrorDescription(grant.Status));
            return;
        }

        context.AccessTokenPrincipal?.SetExpirationDate(grant.AccessTokenExpiresAt);
        context.RefreshTokenPrincipal?.SetExpirationDate(grant.RefreshTokenExpiresAt);
    }

    private Task<WebAuthSessionGrant> EvaluateGrantAsync(
        OpenIddictRequest request, ClaimsPrincipal? principal, CancellationToken cancellationToken)
    {
        var isRefresh = request.IsRefreshTokenGrantType();
        if (!WebAuthClient.IsEnabled(configuration) || (!isRefresh && !request.IsAuthorizationCodeGrantType()) ||
            !TryReadBinding(principal, out var binding))
        {
            return Task.FromResult(new WebAuthSessionGrant(WebAuthSessionGrantStatus.Invalid));
        }

        Guid? requiredLease = null;
        if (isRefresh)
        {
            if (!Guid.TryParseExact((string?)request.GetParameter(WebAuthClient.RefreshLeaseParameter), "N", out var lease))
            {
                return Task.FromResult(new WebAuthSessionGrant(WebAuthSessionGrantStatus.Invalid));
            }

            requiredLease = lease;
        }

        return sessions.EvaluateGrantAsync(binding, isRefresh, cancellationToken, requiredLease);
    }

    private Task<bool> IsAccountEnabledAsync(WebAuthSessionBinding binding)
    {
        if (!long.TryParse(binding.Subject, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) ||
            !long.TryParse(binding.TenantId, NumberStyles.None, CultureInfo.InvariantCulture, out var tenantId))
        {
            return Task.FromResult(false);
        }

        return users.QueryExistsAsync(user => user.Id == userId && user.TenantId == tenantId && user.IsEnable && !user.IsDeleted);
    }

    private static bool TryReadBinding(ClaimsPrincipal? principal, out WebAuthSessionBinding binding)
    {
        binding = default;
        if (principal is null || !Guid.TryParse(principal.GetClaim(WebAuthClient.SessionIdClaim), out var sessionId))
        {
            return false;
        }

        var subject = principal.GetClaim(OpenIddictConstants.Claims.Subject);
        var tenantId = principal.GetClaim(UserClaimTypes.TenantId);
        var authorizationId = principal.GetAuthorizationId();
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(authorizationId))
        {
            return false;
        }

        binding = new(sessionId, subject, tenantId, authorizationId);
        return true;
    }

    private static string GetErrorDescription(WebAuthSessionGrantStatus status) => status switch
    {
        WebAuthSessionGrantStatus.IdleExpired => IdleSessionPolicy.ExpiredErrorDescription,
        WebAuthSessionGrantStatus.AbsoluteExpired => "session_absolute_expired",
        _ => "web_session_invalid"
    };
}
