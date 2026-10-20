using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

internal sealed record OpenIddictCoreEvent(string Source, int Id, LogLevel Level, string Code);

internal static class OpenIddictCoreEvents
{
    internal const string Application = "OpenIddict.Core.OpenIddictApplicationManager";
    internal const string Authorization = "OpenIddict.Core.OpenIddictAuthorizationManager";
    internal const string Token = "OpenIddict.Core.OpenIddictTokenManager";
    internal static readonly OpenIddictCoreEvent[] All =
    [
        new(Application, 6159, LogLevel.Warning, "oidc.core.application.public_client_secret_rejected"),
        new(Application, 6160, LogLevel.Information, "oidc.core.application.client_secret_missing"),
        new(Application, 6161, LogLevel.Information, "oidc.core.application.client_secret_invalid"),
        new(Application, 6202, LogLevel.Information, "oidc.core.application.post_logout_redirect_uri_rejected"),
        new(Application, 6162, LogLevel.Information, "oidc.core.application.redirect_uri_rejected"),
        new(Application, 6163, LogLevel.Warning, "oidc.core.application.client_secret_verification_failed"),
        new(Authorization, 6164, LogLevel.Information, "oidc.core.authorization.revoke_completed"),
        new(Authorization, 6165, LogLevel.Debug, "oidc.core.authorization.revoke_concurrency"),
        new(Authorization, 6166, LogLevel.Warning, "oidc.core.authorization.revoke_failed"),
        new(Token, 6168, LogLevel.Information, "oidc.core.token.redeem_completed"),
        new(Token, 6169, LogLevel.Debug, "oidc.core.token.redeem_concurrency"),
        new(Token, 6170, LogLevel.Warning, "oidc.core.token.redeem_failed"),
        new(Token, 6171, LogLevel.Information, "oidc.core.token.reject_completed"),
        new(Token, 6172, LogLevel.Debug, "oidc.core.token.reject_concurrency"),
        new(Token, 6173, LogLevel.Warning, "oidc.core.token.reject_failed"),
        new(Token, 6174, LogLevel.Information, "oidc.core.token.revoke_completed"),
        new(Token, 6175, LogLevel.Debug, "oidc.core.token.revoke_concurrency"),
        new(Token, 6176, LogLevel.Warning, "oidc.core.token.revoke_failed"),
    ];
    internal static readonly string[] Sources = [Application, Authorization, Token];
    internal static string Unknown(string source) => source switch
    {
        Application => "oidc.core.application.unknown",
        Authorization => "oidc.core.authorization.unknown",
        Token => "oidc.core.token.unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };

    internal static void AssertEvents(HttpBoundaryCapture capture, params int[] ids)
    {
        var rows = ids.Select(id => All.Single(row => row.Id == id)).ToArray();
        var visible = rows.Where(row => !capture.Candidate || capture.Environment == "Development" || row.Level >= LogLevel.Warning).ToArray();
        Assert.Equal(visible.Select(row => row.Code), capture.Codes);
        Assert.Equal(visible.Select(row => row.Level < LogLevel.Warning), capture.Events.Select(e => e.Diagnostic));
        Assert.Equal(visible.Select(row => capture.Candidate && row.Level < LogLevel.Warning ? "Info" : row.Level.ToString()), capture.Events.Select(e => e.Level));
        OpenIddictLoggingFixture.AssertSafe(capture);
    }
}
