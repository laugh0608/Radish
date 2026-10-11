using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Xunit;
using static OpenIddict.Server.OpenIddictServerEvents;
using TokenHandlers = OpenIddict.Server.OpenIddictServerHandlers.Protection;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class OpenIddictTokenLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RealJwtGenerationAndValidation_ShouldKeepTokensClaimsAndRejections(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        using var scope = capture.SensitiveScope();
        var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        var generation = new GenerateTokenContext(fixture.Transaction())
        {
            TokenType = OpenIddictConstants.TokenTypeIdentifiers.AccessToken,
            SecurityTokenHandler = new JsonWebTokenHandler()
        };
        generation.SecurityTokenDescriptor.Subject = new ClaimsIdentity(new[] { new Claim("sub", HttpBoundaryCapture.Secret), new Claim("private", HttpBoundaryCapture.Secret) });
        generation.SecurityTokenDescriptor.Issuer = "https://issuer.invalid/";
        generation.SecurityTokenDescriptor.Audience = "resource";
        generation.SecurityTokenDescriptor.TokenType = "at+jwt";
        generation.SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        await new TokenHandlers.GenerateIdentityModelToken().HandleAsync(generation);
        var token = generation.Token;
        Assert.False(string.IsNullOrEmpty(token));
        await new TokenHandlers.GenerateIdentityModelToken().HandleAsync(generation);
        Assert.Equal(token, generation.Token);
        OpenIddictRequestLoggingTests.AssertCodes(capture, "oidc.server.jwt_created");
        AssertTokenOmitted(capture, token!);
        capture.Clear();
        foreach (var valid in new[] { true, false })
        {
            var validation = new ValidateTokenContext(fixture.Transaction())
            {
                Token = token!, SecurityTokenHandler = new JsonWebTokenHandler(),
                TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = "https://issuer.invalid/", ValidAudience = "resource", ValidTypes = new[] { "at+jwt" },
                    IssuerSigningKey = valid ? key : new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)),
                    RequireSignedTokens = true, ValidateIssuerSigningKey = true, ValidateLifetime = true
                }
            };
            validation.ValidTokenTypes.Add(OpenIddictConstants.TokenTypeIdentifiers.AccessToken);
            await new TokenHandlers.ValidateIdentityModelToken().HandleAsync(validation);
            Assert.Equal(!valid, validation.IsRejected);
            if (valid)
            {
                Assert.Equal(HttpBoundaryCapture.Secret, validation.Principal!.GetClaim("sub"));
                Assert.Equal(OpenIddictConstants.TokenTypeIdentifiers.AccessToken, validation.Principal!.GetTokenType());
            }
            else Assert.Equal("invalid_token", validation.Error);
            Assert.Equal(token, validation.Token);
            OpenIddictRequestLoggingTests.AssertCodes(capture, valid ? "oidc.server.token_validated" : "oidc.server.token_validation_failed");
            AssertTokenOmitted(capture, token!);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task PresentersAndAudiences_ShouldPreservePrincipalAndValidationOutcomes(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        foreach (var audience in new[] { false, true })
        foreach (var kind in new[] { "valid", "missing", "invalid" })
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", HttpBoundaryCapture.Secret) }));
            var actual = kind == "valid" ? "expected" : HttpBoundaryCapture.Secret;
            if (kind != "missing")
            {
                if (audience) principal.SetAudiences(ImmutableArray.Create(actual));
                else principal.SetPresenters(ImmutableArray.Create(actual));
            }
            var context = new ValidateTokenContext(fixture.Transaction()) { Principal = principal };
            if (audience)
            {
                context.ValidAudiences.Add("expected");
                await new TokenHandlers.ValidateAudiences().HandleAsync(context);
            }
            else
            {
                context.ValidPresenters.Add("expected");
                await new TokenHandlers.ValidatePresenters().HandleAsync(context);
            }
            Assert.Same(principal, context.Principal);
            Assert.Equal(kind != "valid", context.IsRejected);
            if (context.IsRejected) Assert.Equal("invalid_token", context.Error);
            OpenIddictRequestLoggingTests.AssertCodes(capture, kind == "valid" ? null : "oidc.server.token_" + (audience ? "audience_" : "presenter_") + kind);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task TokenEntryAndReplayRevocation_ShouldKeepStatusesAndConsumedFailures(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        foreach (var status in new[] { "valid", "inactive", "rejected", "invalid", "redeemed", "revoke-failure", "revoke-cancel" })
        {
            var token = new object();
            var manager = new Mock<IOpenIddictTokenManager>(MockBehavior.Strict);
            manager.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(token);
            manager.Setup(m => m.HasStatusAsync(token, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((object _, string value, CancellationToken _) => value == (status.StartsWith("revoke-", StringComparison.Ordinal) ? "redeemed" : status));
            var revoke = manager.Setup(m => m.RevokeByAuthorizationIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()));
            if (status == "revoke-failure") revoke.Throws(new IOException(HttpBoundaryCapture.Secret));
            else if (status == "revoke-cancel") revoke.Throws(new OperationCanceledException(HttpBoundaryCapture.Secret));
            else revoke.ReturnsAsync(2L);
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", HttpBoundaryCapture.Secret) }))
                .SetTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
            var context = new ValidateTokenContext(fixture.Transaction())
            {
                TokenId = HttpBoundaryCapture.Secret, AuthorizationId = HttpBoundaryCapture.Secret, Principal = principal
            };
            await new TokenHandlers.ValidateTokenEntry(manager.Object).HandleAsync(context);
            Assert.Same(principal, context.Principal);
            Assert.Equal(status != "valid", context.IsRejected);
            if (context.IsRejected) Assert.Equal(status == "inactive" ? "authorization_pending" : status == "rejected" ? "access_denied" : "invalid_token", context.Error);
            var replay = status is "redeemed" or "revoke-failure" or "revoke-cancel";
            manager.Verify(m => m.RevokeByAuthorizationIdAsync(HttpBoundaryCapture.Secret, It.IsAny<CancellationToken>()), replay ? Times.Once() : Times.Never());
            var expected = status switch
            {
                "valid" => Array.Empty<string>(),
                "inactive" => new[] { "oidc.server.inactive_token_rejected" },
                "rejected" => new[] { "oidc.server.rejected_token_refused" },
                "invalid" => new[] { "oidc.server.invalid_token_rejected" },
                "redeemed" => new[] { "oidc.server.replay_tokens_revoked", "oidc.server.redeemed_token_rejected" },
                _ => new[] { "oidc.server.replay_revocation_failed", "oidc.server.redeemed_token_rejected" }
            };
            Assert.Equal(candidate && environment == "Production" ? expected.Where(e => e.Contains("replay_", StringComparison.Ordinal)) : expected, capture.Codes);
            if (status.StartsWith("revoke-", StringComparison.Ordinal))
                Assert.Equal(status == "revoke-cancel" ? "cancelled" : "io", capture.Events.First(e => e.Code == "oidc.server.replay_revocation_failed").FailureKind);
            OpenIddictLoggingFixture.AssertSafe(capture);
            capture.Clear();
        }
    }

    private static void AssertTokenOmitted(HttpBoundaryCapture capture, string token)
    {
        Assert.DoesNotContain(token, capture.Output.ToString());
        foreach (var entry in capture.Sink.Events)
            Assert.DoesNotContain(token, entry.RenderMessage() + string.Join(" ", entry.Properties));
    }
}
