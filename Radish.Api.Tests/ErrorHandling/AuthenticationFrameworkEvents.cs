using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Radish.Api.Tests.ErrorHandling;

internal sealed record AuthenticationFrameworkEvent(string Assembly, string Source, int Id, string Name, LogLevel Level, string Code)
{
    public MethodInfo Generator => System.Reflection.Assembly.Load(Assembly).GetType("Microsoft.Extensions.Logging.LoggingExtensions", true)!
        .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        .Single(m => m.GetCustomAttribute<LoggerMessageAttribute>() is { } a && a.EventId == Id);
}

internal static class AuthenticationFrameworkEvents
{
    internal const string Jwt = "Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerHandler";
    internal const string Cookie = "Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationHandler";
    internal const string Authorization = "Microsoft.AspNetCore.Authorization.DefaultAuthorizationService";
    internal const string Middleware = "Microsoft.AspNetCore.Authorization.AuthorizationMiddleware";
    internal static readonly AuthenticationFrameworkEvent[] All =
    [
        new("Microsoft.AspNetCore.Authentication.JwtBearer", Jwt, 1, "TokenValidationFailed", LogLevel.Information, "auth.jwt_validation_failed"),
        new("Microsoft.AspNetCore.Authentication.JwtBearer", Jwt, 2, "TokenValidationSucceeded", LogLevel.Debug, "auth.jwt_validation_succeeded"),
        new("Microsoft.AspNetCore.Authentication.JwtBearer", Jwt, 3, "ProcessingMessageFailed", LogLevel.Error, "auth.jwt_processing_failed"),
        new("Microsoft.AspNetCore.Authentication.JwtBearer", Jwt, 4, "ForbiddenResponseHasStarted", LogLevel.Debug, "auth.jwt_forbidden_response_started"),
        new("Microsoft.AspNetCore.Authentication", Jwt, 7, "AuthenticationSchemeNotAuthenticatedWithFailure", LogLevel.Information, "auth.jwt_authentication_failed"),
        new("Microsoft.AspNetCore.Authentication", Jwt, 8, "AuthenticationSchemeAuthenticated", LogLevel.Debug, "auth.jwt_authenticated"),
        new("Microsoft.AspNetCore.Authentication", Jwt, 9, "AuthenticationSchemeNotAuthenticated", LogLevel.Debug, "auth.jwt_not_authenticated"),
        new("Microsoft.AspNetCore.Authentication", Jwt, 12, "AuthenticationSchemeChallenged", LogLevel.Information, "auth.jwt_challenged"),
        new("Microsoft.AspNetCore.Authentication", Jwt, 13, "AuthenticationSchemeForbidden", LogLevel.Information, "auth.jwt_forbidden"),
        new("Microsoft.AspNetCore.Authentication", Cookie, 7, "AuthenticationSchemeNotAuthenticatedWithFailure", LogLevel.Information, "auth.cookie_authentication_failed"),
        new("Microsoft.AspNetCore.Authentication", Cookie, 8, "AuthenticationSchemeAuthenticated", LogLevel.Debug, "auth.cookie_authenticated"),
        new("Microsoft.AspNetCore.Authentication", Cookie, 9, "AuthenticationSchemeNotAuthenticated", LogLevel.Debug, "auth.cookie_not_authenticated"),
        new("Microsoft.AspNetCore.Authentication.Cookies", Cookie, 10, "AuthenticationSchemeSignedIn", LogLevel.Information, "auth.cookie_signed_in"),
        new("Microsoft.AspNetCore.Authentication.Cookies", Cookie, 11, "AuthenticationSchemeSignedOut", LogLevel.Information, "auth.cookie_signed_out"),
        new("Microsoft.AspNetCore.Authentication", Cookie, 12, "AuthenticationSchemeChallenged", LogLevel.Information, "auth.cookie_challenged"),
        new("Microsoft.AspNetCore.Authentication", Cookie, 13, "AuthenticationSchemeForbidden", LogLevel.Information, "auth.cookie_forbidden"),
        new("Microsoft.AspNetCore.Authorization", Authorization, 1, "UserAuthorizationSucceeded", LogLevel.Debug, "auth.authorization_succeeded"),
        new("Microsoft.AspNetCore.Authorization", Authorization, 2, "UserAuthorizationFailed", LogLevel.Information, "auth.authorization_failed")
    ];
    internal static readonly (string Source, string UnknownCode)[] Sources =
    [
        (Jwt, "auth.jwt_unknown"), (Cookie, "auth.cookie_unknown"),
        (Authorization, "auth.authorization_unknown"), (Middleware, "auth.authorization_middleware_unknown")
    ];

    internal static void Generate(AuthenticationFrameworkEvent row, ILogger logger) =>
        row.Generator.Invoke(null, row.Generator.GetParameters().Select(p => p.ParameterType == typeof(ILogger) ? (object)logger :
            p.ParameterType == typeof(string) ? HttpBoundaryCapture.Secret :
            p.ParameterType == typeof(Exception) ? new UnreadableAuthenticationException() :
            throw new InvalidOperationException("Unexpected authentication generator argument: " + p.ParameterType.FullName)).ToArray());
}

internal sealed class UnreadableAuthenticationException : Exception
{
    public override string Message => throw new InvalidOperationException("Unexpected exception message access");
    public override string ToString() => throw new InvalidOperationException("Unexpected exception formatting");
}
