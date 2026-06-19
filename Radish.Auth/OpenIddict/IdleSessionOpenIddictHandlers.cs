using System.Globalization;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Radish.Auth.Models;

namespace Radish.Auth.OpenIddict;

public sealed class ValidateIdleSessionTokenRequestHandler
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenRequestContext>
{
    private readonly IOptionsMonitor<IdleSessionOptions> _options;
    private readonly ILogger<ValidateIdleSessionTokenRequestHandler> _logger;

    public ValidateIdleSessionTokenRequestHandler(
        IOptionsMonitor<IdleSessionOptions> options,
        ILogger<ValidateIdleSessionTokenRequestHandler> logger)
    {
        _options = options;
        _logger = logger;
    }

    public ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenRequestContext context)
    {
        if (context.Request is null || !context.Request.IsRefreshTokenGrantType())
        {
            return default;
        }

        var options = _options.CurrentValue;
        if (!options.Enable)
        {
            return default;
        }

        var now = DateTimeOffset.UtcNow;
        var storedLastActiveAt = IdleSessionPolicy.ParseUnixTimeSeconds(
            context.RefreshTokenPrincipal?.GetClaim(IdleSessionPolicy.LastActiveClaim));
        var clientLastActiveAt = IdleSessionPolicy.NormalizeClientActivityUnixTimeSeconds(
            (string?)context.Request.GetParameter(IdleSessionPolicy.LastActiveParameter),
            now,
            options);

        var evaluation = IdleSessionPolicy.Evaluate(storedLastActiveAt, clientLastActiveAt, now, options);
        if (!evaluation.IsExpired)
        {
            return default;
        }

        _logger.LogInformation(
            "[IdleSession] Refresh token rejected because the session was idle. Subject: {Subject}, LastActiveAt: {LastActiveAt}",
            context.RefreshTokenPrincipal?.GetClaim(OpenIddictConstants.Claims.Subject),
            evaluation.EffectiveLastActiveAt);

        context.Reject(
            error: OpenIddictConstants.Errors.InvalidGrant,
            description: IdleSessionPolicy.ExpiredErrorDescription);

        return default;
    }
}

public sealed class AttachIdleSessionRefreshTokenHandler
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    private readonly IOptionsMonitor<IdleSessionOptions> _options;

    public AttachIdleSessionRefreshTokenHandler(IOptionsMonitor<IdleSessionOptions> options)
    {
        _options = options;
    }

    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (context.RefreshTokenPrincipal is null)
        {
            return default;
        }

        var options = _options.CurrentValue;
        if (!options.Enable)
        {
            return default;
        }

        var now = DateTimeOffset.UtcNow;
        var existingLastActiveAt = IdleSessionPolicy.ParseUnixTimeSeconds(
            context.RefreshTokenPrincipal.GetClaim(IdleSessionPolicy.LastActiveClaim));
        var clientLastActiveAt = context.Request is null
            ? null
            : IdleSessionPolicy.NormalizeClientActivityUnixTimeSeconds(
                (string?)context.Request.GetParameter(IdleSessionPolicy.LastActiveParameter),
                now,
                options);
        var nextLastActiveAt = IdleSessionPolicy.ResolveNextLastActiveAt(existingLastActiveAt, clientLastActiveAt, now);

        context.RefreshTokenPrincipal.SetClaim(
            IdleSessionPolicy.LastActiveClaim,
            nextLastActiveAt.ToString(CultureInfo.InvariantCulture));

        return default;
    }
}
