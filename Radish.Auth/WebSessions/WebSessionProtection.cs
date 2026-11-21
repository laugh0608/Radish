using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Radish.Auth.WebSessions;

public sealed class WebSessionProtection(Func<IDataProtectionProvider> createProvider)
{
    private readonly Lazy<IDataProtectionProvider> _provider = new(createProvider);

    public static WebSessionProtection FromKeyRing(IConfiguration configuration, string contentRoot) =>
        new(() => WebSessionKeyRing.Create(configuration, contentRoot));

    public string ProtectLogin<T>(string stateDigest, T value) => Protect($"Login.v1.{stateDigest}", value);
    public T ReadLogin<T>(string stateDigest, string value) => Read<T>($"Login.v1.{stateDigest}", value);

    public string ProtectTokens<T>(Guid session, T value) => Protect($"Tokens.v1.{session:N}", value);
    public T ReadTokens<T>(Guid session, string value) => Read<T>($"Tokens.v1.{session:N}", value);

    private string Protect<T>(string purpose, T value) =>
        _provider.Value.CreateProtector(purpose).Protect(JsonSerializer.Serialize(value));

    private T Read<T>(string purpose, string value) =>
        JsonSerializer.Deserialize<T>(_provider.Value.CreateProtector(purpose).Unprotect(value)) ??
        throw new CryptographicException("The protected Web session value is missing.");
}

public static class WebSessionSecrets
{
    public static string NewSecret() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static bool IsSecret([NotNullWhen(true)] string? value) => value is { Length: 43 } &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    public static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string Proof(string cookie) => WebEncoders.Base64UrlEncode(
        SHA256.HashData(Encoding.UTF8.GetBytes("Radish.WebSession.Csrf.v1:" + cookie)));

    public static bool VerifyProof(string cookie, string? proof) => IsSecret(proof) &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Proof(cookie)), Encoding.ASCII.GetBytes(proof));
}
